namespace StateLandGovernance.WorkflowGovernance.Application.Commands;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Exceptions;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Mappings;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning.Routing;

public sealed class WorkflowPlanningService(ILeaseCaseRepository cases, IDocumentAnalysisRepository analyses,
    IDocumentCompletenessAssessmentRepository completeness, IWorkflowPlanningStore store,
    ICaseScreeningReadiness screening, IWorkflowGovernanceUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task<WorkflowPlanningState> GetAsync(Guid caseId, VerifiedAuthorityContext? authority, CancellationToken ct)
    {
        var (leaseCase, policy, binding, _, missing) = await PrepareAsync(caseId, authority, ct);
        var history = await store.GetPlansAsync(caseId, ct);
        var active = history.SingleOrDefault(p => p.Id == leaseCase.ActiveWorkflowPlan?.Id.Value);
        if (leaseCase.ActiveWorkflowPlan is not null && active is null) missing.Add("The active plan has no durable input bindings; an authorized governed revision is required.");
        if (active is not null && active.Binding != binding) missing.Add("The active workflow plan is stale; generate a governed revision before approval.");
        return new(caseId, leaseCase.Revision, leaseCase.CurrentVerifiedFactSnapshotId == Guid.Empty ? null : leaseCase.CurrentVerifiedFactSnapshotId,
            policy is not null && missing.Count == 0, missing.Distinct().ToList(), binding, active, history,
            authority?.Capabilities.Contains("WorkflowPlanApprover") == true && missing.Count == 0 && active?.Status == "Draft");
    }

    private async Task<(LeaseCase Case, RoutingPolicyDto? Policy, string? Binding, string? InputBindings, List<string> Missing)> PrepareAsync(
        Guid caseId, VerifiedAuthorityContext? authority, CancellationToken ct)
    {
        var leaseCase = await cases.GetByIdAsync(new(caseId), ct) ?? throw new LeaseCaseNotFoundException(caseId);
        var state = await screening.GetAsync(caseId, null, null, ct);
        var missing = state.MissingInputs.ToList();
        if (!state.IsCurrent || leaseCase.LatestScreening is null) missing.Add("A current C4 assessment recorded by C3 is required.");
        else try { leaseCase.ProgressWorkflow(); } catch (Exception ex) when (ex.GetType().Namespace?.Contains("WorkflowGovernance.Domain.Exceptions") == true) { missing.Add(ex.Message); }
        var now = clock.GetUtcNow().UtcDateTime;
        if (authority is null) missing.Add("Verified case-scoped WorkflowPlanner authority is required; officer role alone is insufficient.");
        else authority.ToDomain().EnsureAuthorizes(authority.ActorId, "WorkflowPlanner",
            new(StateLandGovernance.WorkflowGovernance.Domain.Authority.AuthorityScopeKind.LeaseCase, caseId.ToString("D")), now);
        if (await store.HasExecutionAsync(caseId, ct)) missing.Add("Workflow execution is already activated; use its governed change process.");
        var snapshot = leaseCase.CurrentVerifiedFactSnapshotId == Guid.Empty ? null
            : await analyses.GetSnapshotByIdAsync(new(leaseCase.CurrentVerifiedFactSnapshotId), ct);
        var facts = snapshot is null ? new Dictionary<string,string>() : FactVerificationMapper.ToDto(snapshot).Entries
            .Where(e => e.EffectiveValue is not null).ToDictionary(e => e.FactCode, e => e.EffectiveValue!.CanonicalValue);
        var applicable = (await store.GetPoliciesAsync(ct)).Where(p => p.ApprovedByActorId != Guid.Empty
            && !string.IsNullOrWhiteSpace(p.SourceReference) && p.EffectiveFrom <= now && (!p.EffectiveUntil.HasValue || p.EffectiveUntil > now)
            && p.ApplicableFacts.Count > 0 && p.ApplicableFacts.All(f => facts.TryGetValue(f.Key, out var value) && value == f.Value)).ToList();
        RoutingPolicyDto? policy = applicable.Count == 1 ? applicable[0] : null;
        if (policy is null) missing.Add(applicable.Count > 1 ? "Applicable routing rules are ambiguous; an approved selection is required." : "Routing rules not configured for this verified snapshot.");
        if (policy?.ProjectRouting != null)
        {
            if (ProjectWorkflowGraph.SourceBindingError(policy.ProjectRouting, state.LatestAssessment, state.IsDevelopmentSpatialMock) is { } spatialError)
                missing.Add(spatialError);
            try { ProjectWorkflowGraph.Build(policy, caseId.ToString("D") + ":" + leaseCase.CurrentVerifiedFactSnapshotId, facts); }
            catch (InvalidOperationException ex) { missing.Add(ex.Message); }
        }
        var inputBindings = policy is null || state.LatestAssessment is null ? null : JsonSerializer.Serialize(new {
            snapshot = state.VerifiedFactSnapshotId, assessment = state.LatestAssessment.Assessment,
            state.LatestAssessment.InputFingerprint, state.LatestAssessment.PolicyFingerprint,
            state.LatestAssessment.InputJson, state.LatestAssessment.SourceBindingsJson, policy }, CaseScreeningService.JsonOptions);
        var binding = inputBindings is null ? null : Fingerprint(inputBindings);
        return (leaseCase, policy, binding, inputBindings, missing);
    }

    public async Task<WorkflowPlanningState> GenerateAsync(Guid caseId, GenerateWorkflowPlanRequest request,
        VerifiedAuthorityContext authority, CancellationToken ct)
    {
        var (leaseCase, policy, binding, inputBindings, missing) = await PrepareAsync(caseId, authority, ct);
        CheckVersion(leaseCase, request.VerifiedFactSnapshotId, request.ExpectedCaseRevision);
        if (missing.Count > 0 || policy is null || binding is null) throw new InvalidOperationException(string.Join(" ", missing));
        CheckPlan(leaseCase, request.ExpectedPlanId, request.ExpectedPlanRevision);
        if (leaseCase.CompletedWorkflow is not null) throw new InvalidOperationException("A completed workflow cannot be replaced through planning.");
        if (await store.HasExecutionAsync(caseId, ct)) throw new InvalidOperationException("An activated workflow requires its governed execution change process; planning cannot replace it.");
        // Execution/activation is a later slice. Never replace a plan that already has an execution record.
        var assessment = (await completeness.GetByLeaseCaseIdAsync(leaseCase.Id, ct)).OrderByDescending(a => a.AssessedAt).First();
        var now = clock.GetUtcNow().UtcDateTime;
        var snapshot = await analyses.GetSnapshotByIdAsync(new(request.VerifiedFactSnapshotId), ct);
        var facts = FactVerificationMapper.ToDto(snapshot!).Entries.ToDictionary(e => e.FactCode, e => e.EffectiveValue.CanonicalValue);
        var plan = BuildPlan(caseId, request.VerifiedFactSnapshotId, assessment.Id.Value, policy, binding, now, facts);
        if (leaseCase.ActiveWorkflowPlan is null) leaseCase.SetInitialWorkflowPlan(plan);
        else leaseCase.ReviseWorkflowPlan(plan, authority.ToDomain(), request.RevisionReason ?? "");
        var old = (await store.GetPlansAsync(caseId, ct)).SingleOrDefault(p => p.Id == request.ExpectedPlanId);
        if (old is not null) await store.SaveAsync(caseId, old with { Status = "Superseded", Revision = leaseCase.WorkflowPlanHistory.Single(p => p.Id.Value == old.Id).Revision,
            RevisionReason = request.RevisionReason, SupersededBy = authority.ActorId, SupersededAt = now }, ct);
        await store.SaveAsync(caseId, ToDto(plan, binding, authority.ActorId, null, request.RevisionReason, policy.RequiredEvidence, inputBindings!), ct);
        await unitOfWork.CommitAsync(ct);
        return await GetAsync(caseId, authority, ct);
    }

    public async Task<WorkflowPlanningState> ApproveAsync(Guid caseId, Guid planId, ApproveWorkflowPlanRequest request,
        VerifiedAuthorityContext authority, CancellationToken ct)
    {
        var state = await GetAsync(caseId, authority, ct);
        if (!state.Ready) throw new InvalidOperationException(string.Join(" ", state.MissingInputs));
        var leaseCase = (await cases.GetByIdAsync(new(caseId), ct))!;
        CheckVersion(leaseCase, request.VerifiedFactSnapshotId, request.ExpectedCaseRevision);
        CheckPlan(leaseCase, planId, request.ExpectedPlanRevision);
        leaseCase.ActiveWorkflowPlan!.ApprovePlan(authority.ActorId, clock.GetUtcNow().UtcDateTime, authority.ToDomain());
        leaseCase.EvaluateOverallReadiness();
        await store.SaveAsync(caseId, state.ActivePlan! with { Status = "Approved", Revision = leaseCase.ActiveWorkflowPlan.Revision,
            ApprovedAt = leaseCase.ActiveWorkflowPlan.ApprovedAt, ApprovedBy = authority.ActorId }, ct);
        await unitOfWork.CommitAsync(ct);
        return await GetAsync(caseId, authority, ct);
    }

    public static WorkflowPlan BuildPlan(Guid caseId, Guid snapshotId, Guid completenessId, RoutingPolicyDto policy,
        string binding, DateTime now, IReadOnlyDictionary<string, string>? verifiedFacts = null)
    {
        var config = new InstitutionalRoutingConfig(new(policy.Identifier, policy.Version), policy.StandardDays,
            policy.DivisionalDays, policy.CommissionerDays, policy.StandardCapability, policy.DivisionalCapability, policy.CommissionerCapability);
        if (policy.ProjectRouting != null)
            return new(new(Guid.NewGuid()), new(caseId), WorkflowPlanSource.RuleBased, new(snapshotId), new(completenessId),
                config.RuleSetReference, null, now, ProjectWorkflowGraph.Build(policy, caseId.ToString("D") + ":" + snapshotId,
                    verifiedFacts ?? throw new InvalidOperationException("Exact verified facts are required for project routing.")));
        var generated = InstitutionalRoutingEngine.GeneratePlanStages(policy.Institutions.Order(StringComparer.Ordinal).Select(i => new InstitutionCode(i)), policy.RequiresCommissionerApproval, config);
        // The existing engine creates random IDs; canonicalize identity without changing its routing decisions.
        var ids = generated.ToDictionary(s => s.Id, s => new WorkflowStageId(StableId(binding + ":" + s.StageCode.Value)));
        var stages = generated.Select(s => new WorkflowStage(ids[s.Id], s.StageCode, s.InstitutionCode, s.StageType,
            s.RequiredOfficerCapability, s.RoutingReasonCode, s.ReasonDescription, s.TargetDurationDays, s.IsFinalDecision, s.Prerequisites.Select(p => ids[p])));
        return new(new(Guid.NewGuid()), new(caseId), WorkflowPlanSource.RuleBased, new(snapshotId), new(completenessId),
            config.RuleSetReference, null, now, stages);
    }
    public static string Fingerprint(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static Guid StableId(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0,16));
    private static void CheckVersion(LeaseCase leaseCase, Guid snapshot, int revision)
    {
        if (leaseCase.Revision != revision) throw new LeaseCaseConcurrencyException(leaseCase.Id.Value, revision, leaseCase.Revision);
        if (snapshot == Guid.Empty || snapshot != leaseCase.CurrentVerifiedFactSnapshotId) throw new InvalidOperationException("The requested verified snapshot is not current.");
    }
    private static void CheckPlan(LeaseCase leaseCase, Guid? id, int? revision)
    {
        if (leaseCase.ActiveWorkflowPlan?.Id.Value != id || leaseCase.ActiveWorkflowPlan?.Revision != revision)
            throw new InvalidOperationException("The workflow plan or revision is outdated. Reload before changing it.");
    }
    private static WorkflowPlanDto ToDto(WorkflowPlan plan, string binding, Guid actor, Guid? approver, string? reason, IReadOnlyList<string> evidence, string inputs) =>
        new(plan.Id.Value, plan.Revision, plan.Status.ToString(), plan.VerifiedFactSnapshotId.Value, plan.RuleSetReference.Identifier,
            plan.RuleSetReference.Version, binding, actor, approver, plan.CreatedAt, plan.ApprovedAt, reason, evidence,
            plan.Stages.Select(s => new PlanStageDto(s.Id.Value, s.StageCode.Value, s.InstitutionCode.Value, s.RequiredOfficerCapability,
                s.StageType.ToString(), s.Prerequisites.Select(p => p.Value).ToList(), s.TargetDurationDays, s.ReasonDescription)).ToList(), inputs);
}
