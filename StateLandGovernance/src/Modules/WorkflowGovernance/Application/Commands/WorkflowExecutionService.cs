namespace StateLandGovernance.WorkflowGovernance.Application.Commands;
using System.Text.Json;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Exceptions;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;

public sealed class WorkflowExecutionService(ILeaseCaseRepository cases, IWorkflowExecutionStore executions,
    IWorkflowPlanningStore plans, ICaseScreeningReadiness screening, IGovernedDocumentRepository documents,
    IWorkflowGovernanceUnitOfWork unit, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private async Task<List<string>> MissingAsync(Guid caseId, CancellationToken ct)
    {
        var c = await cases.GetByIdAsync(new(caseId), ct) ?? throw new LeaseCaseNotFoundException(caseId);
        var state = await screening.GetAsync(caseId, null, null, ct);
        var missing = state.MissingInputs.ToList();
        if (!state.IsCurrent) missing.Add("A current finalized C4 assessment and both completeness gates are required.");
        else try { c.ProgressWorkflow(); }
        catch (Exception ex) when (ex.GetType().Namespace?.Contains("WorkflowGovernance.Domain.Exceptions") == true) { missing.Add(ex.Message); }
        if (c.ActiveWorkflowPlan?.Status != WorkflowPlanStatus.Approved) missing.Add("An explicitly approved current workflow plan is required.");
        else
        {
            var plan = (await plans.GetPlansAsync(caseId, ct)).SingleOrDefault(p => p.Id == c.ActiveWorkflowPlan.Id.Value);
            if (plan == null || plan.SnapshotId != c.CurrentVerifiedFactSnapshotId || string.IsNullOrWhiteSpace(plan.InputBindingsJson))
                missing.Add("The approved plan has missing or stale input bindings.");
            else
            {
                using var bound = JsonDocument.Parse(plan.InputBindingsJson);
                if (!bound.RootElement.TryGetProperty("inputFingerprint", out var fingerprint) ||
                    fingerprint.GetString() != state.LatestAssessment?.InputFingerprint)
                    missing.Add("Screening inputs changed after plan generation.");
                if (!bound.RootElement.TryGetProperty("assessment", out var assessment) ||
                    !assessment.TryGetProperty("assessmentId", out var assessmentId) ||
                    assessmentId.GetGuid() != state.LatestAssessment?.Assessment.GetProperty("assessmentId").GetGuid())
                    missing.Add("The current C4 assessment differs from the exact assessment used for planning.");
                if (bound.RootElement.TryGetProperty("policy", out var policyElement))
                {
                    var policy = policyElement.Deserialize<RoutingPolicyDto>(CaseScreeningService.JsonOptions)!;
                    if (policy.ProjectRouting != null && ProjectWorkflowGraph.SourceBindingError(policy.ProjectRouting,
                        state.LatestAssessment, state.IsDevelopmentSpatialMock) is { } spatialError) missing.Add(spatialError);
                    var policies = await plans.GetPoliciesAsync(ct);
                    var current = policies.SingleOrDefault(p => p.Identifier == policy.Identifier && p.Version == policy.Version);
                    if (current == null || current.EffectiveFrom > Now || current.EffectiveUntil <= Now ||
                        policies.Count(p => p.Identifier == policy.Identifier && p.EffectiveFrom <= Now && (!p.EffectiveUntil.HasValue || p.EffectiveUntil > Now)) != 1 ||
                        JsonSerializer.Serialize(current, CaseScreeningService.JsonOptions) != JsonSerializer.Serialize(policy, CaseScreeningService.JsonOptions))
                        missing.Add("The approved routing policy binding is no longer current.");
                }
                else missing.Add("The approved routing policy binding is unavailable.");
            }
        }
        return missing.Distinct().ToList();
    }
    private async Task EnsureCurrentAsync(Guid caseId, CancellationToken ct)
    {
        var missing = await MissingAsync(caseId, ct);
        if (missing.Count > 0) throw new InvalidOperationException(string.Join(" ", missing));
    }
    private static void EnsureRevision(WorkflowExecution execution, int revision)
    { if (execution.Revision != revision) throw new InvalidOperationException("Execution revision is stale. Reload before submitting."); }
    private static void Authorize(InstitutionalActorContext actor, WorkflowExecution execution, WorkflowStageDefinition stage, DateTime at)
    {
        new VerifiedInstitutionalAuthoritySnapshot(actor.Authority.ActorId, new(actor.Institution), actor.Authority.Capabilities,
            actor.Authority.ToDomain().Scope, actor.Authority.ValidFrom, actor.Authority.VerificationTime, actor.Authority.ValidUntil)
            .EnsureAuthorizes(actor.Authority.ActorId, stage.InstitutionCode, stage.RequiredOfficerCapability,
                new(AuthorityScopeKind.LeaseCase, execution.LeaseCaseId.Value.ToString("D")), at);
    }
    private static VerifiedInstitutionalAuthoritySnapshot InstitutionAuthority(InstitutionalActorContext actor) =>
        new(actor.Authority.ActorId, new(actor.Institution), actor.Authority.Capabilities, actor.Authority.ToDomain().Scope,
            actor.Authority.ValidFrom, actor.Authority.VerificationTime, actor.Authority.ValidUntil);
    public async Task<WorkflowExecutionState> GetAsync(Guid caseId, CancellationToken ct)
    {
        var missing = await MissingAsync(caseId, ct);
        var e = await executions.GetAsync(caseId, ct);
        var c = (await cases.GetByIdAsync(new(caseId), ct))!;
        var plan = (await plans.GetPlansAsync(caseId, ct)).FirstOrDefault(p => p.Id == e?.WorkflowPlanId.Value);
        var mock = plan?.InputBindingsJson.Contains("\"isDevelopmentMock\":true", StringComparison.Ordinal) == true;
        return new(caseId, e?.Id.Value, e?.WorkflowPlanId.Value, e?.Revision, e?.Status.ToString() ?? "NotActivated", missing.Count == 0,
            missing, e?.Stages.Select(StageDto).ToList() ?? new(), e?.PlanHistory.Select(h => new ExecutionPlanHistoryDto(
                h.PreviousDefinition.WorkflowPlanId.Value, h.ReplacementDefinition.WorkflowPlanId.Value, h.ActorId, h.Reason, h.At, h.ExecutionRevision)).ToList() ?? new(), mock,
                c.Revision, c.ActiveWorkflowPlan?.Id.Value, c.ActiveWorkflowPlan?.Revision, c.ActiveWorkflowPlan?.Status.ToString());
    }
    public async Task<WorkflowExecutionState> StartAsync(Guid caseId, StartExecutionRequest request, VerifiedAuthorityContext authority, CancellationToken ct)
    {
        authority.ToDomain().EnsureAuthorizes(authority.ActorId, "WorkflowExecutor", new(AuthorityScopeKind.LeaseCase, caseId.ToString("D")), Now);
        await EnsureCurrentAsync(caseId, ct);
        var c = (await cases.GetByIdAsync(new(caseId), ct))!;
        if (c.Revision != request.ExpectedCaseRevision || c.ActiveWorkflowPlan!.Id.Value != request.PlanId || c.ActiveWorkflowPlan.Revision != request.ExpectedPlanRevision)
            throw new InvalidOperationException("Case or approved plan revision is stale.");
        if (await executions.GetAsync(caseId, ct) != null) throw new InvalidOperationException("An execution already exists; use governed revisions.");
        var definition = c.ActiveWorkflowPlan.CreateExecutionDefinition();
        // Explicit approval gates on external/Cabinet -> DS -> regional processing. Commissioner sees the regional rejection.
        definition = definition.WithPrerequisiteGatePolicy(new(c.ActiveWorkflowPlan.RuleSetReference.Identifier,
            c.ActiveWorkflowPlan.RuleSetReference.Version, definition.Stages.Where(s => !s.IsFinalDecision).Select(s => s.StageId)));
        var execution = new WorkflowExecution(new(Guid.NewGuid()), definition, Now);
        await executions.SaveAsync(execution, ct); await unit.CommitAsync(ct);
        return await GetAsync(caseId, ct);
    }
    public async Task<RestrictedInstitutionalRequest> RestrictedAsync(Guid caseId, Guid stageId, InstitutionalActorContext actor, CancellationToken ct)
    {
        var e = await executions.GetAsync(caseId, ct) ?? throw new InvalidOperationException("No execution exists.");
        var stage = e.Stages.SingleOrDefault(s => s.Definition.StageId.Value == stageId) ?? throw new InvalidOperationException("Stage not found.");
        Authorize(actor, e, stage.Definition, Now);
        var dto = StageDto(stage);
        return new(caseId, e.Id.Value, e.Revision, stageId, stage.Definition.InstitutionCode.Value, stage.Status.ToString(),
            stage.Definition.ReasonDescription, dto.Decision, dto.DecisionHistory, (await cases.GetByIdAsync(new(caseId), ct))!.Revision,
            (stage.Status == WorkflowStageExecutionStatus.Ready || stage.Definition.IsFinalDecision && e.Status == WorkflowExecutionStatus.AwaitingConsensus) &&
                (await MissingAsync(caseId, ct)).Count == 0);
    }
    public async Task StageStartAsync(Guid caseId, Guid stageId, int revision, InstitutionalActorContext actor, CancellationToken ct)
    {
        await EnsureCurrentAsync(caseId, ct);
        var e = await executions.GetAsync(caseId, ct) ?? throw new InvalidOperationException("No execution exists.");
        EnsureRevision(e, revision);
        var stage = e.Stages.Single(s => s.Definition.StageId.Value == stageId);
        Authorize(actor, e, stage.Definition, Now);
        if (stage.Definition.IsFinalDecision && e.Status == WorkflowExecutionStatus.AwaitingConsensus)
        {
            // The user requires approval from every required external/DS prerequisite, while regional rejection is forwarded for independent review.
            var participants = e.Stages.Where(s => !s.Definition.IsFinalDecision).ToList();
            var required = participants.Count(s => s.Definition.StageCode.Value is not ("PROVINCIAL_LC" or "INTER_PROVINCIAL_LC"));
            e.EvaluateConsensus(new(Guid.NewGuid()), new(new(Guid.NewGuid()), "USER_PROJECT_REQUIRED_REVIEWS", e.RuleSetReference.Version,
                e.LeaseCaseId, e.WorkflowPlanId, e.WorkflowPlanRevision, e.RuleSetReference, ConsensusRuleType.ApprovalThreshold,
                required, e.StartedAt, participants.Select(s => new ConsensusParticipantRule(s.Definition.StageId,
                    s.Definition.InstitutionCode, s.Definition.StageCode.Value is not ("PROVINCIAL_LC" or "INTER_PROVINCIAL_LC"),
                    s.Definition.StageCode.Value is not ("PROVINCIAL_LC" or "INTER_PROVINCIAL_LC")))), Now);
        }
        e.StartStage(new(stageId), actor.Authority.ActorId, Now, InstitutionAuthority(actor));
        await executions.SaveAsync(e, ct); await unit.CommitAsync(ct);
    }
    public async Task AddDepartmentAsync(Guid caseId, AddDepartmentRequest request, InstitutionalActorContext actor, CancellationToken ct)
    {
        await EnsureCurrentAsync(caseId, ct);
        var e = await executions.GetAsync(caseId, ct) ?? throw new InvalidOperationException("No execution exists.");
        EnsureRevision(e, request.ExpectedExecutionRevision);
        var ds = e.Stages.Single(s => s.Definition.StageCode.Value == "DS");
        Authorize(actor, e, ds.Definition, Now);
        actor.Authority.ToDomain().EnsureAuthorizes(actor.Authority.ActorId, "WorkflowPlanSuperseder",
            new(AuthorityScopeKind.LeaseCase, caseId.ToString("D")), Now);
        if (ds.StartedAt.HasValue || ds.Decision != null) throw new InvalidOperationException("DS work already started; explicit resolution is required before department addition.");
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new InvalidOperationException("Department addition requires a reason.");
        var c = (await cases.GetByIdAsync(new(caseId), ct))!;
        if (c.Revision != request.ExpectedCaseRevision) throw new InvalidOperationException("Case revision is stale.");
        var old = (await plans.GetPlansAsync(caseId, ct)).Single(p => p.Id == e.WorkflowPlanId.Value);
        using var input = JsonDocument.Parse(old.InputBindingsJson);
        var policy = input.RootElement.GetProperty("policy").Deserialize<RoutingPolicyDto>(CaseScreeningService.JsonOptions)!;
        var institution = new InstitutionCode(request.Institution).Value;
        if (policy.ProjectRouting == null || !policy.Institutions.Contains(institution))
            throw new InvalidOperationException("Department is not in the versioned extensible project pool.");
        if (e.Stages.Any(s => s.Definition.InstitutionCode.Value == institution)) throw new InvalidOperationException("Department already participates.");
        var added = new WorkflowStage(new(Guid.NewGuid()), new("EXTERNAL_" + institution), new(institution), WorkflowStageType.Review,
            policy.StandardCapability, new("DS_GOVERNED_ADDITION"), request.Reason, null, false,
            e.Stages.Where(s => s.Definition.StageCode.Value == "CABINET").Select(s => s.Definition.StageId));
        var stages = c.ActiveWorkflowPlan!.Stages.Select(s => s.StageCode.Value == "DS"
            ? new WorkflowStage(s.Id, s.StageCode, s.InstitutionCode, s.StageType, s.RequiredOfficerCapability,
                s.RoutingReasonCode, s.ReasonDescription, s.TargetDurationDays, s.IsFinalDecision, s.Prerequisites.Append(added.Id)) : s).Append(added).ToList();
        var replacement = new WorkflowPlan(new(Guid.NewGuid()), c.Id, WorkflowPlanSource.RuleBased,
            c.ActiveWorkflowPlan.VerifiedFactSnapshotId, c.ActiveWorkflowPlan.DocumentCompletenessAssessmentId,
            c.ActiveWorkflowPlan.RuleSetReference, null, Now, stages);
        c.ReviseWorkflowPlan(replacement, actor.Authority.ToDomain(), request.Reason);
        await plans.SaveAsync(caseId, old with { Status = "Superseded", Revision = c.WorkflowPlanHistory.Single(p => p.Id.Value == old.Id).Revision,
            SupersededAt = Now, SupersededBy = actor.Authority.ActorId, RevisionReason = request.Reason }, ct);
        var inputMap = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(old.InputBindingsJson)!;
        inputMap["departmentAddition"] = JsonSerializer.SerializeToElement(new { institution, request.Reason, actorId = actor.Authority.ActorId, at = Now });
        var inputJson = JsonSerializer.Serialize(inputMap);
        await plans.SaveAsync(caseId, old with { Id = replacement.Id.Value, Revision = replacement.Revision, Status = "Draft",
            GeneratedBy = actor.Authority.ActorId, ApprovedBy = null, ApprovedAt = null, CreatedAt = Now,
            SupersededAt = null, SupersededBy = null, RevisionReason = request.Reason,
            Binding = WorkflowPlanningService.Fingerprint(inputJson), InputBindingsJson = inputJson,
            Stages = stages.Select(s => new PlanStageDto(s.Id.Value, s.StageCode.Value, s.InstitutionCode.Value,
                s.RequiredOfficerCapability, s.StageType.ToString(), s.Prerequisites.Select(p => p.Value).ToArray(), s.TargetDurationDays, s.ReasonDescription)).ToArray() }, ct);
        await unit.CommitAsync(ct);
    }
    public async Task ApproveRevisionAsync(Guid caseId, Guid planId, int expectedCaseRevision, int expectedPlanRevision,
        int expectedExecutionRevision, VerifiedAuthorityContext authority, CancellationToken ct)
    {
        var c = (await cases.GetByIdAsync(new(caseId), ct))!;
        var e = await executions.GetAsync(caseId, ct) ?? throw new InvalidOperationException("No execution exists.");
        EnsureRevision(e, expectedExecutionRevision);
        var scope = new AuthorityScope(AuthorityScopeKind.LeaseCase, caseId.ToString("D"));
        authority.ToDomain().EnsureAuthorizes(authority.ActorId, "WorkflowPlanApprover", scope, Now);
        authority.ToDomain().EnsureAuthorizes(authority.ActorId, "WorkflowPlanSuperseder", scope, Now);
        if (c.Revision != expectedCaseRevision || c.ActiveWorkflowPlan?.Id.Value != planId || c.ActiveWorkflowPlan.Revision != expectedPlanRevision)
            throw new InvalidOperationException("Case/plan revision is stale.");
        var state = await screening.GetAsync(caseId, null, null, ct);
        if (!state.IsCurrent || state.MissingInputs.Count != 0) throw new InvalidOperationException("Screening/completeness inputs are stale or incomplete.");
        var plan = (await plans.GetPlansAsync(caseId, ct)).Single(p => p.Id == planId);
        using var inputs = JsonDocument.Parse(plan.InputBindingsJson);
        if (plan.SnapshotId != c.CurrentVerifiedFactSnapshotId || inputs.RootElement.GetProperty("inputFingerprint").GetString() != state.LatestAssessment?.InputFingerprint ||
            inputs.RootElement.GetProperty("assessment").GetProperty("assessmentId").GetGuid() != state.LatestAssessment?.Assessment.GetProperty("assessmentId").GetGuid())
            throw new InvalidOperationException("Revision screening binding is stale.");
        var policy = inputs.RootElement.GetProperty("policy").Deserialize<RoutingPolicyDto>(CaseScreeningService.JsonOptions)!;
        if (policy.ProjectRouting != null && ProjectWorkflowGraph.SourceBindingError(policy.ProjectRouting,
            state.LatestAssessment, state.IsDevelopmentSpatialMock) is { } spatialError) throw new InvalidOperationException(spatialError);
        var policies = await plans.GetPoliciesAsync(ct);
        var current = policies.SingleOrDefault(p => p.Identifier == policy.Identifier && p.Version == policy.Version);
        if (current == null || current.EffectiveFrom > Now || current.EffectiveUntil <= Now ||
            policies.Count(p => p.Identifier == policy.Identifier && p.EffectiveFrom <= Now && (!p.EffectiveUntil.HasValue || p.EffectiveUntil > Now)) != 1 ||
            JsonSerializer.Serialize(current, CaseScreeningService.JsonOptions) != JsonSerializer.Serialize(policy, CaseScreeningService.JsonOptions))
            throw new InvalidOperationException("Revision routing policy is stale or ambiguous.");
        var preview = c.ActiveWorkflowPlan;
        var approvedCopy = new WorkflowPlan(preview.Id, preview.LeaseCaseId, preview.Source, preview.VerifiedFactSnapshotId,
            preview.DocumentCompletenessAssessmentId, preview.RuleSetReference, preview.RecommendationReference, preview.CreatedAt, preview.Stages);
        approvedCopy.ApprovePlan(authority.ActorId, Now, authority.ToDomain());
        var definition = approvedCopy.CreateExecutionDefinition();
        definition = definition.WithPrerequisiteGatePolicy(new(c.ActiveWorkflowPlan.RuleSetReference.Identifier,
            c.ActiveWorkflowPlan.RuleSetReference.Version, definition.Stages.Where(s => !s.IsFinalDecision).Select(s => s.StageId)));
        e.AdoptRevisedPlan(definition, expectedExecutionRevision, authority.ActorId, authority.ToDomain(), plan.RevisionReason!, Now);
        c.ActiveWorkflowPlan.ApprovePlan(authority.ActorId, Now, authority.ToDomain());
        await plans.SaveAsync(caseId, plan with { Status = "Approved", ApprovedBy = authority.ActorId,
            ApprovedAt = c.ActiveWorkflowPlan.ApprovedAt, Revision = c.ActiveWorkflowPlan.Revision }, ct);
        await executions.SaveAsync(e, ct); await unit.CommitAsync(ct);
    }
    public async Task DecideAsync(Guid caseId, Guid stageId, StageDecisionRequest request, InstitutionalActorContext actor, CancellationToken ct)
    {
        await EnsureCurrentAsync(caseId, ct);
        var e = await executions.GetAsync(caseId, ct) ?? throw new InvalidOperationException("No execution exists.");
        EnsureRevision(e, request.ExpectedExecutionRevision);
        var stage = e.Stages.Single(s => s.Definition.StageId.Value == stageId);
        Authorize(actor, e, stage.Definition, Now);
        if (!Enum.TryParse<WorkflowStageDecisionOutcome>(request.Outcome, false, out var outcome) ||
            outcome is not (WorkflowStageDecisionOutcome.Approved or WorkflowStageDecisionOutcome.ApprovedWithConditions or WorkflowStageDecisionOutcome.Rejected))
            throw new InvalidOperationException("Choose Approved, ApprovedWithConditions or Rejected.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidOperationException("An explicit institutional decision reason is required; document references are not a reason.");
        if (request.Documents.Count == 0) throw new InvalidOperationException("A decision document is required; submission is not acceptance.");
        var refs = new List<string>();
        foreach (var binding in request.Documents)
        {
            var doc = await documents.GetByIdAsync(new(binding.DocumentId), ct);
            if (doc == null || doc.LeaseCaseId.Value != caseId || doc.LogicalCategory != DocumentCategory(e.Id.Value, stageId) || doc.ActiveVersionId.Value != binding.VersionId ||
                !doc.Versions.Any(v => v.Id.Value == binding.VersionId && v.SubmittedByActorId == actor.Authority.ActorId))
                throw new InvalidOperationException("Decision evidence must be an exact version submitted for this institution's stage.");
            refs.Add($"document:{binding.DocumentId:D}/version:{binding.VersionId:D}");
        }
        var reason = request.Reason + " Evidence: " + string.Join("; ", refs);
        if (request.PriorDecisionId is { } prior)
            e.ReviseStageDecision(new(stageId), request.ExpectedExecutionRevision, new(prior), new(Guid.NewGuid()), outcome,
                reason, request.Conditions, actor.Authority.ActorId, InstitutionAuthority(actor), request.RevisionReason ?? "", Now);
        else e.RecordStageDecision(new(Guid.NewGuid()), new(stageId), outcome, reason, request.Conditions,
            actor.Authority.ActorId, Now, InstitutionAuthority(actor));
        await executions.SaveAsync(e, ct); await unit.CommitAsync(ct);
    }
    public static string DocumentCategory(Guid executionId, Guid stageId) => $"WorkflowApproval:{executionId:N}:{stageId:N}";
    public Task<IReadOnlyList<Guid>> RejectedCasesAsync(CancellationToken ct) => executions.RejectedCasesAsync(ct);
    private static ExecutionDecisionDto DecisionDto(WorkflowStageDecision d) => new(d.Id.Value, d.Outcome.ToString(), d.Reason, d.Conditions.ToArray(), d.DecidingOfficerId, d.DecidedAt);
    private static ExecutionStageDto StageDto(WorkflowStageExecution s) => new(s.Definition.StageId.Value, s.Definition.StageCode.Value,
        s.Definition.InstitutionCode.Value, s.Definition.RequiredOfficerCapability, s.Status.ToString(), s.Definition.Prerequisites.Select(p => p.Value).ToArray(),
        s.Definition.ReasonDescription, s.Decision == null ? null : DecisionDto(s.Decision),
        s.DecisionHistory.Select(h => new ExecutionDecisionHistoryDto(DecisionDto(h.PreviousDecision), DecisionDto(h.ReplacementDecision), h.Reason, h.ExecutionRevision)).ToArray());
}
