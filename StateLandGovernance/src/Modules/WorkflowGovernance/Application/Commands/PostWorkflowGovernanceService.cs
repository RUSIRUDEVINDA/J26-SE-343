namespace StateLandGovernance.WorkflowGovernance.Application.Commands;

using System.Text.Json;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;

public sealed record RecordDsNotificationRequest(int ExpectedExecutionRevision, Guid FinalDecisionId,
    DateTime NotifiedAtUtc, Guid DocumentId, Guid VersionId, string Reason);
public sealed record PostWorkflowReviewRequest(int ExpectedExecutionRevision, string Reason);
public sealed record AssignPostWorkflowFollowUpRequest(Guid AssessmentId, string Institution, DateTime DueAtUtc, string Reason);
public sealed record ResolvePostWorkflowFollowUpRequest(int ExpectedRevision, string Reason, string EvidenceReference);
public sealed record PostWorkflowState(Guid CaseId, Guid? ExecutionId, int? ExecutionRevision, bool CanAssess,
    IReadOnlyList<string> MissingInputs, PostWorkflowAssessmentResult? Assessment, bool AssessmentCurrent,
    IReadOnlyList<CorrectionRecord> FollowUps, Guid? FinalizedRecordId, string IntegrityStatus);

public sealed class PostWorkflowGovernanceService(ILeaseCaseRepository cases, IWorkflowExecutionStore executions,
    ICorrectionRepository records, IGovernedDocumentRepository documents, IPostWorkflowAssessmentGateway gateway,
    ICaseScreeningReadiness screening, IWorkflowGovernanceUnitOfWork unit, TimeProvider clock, IScreeningInputRefresh? inputRefresh = null)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private void Authorize(VerifiedAuthorityContext actor, Guid caseId, string capability) => actor.ToDomain().EnsureAuthorizes(
        actor.ActorId, capability, new(AuthorityScopeKind.LeaseCase, caseId.ToString("D")), Now);
    private async Task<(PostWorkflowTrace? Trace, List<string> Missing)> TraceAsync(Guid caseId, CancellationToken ct)
    {
        var missing = new List<string>();
        var c = await cases.GetByIdAsync(new(caseId), ct) ?? throw new InvalidOperationException("Case not found.");
        var e = await executions.GetAsync(caseId, ct);
        if (e?.Status != WorkflowExecutionStatus.Completed || c.CompletedWorkflow?.ExecutionId != e?.Id)
            missing.Add("A persisted completed institutional execution and immutable final decision are required.");
        var notification = (await records.ListAsync(caseId, ct)).Where(r => r.Purpose == "PostWorkflowNotification").OrderByDescending(r => r.CreatedAtUtc).FirstOrDefault();
        if (notification == null) missing.Add("C4 model eligibility requires an actual evidence-backed DS Final Notification; the Commissioner decision cannot substitute for it.");
        var screeningState = await screening.GetAsync(caseId, null, null, ct);
        if (!screeningState.IsCurrent) missing.Add("The completed workflow's source screening/snapshot bindings are stale or incomplete.");
        else
        {
            try { c.ProgressWorkflow(); }
            catch (Exception ex) when (ex.GetType().Namespace?.Contains("WorkflowGovernance.Domain.Exceptions") == true) { missing.Add(ex.Message); }
            if (inputRefresh != null)
            {
                var fresh = await inputRefresh.ReadAsync(caseId, c.CurrentVerifiedFactSnapshotId, screeningState.ParcelId, ct);
                if (fresh.MissingInputs.Count > 0 || fresh.Prepared.InputFingerprint != screeningState.LatestAssessment?.InputFingerprint ||
                    fresh.Prepared.PolicyFingerprint != screeningState.LatestAssessment?.PolicyFingerprint)
                    missing.Add("Committed screening inputs changed; the post-workflow trace cannot be assessed as current.");
            }
        }
        if (!gateway.Available) missing.Add("C4 workflow anomaly orchestration is unavailable.");
        if (e == null || notification?.PayloadJson == null || c.CompletedWorkflow == null) return (null, missing);
        using var payload = JsonDocument.Parse(notification.PayloadJson);
        if (payload.RootElement.GetProperty("executionId").GetGuid() != e.Id.Value ||
            payload.RootElement.GetProperty("executionRevision").GetInt32() != e.Revision ||
            payload.RootElement.GetProperty("finalDecisionId").GetGuid() != c.CompletedWorkflow.FinalDecisionId.Value)
            missing.Add("The DS notification refers to a different execution revision or final decision.");
        var doc = await documents.GetByIdAsync(new(payload.RootElement.GetProperty("documentId").GetGuid()), ct);
        if (doc?.ActiveVersionId.Value != payload.RootElement.GetProperty("versionId").GetGuid()) missing.Add("DS notification source document version is stale.");
        var events = e.Stages.SelectMany(s => s.DecisionHistory.Select(h => h.PreviousDecision).Concat(s.Decision == null ? [] : new[] { s.Decision })
            .Select(d => new { stage = s.Definition.StageCode.Value, institution = s.Definition.InstitutionCode.Value, decision = d }))
            .OrderBy(x => x.decision.DecidedAt).ThenBy(x => x.decision.Id.Value)
            .Select((x, i) => new PostWorkflowTraceEvent(i + 1, x.stage + ":" + x.decision.Outcome,
                x.institution, x.decision.DecidingOfficerId.ToString("D"), x.decision.DecidedAt)).ToList();
        events.AddRange(e.Stages.Where(s => s.StartedAt.HasValue && s.ActingOfficerId.HasValue).Select(s =>
            new PostWorkflowTraceEvent(0, s.Definition.StageCode.Value + ":Started", s.Definition.InstitutionCode.Value,
                s.ActingOfficerId!.Value.ToString("D"), s.StartedAt!.Value)));
        events = events.OrderBy(e => e.AtUtc).ThenBy(e => e.Activity, StringComparer.Ordinal)
            .Select((e, i) => e with { Sequence = i + 1 }).ToList();
        var notifiedAt = payload.RootElement.GetProperty("notifiedAtUtc").GetDateTime();
        if (events.Any(e => e.AtUtc > notifiedAt)) missing.Add("Notification predates a recorded institutional decision.");
        events.Add(new(events.Count + 1, "DS Final Notification", "DS", notification.RequestedBy.ToString("D"), notifiedAt));
        var binding = JsonSerializer.Serialize(new { caseId, executionId = e.Id.Value, e.Revision,
            snapshotId = c.CurrentVerifiedFactSnapshotId, final = c.CompletedWorkflow, notification.PayloadJson, events,
            screeningState.LatestAssessment?.InputFingerprint, screeningState.LatestAssessment?.PolicyFingerprint,
            screeningAssessment = screeningState.LatestAssessment?.Assessment }, CaseScreeningService.JsonOptions);
        return (new(caseId, e.Id.Value, e.Revision, WorkflowPlanningService.Fingerprint(binding), events,
            screeningState.IsDevelopmentSpatialMock), missing);
    }
    public async Task<PostWorkflowState> GetAsync(Guid caseId, CancellationToken ct)
    {
        var (trace, missing) = await TraceAsync(caseId, ct); var all = await records.ListAsync(caseId, ct);
        var e = await executions.GetAsync(caseId, ct);
        var stored = all.Where(r => r.Purpose == "PostWorkflowAssessment").OrderByDescending(r => r.CreatedAtUtc).FirstOrDefault();
        var assessment = stored?.PayloadJson == null ? null : JsonSerializer.Deserialize<PostWorkflowAssessmentResult>(stored.PayloadJson, CaseScreeningService.JsonOptions);
        var current = assessment != null && trace != null && missing.Count == 0 && assessment.InputFingerprint == trace.InputFingerprint;
        return new(caseId, e?.Id.Value, e?.Revision, missing.Count == 0, missing, assessment, current,
            all.Where(r => r.Purpose == "PostWorkflowFollowUp").ToArray(), all.SingleOrDefault(r => r.Purpose == "FinalGovernanceRecord")?.Id,
            "Unavailable: C4's audit integrity endpoint requires its own audit record ID; a C3 finalized-manifest registration contract is not configured.");
    }
    public async Task RecordNotificationAsync(Guid caseId, RecordDsNotificationRequest request, InstitutionalActorContext actor, CancellationToken ct)
    {
        Authorize(actor.Authority, caseId, "DSNotificationReviewer");
        if (actor.Institution != "DS") throw new UnauthorizedAccessException("Only the case-authorized DS may record this notification evidence.");
        var c = await cases.GetByIdAsync(new(caseId), ct) ?? throw new InvalidOperationException("Case not found.");
        var e = await executions.GetAsync(caseId, ct);
        var doc = await documents.GetByIdAsync(new(request.DocumentId), ct);
        var ds = e?.Stages.SingleOrDefault(s => s.Definition.StageCode.Value == "DS");
        if (e?.Status != WorkflowExecutionStatus.Completed || e.Revision != request.ExpectedExecutionRevision ||
            c.CompletedWorkflow?.ExecutionId != e.Id || c.CompletedWorkflow.FinalDecisionId.Value != request.FinalDecisionId ||
            request.NotifiedAtUtc.Kind != DateTimeKind.Utc || request.NotifiedAtUtc > Now || request.NotifiedAtUtc < c.CompletedWorkflow.DecidedAtUtc ||
            string.IsNullOrWhiteSpace(request.Reason) || doc?.LeaseCaseId.Value != caseId || doc.ActiveVersionId.Value != request.VersionId || ds == null ||
            (doc.LogicalCategory != WorkflowExecutionService.DocumentCategory(e.Id.Value, ds.Definition.StageId.Value) &&
             doc.LogicalCategory != $"WorkflowEvidence:{e.Id.Value:N}:{ds.Definition.StageId.Value:N}"))
            throw new InvalidOperationException("Current completed execution, exact final decision, chronological notification time and actual source document/version are required.");
        var all = await records.ListAsync(caseId, ct);
        if (all.Any(r => r.Purpose == "FinalGovernanceRecord" || r.Purpose == "PostWorkflowNotification"))
            throw new InvalidOperationException("An existing notification/final record is preserved; explicit governed revision is required.");
        await records.SaveAsync(new CorrectionRecord { Id = Guid.NewGuid(), LeaseCaseId = caseId, Purpose = "PostWorkflowNotification",
            RequestedBy = actor.Authority.ActorId, CreatedAtUtc = Now, DueDateUtc = Now, Status = "EvidenceRecorded", AssignedInstitution = "DS",
            Instructions = request.Reason, PayloadJson = JsonSerializer.Serialize(new { executionId = e.Id.Value, executionRevision = e.Revision,
                finalDecisionId = request.FinalDecisionId, documentId = request.DocumentId, versionId = request.VersionId,
                checksum = doc.Versions.Single(v => v.Id.Value == request.VersionId).Checksum.Value, notifiedAtUtc = request.NotifiedAtUtc }, CaseScreeningService.JsonOptions) }, ct);
        await unit.CommitAsync(ct);
    }
    public async Task AssessAsync(Guid caseId, PostWorkflowReviewRequest request, VerifiedAuthorityContext actor, CancellationToken ct)
    {
        Authorize(actor, caseId, "PostWorkflowReviewer");
        var (trace, missing) = await TraceAsync(caseId, ct);
        if (trace == null || missing.Count > 0 || trace.Revision != request.ExpectedExecutionRevision || string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidOperationException(string.Join(" ", missing.Append("Current execution revision and review reason are required.")));
        if ((await records.ListAsync(caseId, ct)).Any(r => r.Purpose == "FinalGovernanceRecord")) throw new InvalidOperationException("Final governance record is immutable.");
        var result = await gateway.AssessAsync(trace, ct);
        result = result with { SourceTrace = trace };
        var (fresh, freshMissing) = await TraceAsync(caseId, ct);
        if (freshMissing.Count > 0 || fresh?.InputFingerprint != trace.InputFingerprint || result.CaseId != caseId || result.ExecutionId != trace.ExecutionId || result.InputFingerprint != trace.InputFingerprint)
            throw new InvalidOperationException("C4 result/trace changed; no current assessment was recorded.");
        await records.SaveAsync(new CorrectionRecord { Id = result.AssessmentId, LeaseCaseId = caseId, Purpose = "PostWorkflowAssessment",
            RequestedBy = actor.ActorId, CreatedAtUtc = Now, DueDateUtc = Now, Status = result.Flagged ? "HumanFollowUpRequired" : "AdvisoryReviewRequired",
            Instructions = request.Reason, PayloadJson = JsonSerializer.Serialize(result, CaseScreeningService.JsonOptions) }, ct);
        await unit.CommitAsync(ct);
    }
    public async Task AssignFollowUpAsync(Guid caseId, AssignPostWorkflowFollowUpRequest request, VerifiedAuthorityContext actor, CancellationToken ct)
    {
        Authorize(actor, caseId, "PostWorkflowReviewer"); var state = await GetAsync(caseId, ct);
        var e = await executions.GetAsync(caseId, ct);
        if (!state.AssessmentCurrent || state.Assessment?.AssessmentId != request.AssessmentId || state.FinalizedRecordId != null ||
            !e!.Stages.Any(s => s.Definition.InstitutionCode.Value == request.Institution) || request.DueAtUtc.Kind != DateTimeKind.Utc || string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidOperationException("Current assessment, participating institution, explicit UTC due date and reason are required.");
        await records.SaveAsync(new CorrectionRecord { Id = Guid.NewGuid(), LeaseCaseId = caseId, Purpose = "PostWorkflowFollowUp",
            RequestedBy = actor.ActorId, CreatedAtUtc = Now, DueDateUtc = request.DueAtUtc, AssignedInstitution = request.Institution,
            Status = "Assigned", Instructions = request.Reason, PayloadJson = JsonSerializer.Serialize(new { assessmentId = request.AssessmentId }, CaseScreeningService.JsonOptions) }, ct);
        await unit.CommitAsync(ct);
    }
    public async Task ResolveFollowUpAsync(Guid caseId, Guid id, ResolvePostWorkflowFollowUpRequest request, InstitutionalActorContext actor, CancellationToken ct)
    {
        Authorize(actor.Authority, caseId, "PostWorkflowFollowUpReviewer");
        var state = await GetAsync(caseId, ct); var r = await records.GetAsync(caseId, id, ct);
        if (state.FinalizedRecordId != null || !state.AssessmentCurrent || r?.Purpose != "PostWorkflowFollowUp" ||
            r.Status != "Assigned" || r.Revision != request.ExpectedRevision || r.AssignedInstitution != actor.Institution ||
            string.IsNullOrWhiteSpace(request.Reason) || string.IsNullOrWhiteSpace(request.EvidenceReference))
            throw new InvalidOperationException("Current assigned follow-up, institutional authority, revision, reason and evidence are required.");
        using var payload = JsonDocument.Parse(r.PayloadJson!);
        if (payload.RootElement.GetProperty("assessmentId").GetGuid() != state.Assessment!.AssessmentId) throw new InvalidOperationException("Follow-up belongs to a previous assessment.");
        r.Status = "ResolvedAfterHumanReview"; r.Revision++;
        r.FollowUpReviews.Add(new(actor.Authority.ActorId, actor.Institution, Now, request.Reason, request.EvidenceReference));
        await records.SaveAsync(r, ct); await unit.CommitAsync(ct);
    }
    public async Task FinalizeAsync(Guid caseId, PostWorkflowReviewRequest request, VerifiedAuthorityContext actor, CancellationToken ct)
    {
        Authorize(actor, caseId, "GovernanceRecordFinalizer"); var state = await GetAsync(caseId, ct);
        var followUps = state.FollowUps.Where(r => {
            using var payload = JsonDocument.Parse(r.PayloadJson!);
            return payload.RootElement.GetProperty("assessmentId").GetGuid() == state.Assessment?.AssessmentId;
        }).ToArray();
        if (!state.AssessmentCurrent || state.ExecutionRevision != request.ExpectedExecutionRevision || state.FinalizedRecordId != null ||
            string.IsNullOrWhiteSpace(request.Reason) || followUps.Any(r => r.Status != "ResolvedAfterHumanReview") || (state.Assessment!.Flagged && followUps.Length == 0))
            throw new InvalidOperationException("Current advisory assessment, explicit review and resolved required follow-ups are necessary; anomaly flags never establish corruption or a lease decision.");
        var manifest = JsonSerializer.Serialize(new { state.CaseId, state.ExecutionId, state.ExecutionRevision, state.Assessment,
            followUps, actorId = actor.ActorId, reviewReason = request.Reason, finalizedAtUtc = Now }, CaseScreeningService.JsonOptions);
        await records.SaveAsync(new CorrectionRecord { Id = Guid.NewGuid(), LeaseCaseId = caseId, Purpose = "FinalGovernanceRecord",
            RequestedBy = actor.ActorId, CreatedAtUtc = Now, DueDateUtc = Now, Status = "FinalizedAwaitingIntegrityContract",
            Instructions = request.Reason, RequirementSourceReference = "SHA-256:" + WorkflowPlanningService.Fingerprint(manifest), PayloadJson = manifest }, ct);
        await unit.CommitAsync(ct);
    }
}
