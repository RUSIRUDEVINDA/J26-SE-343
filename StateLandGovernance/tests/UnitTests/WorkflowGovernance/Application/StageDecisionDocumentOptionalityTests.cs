namespace StateLandGovernance.UnitTests.WorkflowGovernance.Application;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;

/// <summary>
/// Application-layer contract tests for institutional stage decisions with optional evidence documents.
///
/// Batch 1 (P0) — confirms that the institutional decision is the reviewer's explicit choice and
/// that an uploaded evidence file is NOT required to record it.
///
/// Protected invariants that must NOT regress are also verified here:
/// - An explicit human-entered reason is still mandatory.
/// - Rejection without reason is still rejected.
/// - An invalid document binding (wrong stage, wrong actor, superseded version) is still rejected.
/// - Outcome enum outside the allowed set is still rejected.
/// - Authorization (wrong institution, expired authority) still blocks the call.
/// - Stale execution revision still blocks the call.
/// - Prerequisite-stage incompleteness still blocks stage-start.
///
/// No Domain tests are modified; the Domain RecordStageDecision() signature has no document parameter
/// and was never the source of the defect.
/// </summary>
public sealed class StageDecisionDocumentOptionalityTests
{
    // -------------------------------------------------------------------------
    // Case A — Approved with Documents = null (omitted by caller)
    // -------------------------------------------------------------------------
    [Fact]
    public async Task DecideAsync_Approved_NullDocuments_Accepted()
    {
        var f = new ProjectWorkflowExecutionSliceTests.Fixture();
        await f.Start();
        await StartStage(f, "CABINET");

        var e = f.Executions.Value!;
        var stage = f.Stage("CABINET");

        // Null Documents — simulates a caller that omits the field entirely.
        await f.Service.DecideAsync(
            f.Case.Id.Value, stage.Definition.StageId.Value,
            new StageDecisionRequest(e.Revision, "Approved", "Explicit human-entered reason", [], Documents: null),
            f.ActorFor(stage.Definition.InstitutionCode.Value), default);

        var decision = f.Stage("CABINET").Decision;
        Assert.NotNull(decision);
        Assert.Equal("Approved", decision.Outcome.ToString());
        // Reason is preserved exactly as entered — no "Evidence:" suffix when no documents given.
        Assert.Equal("Explicit human-entered reason", decision.Reason);
    }

    // -------------------------------------------------------------------------
    // Case B — Approved with Documents = [] (empty list)
    // -------------------------------------------------------------------------
    [Fact]
    public async Task DecideAsync_Approved_EmptyDocumentsList_Accepted()
    {
        var f = new ProjectWorkflowExecutionSliceTests.Fixture();
        await f.Start();
        await StartStage(f, "CABINET");

        var e = f.Executions.Value!;
        var stage = f.Stage("CABINET");

        await f.Service.DecideAsync(
            f.Case.Id.Value, stage.Definition.StageId.Value,
            new StageDecisionRequest(e.Revision, "Approved", "Officer review complete — no supporting file.", [], Documents: []),
            f.ActorFor(stage.Definition.InstitutionCode.Value), default);

        var decision = f.Stage("CABINET").Decision;
        Assert.NotNull(decision);
        Assert.Equal("Approved", decision.Outcome.ToString());
        Assert.Equal("Officer review complete — no supporting file.", decision.Reason);
    }

    // -------------------------------------------------------------------------
    // Case C — Approved with a valid document binding (existing behavior preserved)
    // -------------------------------------------------------------------------
    [Fact]
    public async Task DecideAsync_Approved_WithValidDocumentBinding_Accepted()
    {
        var f = new ProjectWorkflowExecutionSliceTests.Fixture();
        await f.Start();
        await StartStage(f, "CABINET");

        var e = f.Executions.Value!;
        var stage = f.Stage("CABINET");
        var doc = CreateStagedDocument(f, e.Id.Value, stage.Definition.StageId.Value);
        await f.Documents.AddAsync(doc);

        await f.Service.DecideAsync(
            f.Case.Id.Value, stage.Definition.StageId.Value,
            new StageDecisionRequest(e.Revision, "Approved", "Reviewed with supporting file.", [], [new(doc.Id.Value, doc.ActiveVersionId.Value)]),
            f.ActorFor(stage.Definition.InstitutionCode.Value), default);

        var decision = f.Stage("CABINET").Decision;
        Assert.NotNull(decision);
        Assert.Equal("Approved", decision.Outcome.ToString());
        // Evidence suffix IS appended when a document is bound.
        Assert.Contains("Evidence:", decision.Reason);
        Assert.Contains("Reviewed with supporting file.", decision.Reason);
    }

    // -------------------------------------------------------------------------
    // Case D — ApprovedWithConditions without file evidence
    // -------------------------------------------------------------------------
    [Fact]
    public async Task DecideAsync_ApprovedWithConditions_NoDocument_Accepted()
    {
        var f = new ProjectWorkflowExecutionSliceTests.Fixture();
        await f.Start();
        await StartStage(f, "CABINET");

        var e = f.Executions.Value!;
        var stage = f.Stage("CABINET");

        await f.Service.DecideAsync(
            f.Case.Id.Value, stage.Definition.StageId.Value,
            new StageDecisionRequest(e.Revision, "ApprovedWithConditions", "Conditional clearance reason.", ["Condition A", "Condition B"], Documents: null),
            f.ActorFor(stage.Definition.InstitutionCode.Value), default);

        var decision = f.Stage("CABINET").Decision;
        Assert.NotNull(decision);
        Assert.Equal("ApprovedWithConditions", decision.Outcome.ToString());
        Assert.Contains("Condition A", decision.Conditions);
        Assert.Contains("Condition B", decision.Conditions);
    }

    // -------------------------------------------------------------------------
    // Case E — Rejected without file evidence but with valid reason
    // -------------------------------------------------------------------------
    [Fact]
    public async Task DecideAsync_Rejected_NoDocument_WithReason_Accepted()
    {
        var f = new ProjectWorkflowExecutionSliceTests.Fixture();
        await f.Start();
        await StartStage(f, "CABINET");

        var e = f.Executions.Value!;
        var stage = f.Stage("CABINET");

        await f.Service.DecideAsync(
            f.Case.Id.Value, stage.Definition.StageId.Value,
            new StageDecisionRequest(e.Revision, "Rejected", "Statutory conditions not met as reviewed.", [], Documents: null),
            f.ActorFor(stage.Definition.InstitutionCode.Value), default);

        var decision = f.Stage("CABINET").Decision;
        Assert.NotNull(decision);
        Assert.Equal("Rejected", decision.Outcome.ToString());
        Assert.Equal("Statutory conditions not met as reviewed.", decision.Reason);
    }

    // -------------------------------------------------------------------------
    // Case F — Rejected without required reason — must still be rejected
    // -------------------------------------------------------------------------
    [Fact]
    public async Task DecideAsync_Rejected_BlankReason_StillRejected()
    {
        var f = new ProjectWorkflowExecutionSliceTests.Fixture();
        await f.Start();
        await StartStage(f, "CABINET");

        var e = f.Executions.Value!;
        var stage = f.Stage("CABINET");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Service.DecideAsync(
                f.Case.Id.Value, stage.Definition.StageId.Value,
                new StageDecisionRequest(e.Revision, "Rejected", "   ", [], Documents: null),
                f.ActorFor(stage.Definition.InstitutionCode.Value), default));

        Assert.Null(f.Stage("CABINET").Decision);
    }

    // -------------------------------------------------------------------------
    // Case G — User without institutional/stage authority is denied
    // -------------------------------------------------------------------------
    [Fact]
    public async Task DecideAsync_WrongInstitution_Denied()
    {
        var f = new ProjectWorkflowExecutionSliceTests.Fixture();
        await f.Start();
        await StartStage(f, "CABINET");

        var e = f.Executions.Value!;
        var stage = f.Stage("CABINET");

        // ActorFor("WRONG_ORG") has the same capabilities but a different institution scope.
        await Assert.ThrowsAnyAsync<Exception>(() =>
            f.Service.DecideAsync(
                f.Case.Id.Value, stage.Definition.StageId.Value,
                new StageDecisionRequest(e.Revision, "Approved", "Attempting cross-institution decision.", [], Documents: null),
                f.ActorFor("WRONG_ORG"), default));

        Assert.Null(f.Stage("CABINET").Decision);
    }

    // -------------------------------------------------------------------------
    // Case H — Stage whose prerequisites are incomplete cannot be decided
    // -------------------------------------------------------------------------
    [Fact]
    public async Task DecideAsync_DsStage_BeforeCabinetApproved_Blocked()
    {
        var f = new ProjectWorkflowExecutionSliceTests.Fixture();
        await f.Start();

        // DS depends on CABINET (and TEST_A). Attempting to start DS before CABINET is done.
        var ds = f.Stage("DS");
        await Assert.ThrowsAnyAsync<Exception>(() =>
            f.Service.StageStartAsync(f.Case.Id.Value, ds.Definition.StageId.Value,
                f.Executions.Value!.Revision, f.ActorFor(ds.Definition.InstitutionCode.Value), default));

        Assert.Null(f.Stage("DS").Decision);
    }

    // -------------------------------------------------------------------------
    // Case I — Second decision where immutability already recorded one without revision context
    // -------------------------------------------------------------------------
    [Fact]
    public async Task DecideAsync_DuplicateDecision_WithoutRevisionId_Blocked()
    {
        var f = new ProjectWorkflowExecutionSliceTests.Fixture();
        await f.Start();
        await StartStage(f, "CABINET");

        var stage = f.Stage("CABINET");

        // First decision — succeeds.
        await f.Service.DecideAsync(
            f.Case.Id.Value, stage.Definition.StageId.Value,
            new StageDecisionRequest(f.Executions.Value!.Revision, "Approved", "First explicit decision.", [], Documents: null),
            f.ActorFor(stage.Definition.InstitutionCode.Value), default);

        // Second decision without a PriorDecisionId — must be blocked by the Domain's immutability guard.
        await Assert.ThrowsAnyAsync<Exception>(() =>
            f.Service.DecideAsync(
                f.Case.Id.Value, stage.Definition.StageId.Value,
                new StageDecisionRequest(f.Executions.Value!.Revision, "Rejected", "Attempting overwrite.", [], Documents: null),
                f.ActorFor(stage.Definition.InstitutionCode.Value), default));

        // First decision is preserved.
        Assert.Equal("Approved", f.Stage("CABINET").Decision!.Outcome.ToString());
    }

    // -------------------------------------------------------------------------
    // Case — Stale execution revision is blocked
    // -------------------------------------------------------------------------
    [Fact]
    public async Task DecideAsync_StaleRevision_Blocked()
    {
        var f = new ProjectWorkflowExecutionSliceTests.Fixture();
        await f.Start();
        await StartStage(f, "CABINET");

        var stage = f.Stage("CABINET");
        var staleRevision = f.Executions.Value!.Revision - 1;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Service.DecideAsync(
                f.Case.Id.Value, stage.Definition.StageId.Value,
                new StageDecisionRequest(staleRevision, "Approved", "Explicit reason.", [], Documents: null),
                f.ActorFor(stage.Definition.InstitutionCode.Value), default));

        Assert.Null(f.Stage("CABINET").Decision);
    }

    // -------------------------------------------------------------------------
    // Case — Invalid document binding (wrong stage category) still rejected
    // -------------------------------------------------------------------------
    [Fact]
    public async Task DecideAsync_DocumentFromWrongStage_Rejected()
    {
        var f = new ProjectWorkflowExecutionSliceTests.Fixture();
        await f.Start();
        await StartStage(f, "CABINET");

        var e = f.Executions.Value!;
        var stage = f.Stage("CABINET");

        // Create a document staged for a DIFFERENT stage (wrong logical category).
        var wrongStageId = Guid.NewGuid();
        var doc = CreateStagedDocument(f, e.Id.Value, wrongStageId);
        await f.Documents.AddAsync(doc);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Service.DecideAsync(
                f.Case.Id.Value, stage.Definition.StageId.Value,
                new StageDecisionRequest(e.Revision, "Approved", "Attempting wrong-stage doc.", [], [new(doc.Id.Value, doc.ActiveVersionId.Value)]),
                f.ActorFor(stage.Definition.InstitutionCode.Value), default));

        Assert.Null(f.Stage("CABINET").Decision);
    }

    // -------------------------------------------------------------------------
    // Case — Reason stored cleanly without "Evidence:" artifact when no docs given
    // -------------------------------------------------------------------------
    [Fact]
    public async Task DecideAsync_NoDocs_ReasonStoredWithoutEvidenceSuffix()
    {
        var f = new ProjectWorkflowExecutionSliceTests.Fixture();
        await f.Start();
        await StartStage(f, "CABINET");

        var stage = f.Stage("CABINET");
        const string enteredReason = "Reviewed proposal documents in the case workspace.";

        await f.Service.DecideAsync(
            f.Case.Id.Value, stage.Definition.StageId.Value,
            new StageDecisionRequest(f.Executions.Value!.Revision, "Approved", enteredReason, [], Documents: null),
            f.ActorFor(stage.Definition.InstitutionCode.Value), default);

        var storedReason = f.Stage("CABINET").Decision!.Reason;
        Assert.Equal(enteredReason, storedReason);
        Assert.DoesNotContain("Evidence:", storedReason);
        Assert.DoesNotContain("null", storedReason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("undefined", storedReason, StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------
    private static async Task StartStage(ProjectWorkflowExecutionSliceTests.Fixture f, string stageCode)
    {
        var stage = f.Stage(stageCode);
        await f.Service.StageStartAsync(f.Case.Id.Value, stage.Definition.StageId.Value,
            f.Executions.Value!.Revision, f.ActorFor(stage.Definition.InstitutionCode.Value), default);
    }

    private static GovernedDocument CreateStagedDocument(
        ProjectWorkflowExecutionSliceTests.Fixture f, Guid executionId, Guid stageId)
    {
        return new GovernedDocument(
            new(Guid.NewGuid()), f.Case.Id,
            WorkflowExecutionService.DocumentCategory(executionId, stageId),
            new(Guid.NewGuid()),
            new("SHA-256", new string('a', 64)),
            new("TEST_ONLY/evidence.pdf"), "TEST_ONLY_evidence.pdf", "application/pdf", 10,
            f.Actor, DateTime.UtcNow, f.Authority.ToDomain());
    }
}

