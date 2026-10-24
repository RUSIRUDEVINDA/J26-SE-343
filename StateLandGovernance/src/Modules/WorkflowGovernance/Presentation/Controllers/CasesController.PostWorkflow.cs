namespace StateLandGovernance.WorkflowGovernance.Presentation.Controllers;

using Microsoft.AspNetCore.Mvc;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;

public sealed partial class CasesController
{
    [HttpGet("{caseId:guid}/post-workflow/notification-context")]
    public async Task<IActionResult> DsNotificationContext(Guid caseId, [FromServices] IWorkflowExecutionStore executions, CancellationToken ct)
    {
        if (InstitutionActor(caseId) is not { Institution: "DS" } actor || !actor.Authority.Capabilities.Contains("DSNotificationReviewer")) return StatusCode(403);
        var c = await _leaseCaseRepository.GetByIdAsync(new(caseId), ct);
        var e = await executions.GetAsync(caseId, ct);
        return Ok(new { executionId = e?.Id.Value, executionRevision = e?.Revision, finalDecisionId = c?.CompletedWorkflow?.FinalDecisionId.Value,
            decidedAtUtc = c?.CompletedWorkflow?.DecidedAtUtc, canRecord = e?.Status == Domain.WorkflowExecution.WorkflowExecutionStatus.Completed && c?.CompletedWorkflow != null });
    }
    [HttpGet("{caseId:guid}/post-workflow")]
    public async Task<IActionResult> ReadPostWorkflow(Guid caseId, [FromServices] PostWorkflowGovernanceService service, CancellationToken ct)
    { if (!IsOfficer()) return StatusCode(403); return Ok(await service.GetAsync(caseId, ct)); }

    [HttpPost("{caseId:guid}/post-workflow/ds-notification")]
    public async Task<IActionResult> RecordDsNotification(Guid caseId, RecordDsNotificationRequest request,
        [FromServices] PostWorkflowGovernanceService service, CancellationToken ct)
    {
        if (InstitutionActor(caseId) is not { } actor) return StatusCode(403);
        await service.RecordNotificationAsync(caseId, request, actor, ct); return Ok(new { status = "SourceEvidenceRecorded", detail = "This action records reviewed notification evidence; it sends no message." });
    }
    [HttpPost("{caseId:guid}/post-workflow/assess")]
    public async Task<IActionResult> AssessPostWorkflow(Guid caseId, PostWorkflowReviewRequest request,
        [FromServices] PostWorkflowGovernanceService service, CancellationToken ct)
    {
        if (!IsOfficer() || ExecutionAuthority(caseId, "PostWorkflowReviewer") is not { } actor) return StatusCode(403);
        await service.AssessAsync(caseId, request, actor, ct); return Ok(await service.GetAsync(caseId, ct));
    }
    [HttpPost("{caseId:guid}/post-workflow/follow-ups")]
    public async Task<IActionResult> AssignPostWorkflowFollowUp(Guid caseId, AssignPostWorkflowFollowUpRequest request,
        [FromServices] PostWorkflowGovernanceService service, CancellationToken ct)
    {
        if (!IsOfficer() || ExecutionAuthority(caseId, "PostWorkflowReviewer") is not { } actor) return StatusCode(403);
        await service.AssignFollowUpAsync(caseId, request, actor, ct); return Ok(await service.GetAsync(caseId, ct));
    }
    [HttpGet("{caseId:guid}/post-workflow/follow-ups/assigned")]
    public async Task<IActionResult> AssignedPostWorkflowFollowUps(Guid caseId, [FromServices] ICorrectionRepository records, CancellationToken ct)
    {
        if (InstitutionActor(caseId) is not { } actor) return StatusCode(403);
        return Ok((await records.ListAsync(caseId, ct)).Where(r => r.Purpose == "PostWorkflowFollowUp" && r.AssignedInstitution == actor.Institution)
            .Select(r => new { r.Id, r.Revision, r.Status, r.DueDateUtc, r.Instructions, r.FollowUpReviews }));
    }
    [HttpPost("{caseId:guid}/post-workflow/follow-ups/{followUpId:guid}/resolve")]
    public async Task<IActionResult> ResolvePostWorkflowFollowUp(Guid caseId, Guid followUpId, ResolvePostWorkflowFollowUpRequest request,
        [FromServices] PostWorkflowGovernanceService service, CancellationToken ct)
    {
        if (InstitutionActor(caseId) is not { } actor) return StatusCode(403);
        await service.ResolveFollowUpAsync(caseId, followUpId, request, actor, ct); return Ok(new { status = "ResolvedAfterHumanReview" });
    }
    [HttpPost("{caseId:guid}/post-workflow/finalize")]
    public async Task<IActionResult> FinalizePostWorkflow(Guid caseId, PostWorkflowReviewRequest request,
        [FromServices] PostWorkflowGovernanceService service, CancellationToken ct)
    {
        if (!IsOfficer() || ExecutionAuthority(caseId, "GovernanceRecordFinalizer") is not { } actor) return StatusCode(403);
        await service.FinalizeAsync(caseId, request, actor, ct); return Ok(await service.GetAsync(caseId, ct));
    }
}
