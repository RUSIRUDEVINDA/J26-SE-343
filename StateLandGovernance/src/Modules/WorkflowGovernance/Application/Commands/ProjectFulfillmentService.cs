namespace StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.Fulfillment;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;

public sealed class ProjectFulfillmentService(ILeaseCaseRepository cases, ICorrectionRepository records,
    IGovernedDocumentRepository documents, IWorkflowExecutionStore executions, ICaseScreeningReadiness screening,
    IWorkflowGovernanceUnitOfWork unit, TimeProvider clock, WorkflowExecutionService? executionReadiness = null)
{
    public const string UserSource = "User-specified project workflow, 2026-10-16; not independently verified statutory requirements";
    public static readonly IReadOnlyList<ProjectDocumentObligation> Catalogue = new ProjectDocumentObligation[] {
        new("APPLICANT_SCHEDULE17_AFFIDAVIT", "APPLICANT", "17th Schedule affidavit — user cites Land Order 207(2)(ix)"),
        new("APPLICANT_TAX_AFFIDAVIT", "APPLICANT", "Tax affidavit"), new("APPLICANT_NIC", "APPLICANT", "NIC"),
        new("DS_RELEVANT_LAND_REPORT", "DS", "Relevant-land report", false, "When recommending lease; officer must resolve applicability",
            new[] { "PROVINCIAL_LC", "COMMISSIONER" }),
        new("DS_SCHEDULE19", "DS", "19th Schedule — user cites Land Order 206.3"),
        new("DS_LEASE_RENTAL_SHEET", "DS", "Lease rental calculation sheet — official title pending", true),
        new("LC_APPROVAL_1", "COMMISSIONER", "Land Commissioner approval document 1 — title pending", true),
        new("LC_APPROVAL_2", "COMMISSIONER", "Land Commissioner approval document 2 — title pending", true),
        new("LC_APPROVAL_3", "COMMISSIONER", "Land Commissioner approval document 3 — title pending", true),
        new("MINISTRY_LANDS_APPROVAL", "MINISTRY_LANDS", "Ministry of Lands approval document"),
        new("SURVEY_PLAN", "SURVEY", "Survey Plan"), new("SURVEY_LOT_DETAILS_LIST", "SURVEY", "Lot Details List"),
        new("VALUATION_DOCUMENT", "VALUATION", "Valuation document — exact title pending", true) };
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private static void Authority(VerifiedAuthorityContext actor, Guid caseId, string capability, DateTime at) =>
        actor.ToDomain().EnsureAuthorizes(actor.ActorId, capability, new(AuthorityScopeKind.LeaseCase, caseId.ToString("D")), at);
    private Task<StateLandGovernance.WorkflowGovernance.Domain.LeaseCases.LeaseCase?> Case(Guid id, CancellationToken ct) => cases.GetByIdAsync(new(id), ct);
    public async Task<ProjectFulfillmentState> GetAsync(Guid caseId, CancellationToken ct)
    {
        var c = await Case(caseId, ct) ?? throw new InvalidOperationException("Case not found.");
        var all = (await records.ListAsync(caseId, ct)).Where(r => r.Purpose == "ProjectFulfillment").ToList();
        var unconfigured = Catalogue.Where(o => !all.Any(r => r.ClassificationCode == o.Code)).ToList();
        var missing = new List<string>();
        if (unconfigured.Count > 0) missing.Add("Role-specific document obligations require explicit due dates/configuration: " + string.Join(", ", unconfigured.Select(o => o.Code)));
        if (all.Any(r => r.TitlePending && r.Status != "Waived")) missing.Add("Exact owner-sourced document titles remain pending; these obligations cannot be accepted or waived by placeholder.");
        var execution = await executions.GetAsync(caseId, ct);
        foreach (var report in all.Where(r => r.ClassificationCode == "DS_RELEVANT_LAND_REPORT"))
            if (report.Status != "Waived" && !ReportCoverageCurrent(report, execution))
                missing.Add("DS relevant-land report requires a current explicit lease recommendation and exact-version Provincial-to-Commissioner forwarding receipts; non-applicability needs a governed waiver operation.");
        foreach (var r in all.Where(r => r.Status == "Fulfilled"))
        {
            var submission = r.Submissions.Last(); var doc = await documents.GetByIdAsync(new(submission.DocumentId), ct);
            if (doc?.ActiveVersionId.Value != submission.VersionId) missing.Add($"Fulfillment {r.ClassificationCode} has a superseded document version; explicit review is required.");
        }
        if (execution?.Status != WorkflowExecutionStatus.Completed) missing.Add("Institutional execution and its explicit final decision are not completed.");
        else if (execution.CreateCompletionRecord().FinalDecisionOutcome == WorkflowStageDecisionOutcome.Rejected)
            missing.Add("The final institutional decision is Rejected. Fulfillment cannot turn rejection into approval.");
        if (execution != null && executionReadiness != null && !(await executionReadiness.GetAsync(caseId, ct)).Current)
            missing.Add("The execution's approved plan, routing/spatial evidence or exact screening assessment binding is stale/incomplete.");
        var screeningState = await screening.GetAsync(caseId, null, null, ct);
        if (!screeningState.IsCurrent || screeningState.MissingInputs.Count > 0) missing.Add("Current screening/completeness/snapshot bindings are required.");
        else try { c.ProgressWorkflow(); }
        catch (Exception ex) when (ex.GetType().Namespace?.Contains("WorkflowGovernance.Domain.Exceptions") == true) { missing.Add(ex.Message); }
        var readyFinal = missing.Count == 0;
        var readyHandoff = readyFinal && all.All(r => r.Status is "Fulfilled" or "Waived") &&
            c.ApprovalConditions.All(r => r.Status is FulfillmentStatus.Fulfilled or FulfillmentStatus.Waived) && c.CompletedWorkflow != null;
        return new(caseId, c.Revision, c.Status.ToString(), unconfigured,
            all.Select(r => new FulfillmentRequirementDto(r.Id, r.Revision, r.ClassificationCode, r.AssignedInstitution!, r.Instructions,
                r.Status, r.DueDateUtc, r.TitlePending, r.RequirementSourceReference!, r.Submissions.ToArray(), r.Forwarding.ToArray(), r.LeaseRecommended)).ToArray(), missing,
            readyFinal, readyHandoff, c.ApprovalConditions.Select(r => new CaseConditionDto(r.Id, r.InstitutionCode.Value, r.Description, r.Status.ToString())).ToArray());
    }
    public async Task ConfigureAsync(Guid caseId, ConfigureProjectFulfillmentRequest request, VerifiedAuthorityContext actor, CancellationToken ct)
    {
        Authority(actor, caseId, "FulfillmentConfigurator", Now);
        var c = await Case(caseId, ct) ?? throw new InvalidOperationException("Case not found.");
        if (c.Revision != request.ExpectedCaseRevision || c.CompletedWorkflow != null || c.HandoffPackage != null)
            throw new InvalidOperationException("Case revision is stale or the final record is immutable.");
        if (string.IsNullOrWhiteSpace(request.Version) || request.Requirements.Count == 0 || request.Requirements.Select(r => r.Code).Distinct().Count() != request.Requirements.Count)
            throw new ArgumentException("A versioned nonempty unique obligation selection is required.");
        var existing = await records.ListAsync(caseId, ct);
        foreach (var entry in request.Requirements)
        {
            var definition = Catalogue.SingleOrDefault(o => o.Code == entry.Code) ?? throw new ArgumentException("Unknown project document obligation.");
            if (existing.Any(r => r.Purpose == "ProjectFulfillment" && r.ClassificationCode == entry.Code))
                throw new InvalidOperationException("Existing obligations are preserved; replacement requires a governed configuration revision.");
            if (entry.DueAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Every due date must be explicitly supplied in UTC.");
            if (definition.TitlePending && (string.IsNullOrWhiteSpace(entry.ActualTitle) != string.IsNullOrWhiteSpace(entry.ActualTitleSourceReference)))
                throw new ArgumentException("An actual title requires its owner source reference.");
        }
        foreach (var entry in request.Requirements)
        {
            var definition = Catalogue.Single(o => o.Code == entry.Code);
            var requirement = c.AddDocumentSubmissionRequirement(new(entry.Code), entry.DueAtUtc);
            await records.SaveAsync(new CorrectionRecord { Id = requirement.Id, LeaseCaseId = caseId, RequestedBy = actor.ActorId,
                Purpose = "ProjectFulfillment", AssignedInstitution = definition.Institution, CreatedAtUtc = Now, DueDateUtc = entry.DueAtUtc,
                ClassificationCode = entry.Code, Instructions = entry.ActualTitle ?? definition.Title,
                TitlePending = definition.TitlePending && string.IsNullOrWhiteSpace(entry.ActualTitle), RequirementConfigurationVersion = request.Version,
                RequirementSourceReference = UserSource + (entry.ActualTitleSourceReference == null ? "" : "; exact title source: " + entry.ActualTitleSourceReference) }, ct);
        }
        await unit.CommitAsync(ct);
    }
    public async Task<CorrectionRecord> GetAssignedAsync(Guid caseId, Guid id, Guid actorId, string? institution, CancellationToken ct)
    {
        var r = await records.GetAsync(caseId, id, ct) ?? throw new InvalidOperationException("Requirement not found.");
        if (r.Purpose != "ProjectFulfillment") throw new InvalidOperationException("Not a project fulfillment requirement.");
        var c = (await Case(caseId, ct))!;
        if (r.AssignedInstitution == "APPLICANT" ? c.CreatedByActorId != actorId : r.AssignedInstitution != institution)
            throw new UnauthorizedAccessException("Requirement belongs to another applicant/institution.");
        return r;
    }
    public async Task SubmitAsync(Guid caseId, Guid id, Guid actorId, string? institution, DecisionDocumentBinding binding,
        int expectedRevision, string? notes, CancellationToken ct)
    {
        var r = await GetAssignedAsync(caseId, id, actorId, institution, ct);
        if (r.Revision != expectedRevision || r.Status is not ("Pending" or "ChangesRequested")) throw new InvalidOperationException("Requirement revision/state changed.");
        var doc = await documents.GetByIdAsync(new(binding.DocumentId), ct);
        if (doc?.LeaseCaseId.Value != caseId || doc.ActiveVersionId.Value != binding.VersionId || doc.LogicalCategory != "ProjectFulfillment:" + r.ClassificationCode ||
            !doc.Versions.Any(v => v.Id.Value == binding.VersionId && v.SubmittedByActorId == actorId))
            throw new InvalidOperationException("Submission requires the assigned actor's exact current document/version on this case.");
        r.Submissions.Add(new(binding.DocumentId, binding.VersionId, actorId, Now, notes));
        r.Status = "SubmittedAwaitingReview"; r.Revision++; await records.SaveAsync(r, ct); await unit.CommitAsync(ct);
    }
    public async Task ReviewAsync(Guid caseId, Guid id, FulfillmentReviewRequest request, VerifiedAuthorityContext actor, CancellationToken ct)
    {
        Authority(actor, caseId, "FulfillmentReviewer", Now);
        var r = await records.GetAsync(caseId, id, ct) ?? throw new InvalidOperationException("Requirement not found.");
        if (r.Purpose != "ProjectFulfillment" || r.Revision != request.ExpectedRevision || r.Status != "SubmittedAwaitingReview" || string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidOperationException("Awaiting submission, exact revision and review reason are required.");
        if (request.Accept && r.TitlePending) throw new InvalidOperationException("Exact official document title/source remains pending; a filename cannot resolve it.");
        if (request.Accept && r.ClassificationCode == "DS_RELEVANT_LAND_REPORT" && !ReportCoverageCurrent(r, await executions.GetAsync(caseId, ct)))
            throw new InvalidOperationException("Recommendation applicability and Provincial-to-Commissioner forwarding evidence must be established before acceptance.");
        var submission = r.Submissions.Last(); var doc = await documents.GetByIdAsync(new(submission.DocumentId), ct);
        if (doc?.LeaseCaseId.Value != caseId || doc.ActiveVersionId.Value != submission.VersionId) throw new InvalidOperationException("Submitted version is stale.");
        var c = (await Case(caseId, ct))!;
        if (request.Accept)
        {
            c.FulfillDocumentRequirement(id, new(submission.DocumentId), Now);
            c.EvaluateOverallReadiness();
        }
        r.Submissions[^1] = submission with { ReviewedBy = actor.ActorId, ReviewedAtUtc = Now, Decision = request.Accept ? "Accepted" : "Rejected", ReviewReason = request.Reason };
        r.Status = request.Accept ? "Fulfilled" : "ChangesRequested"; r.Revision++;
        await records.SaveAsync(r, ct); await unit.CommitAsync(ct);
    }
    private static bool ReportCoverageCurrent(CorrectionRecord report, WorkflowExecution? execution)
    {
        var decision = execution?.Stages.SingleOrDefault(s => s.Definition.StageCode.Value == "DS")?.Decision;
        var submission = report.Submissions.LastOrDefault();
        return report.LeaseRecommended == true && decision?.Id.Value == report.RecommendationDecisionId &&
            decision?.Outcome is WorkflowStageDecisionOutcome.Approved or WorkflowStageDecisionOutcome.ApprovedWithConditions && submission != null &&
            new[] { "PROVINCIAL_LC", "COMMISSIONER" }.All(i => report.Forwarding.Any(f => f.Institution == i && f.DocumentId == submission.DocumentId && f.VersionId == submission.VersionId));
    }
    public async Task RecordLeaseRecommendationAsync(Guid caseId, Guid id, LeaseRecommendationRequest request, InstitutionalActorContext actor, CancellationToken ct)
    {
        Authority(actor.Authority, caseId, "FulfillmentForwardReviewer", Now);
        if (actor.Institution != "DS") throw new UnauthorizedAccessException("Only the case-authorized DS can state the lease recommendation.");
        var record = await records.GetAsync(caseId, id, ct) ?? throw new InvalidOperationException("Report requirement not found.");
        if (record.Purpose != "ProjectFulfillment" || record.ClassificationCode != "DS_RELEVANT_LAND_REPORT" || record.Revision != request.ExpectedRevision || string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidOperationException("Exact report revision and recommendation reason are required.");
        var e = await executions.GetAsync(caseId, ct);
        var ds = e?.Stages.SingleOrDefault(s => s.Definition.StageCode.Value == "DS")?.Decision;
        if (ds?.Id.Value != request.DsDecisionId || ds?.Outcome is not (WorkflowStageDecisionOutcome.Approved or WorkflowStageDecisionOutcome.ApprovedWithConditions))
            throw new InvalidOperationException("The recommendation must reference the current approved/conditional DS decision.");
        if (record.RecommendationDecisionId != null) throw new InvalidOperationException("A recommendation already exists; a governed recommendation revision is required.");
        record.RecommendationDecisionId = request.DsDecisionId; record.LeaseRecommended = request.RecommendLease;
        record.RecommendationReason = request.Reason; record.Revision++;
        await records.SaveAsync(record, ct); await unit.CommitAsync(ct);
    }
    public async Task RecordForwardingAsync(Guid caseId, Guid id, FulfillmentForwardRequest request, InstitutionalActorContext actor, CancellationToken ct)
    {
        Authority(actor.Authority, caseId, "FulfillmentForwardReviewer", Now);
        if (actor.Institution is not ("PROVINCIAL_LC" or "COMMISSIONER")) throw new UnauthorizedAccessException("This report follows the supplied Provincial-to-Commissioner route only.");
        var record = await records.GetAsync(caseId, id, ct) ?? throw new InvalidOperationException("Report requirement not found.");
        if (record.Purpose != "ProjectFulfillment" || record.ClassificationCode != "DS_RELEVANT_LAND_REPORT" || record.Revision != request.ExpectedRevision ||
            record.LeaseRecommended != true || string.IsNullOrWhiteSpace(request.Reason)) throw new InvalidOperationException("Current recommendation, report revision and forwarding reason are required.");
        var submission = record.Submissions.LastOrDefault() ?? throw new InvalidOperationException("No report version has been submitted.");
        var doc = await documents.GetByIdAsync(new(submission.DocumentId), ct);
        if (doc?.ActiveVersionId.Value != submission.VersionId) throw new InvalidOperationException("Report version is stale.");
        if (record.Forwarding.Any(f => f.Institution == actor.Institution && f.DocumentId == submission.DocumentId && f.VersionId == submission.VersionId))
            throw new InvalidOperationException("Receipt already exists for this exact report version.");
        if (actor.Institution == "COMMISSIONER" && !record.Forwarding.Any(f => f.Institution == "PROVINCIAL_LC" && f.DocumentId == submission.DocumentId && f.VersionId == submission.VersionId))
            throw new InvalidOperationException("The Provincial forwarding receipt is required first.");
        record.Forwarding.Add(new(actor.Institution, submission.DocumentId, submission.VersionId, actor.Authority.ActorId, Now, request.Reason));
        record.Revision++; await records.SaveAsync(record, ct); await unit.CommitAsync(ct);
    }
    public async Task FulfillConditionAsync(Guid caseId, Guid conditionId, FulfillConditionRequest request, InstitutionalActorContext actor, CancellationToken ct)
    {
        Authority(actor.Authority, caseId, "ConditionReviewer", Now);
        var c = await Case(caseId, ct) ?? throw new InvalidOperationException("Case not found.");
        var condition = c.ApprovalConditions.SingleOrDefault(r => r.Id == conditionId) ?? throw new InvalidOperationException("Condition not found.");
        if (condition.InstitutionCode.Value != actor.Institution) throw new UnauthorizedAccessException("Condition belongs to another institution.");
        if (c.Revision != request.ExpectedCaseRevision || string.IsNullOrWhiteSpace(request.Reason) || string.IsNullOrWhiteSpace(request.EvidenceReference))
            throw new InvalidOperationException("Case revision, review reason and evidence reference are required.");
        c.FulfillCondition(conditionId, Now); c.EvaluateOverallReadiness();
        await records.SaveAsync(new CorrectionRecord { Id = Guid.NewGuid(), LeaseCaseId = caseId, Purpose = "ConditionReview", RelatedConditionId = conditionId,
            RequestedBy = actor.Authority.ActorId, CreatedAtUtc = Now, DueDateUtc = Now, ClassificationCode = "CONDITION_REVIEW", Status = "Fulfilled",
            AssignedInstitution = actor.Institution, Instructions = request.Reason, RequirementSourceReference = request.EvidenceReference }, ct);
        await unit.CommitAsync(ct);
    }
    public async Task WaiveAsync(Guid caseId, Guid id, WaiveFulfillmentRequest request,
        InstitutionalActorContext actor, CancellationToken ct)
    {
        Authority(actor.Authority, caseId, "FulfillmentWaiver", Now);
        var c = await Case(caseId, ct) ?? throw new InvalidOperationException("Case not found.");
        var record = await records.GetAsync(caseId, id, ct) ?? throw new InvalidOperationException("Requirement not found.");
        if (record.Purpose != "ProjectFulfillment" || record.Revision != request.ExpectedRequirementRevision ||
            record.Status is not ("Pending" or "ChangesRequested"))
            throw new InvalidOperationException("Requirement state or revision is stale.");
        if (actor.Institution != record.AssignedInstitution && actor.Institution != "COMMISSIONER")
            throw new UnauthorizedAccessException("Waiver authority does not cover the assigned institution.");
        if (record.TitlePending) throw new InvalidOperationException("An unknown document title cannot be resolved through a placeholder waiver.");
        if (record.ClassificationCode == "DS_RELEVANT_LAND_REPORT")
        {
            var execution = await executions.GetAsync(caseId, ct);
            var decision = execution?.Stages.SingleOrDefault(s => s.Definition.StageCode.Value == "DS")?.Decision;
            if (record.LeaseRecommended != false || decision == null || record.RecommendationDecisionId != decision.Id.Value)
                throw new InvalidOperationException("Non-applicability requires the current explicit DS recommendation not to recommend lease.");
        }
        c.WaiveFulfillment(id, false, request.ExpectedCaseRevision, actor.Authority.ToDomain(), request.Reason, request.EvidenceReference, Now);
        record.Status = "Waived"; record.Revision++;
        await records.SaveAsync(record, ct);
        await records.SaveAsync(new CorrectionRecord { Id = Guid.NewGuid(), LeaseCaseId = caseId, Purpose = "FulfillmentWaiver",
            RequestedBy = actor.Authority.ActorId, CreatedAtUtc = Now, DueDateUtc = Now,
            ClassificationCode = record.ClassificationCode, AssignedInstitution = actor.Institution, Status = "Recorded",
            Instructions = request.Reason, RequirementSourceReference = request.EvidenceReference,
            RequirementConfigurationVersion = record.RequirementConfigurationVersion }, ct);
        await unit.CommitAsync(ct);
    }

    public async Task WaiveConditionAsync(Guid caseId, Guid id, FulfillConditionRequest request, InstitutionalActorContext actor, CancellationToken ct)
    {
        Authority(actor.Authority, caseId, "FulfillmentWaiver", Now);
        var c = await Case(caseId, ct) ?? throw new InvalidOperationException("Case not found.");
        var condition = c.ApprovalConditions.SingleOrDefault(r => r.Id == id) ?? throw new InvalidOperationException("Condition not found.");
        if (condition.InstitutionCode.Value != actor.Institution) throw new UnauthorizedAccessException("Condition waiver requires its institution's verified authority.");
        c.WaiveFulfillment(id, true, request.ExpectedCaseRevision, actor.Authority.ToDomain(), request.Reason, request.EvidenceReference, Now);
        await records.SaveAsync(new CorrectionRecord { Id = Guid.NewGuid(), LeaseCaseId = caseId, Purpose = "ConditionWaiver", RelatedConditionId = id,
            RequestedBy = actor.Authority.ActorId, CreatedAtUtc = Now, DueDateUtc = Now, AssignedInstitution = actor.Institution,
            Status = "Recorded", Instructions = request.Reason, RequirementSourceReference = request.EvidenceReference }, ct);
        await unit.CommitAsync(ct);
    }

    public async Task RecordFinalAsync(Guid caseId, int expectedCaseRevision, VerifiedAuthorityContext actor, CancellationToken ct)
    {
        Authority(actor, caseId, "WorkflowFinalRecorder", Now);
        var state = await GetAsync(caseId, ct);
        if (state.CaseRevision != expectedCaseRevision || !state.ReadyForFinalRecord) throw new InvalidOperationException(string.Join(" ", state.MissingInputs));
        var c = (await Case(caseId, ct))!; if (c.CompletedWorkflow != null) throw new InvalidOperationException("A final record already exists.");
        var execution = (await executions.GetAsync(caseId, ct))!;
        foreach (var stage in execution.Stages.Where(s => s.Decision != null))
            foreach (var condition in stage.Decision!.Conditions) c.AddApprovalCondition(stage.Definition.InstitutionCode, condition);
        c.RecordCompletedWorkflow(execution.CreateCompletionRecord()); await unit.CommitAsync(ct);
    }
}
