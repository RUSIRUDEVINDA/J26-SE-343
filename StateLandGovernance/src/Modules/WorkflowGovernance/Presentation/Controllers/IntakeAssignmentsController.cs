namespace StateLandGovernance.WorkflowGovernance.Presentation.Controllers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Commands;

[ApiController, Authorize, Route("api/workflow-governance/intake-assignments")]
public sealed class IntakeAssignmentsController(ICaseAssignmentStore store, CaseAssignmentService service,
    IIntakeAssignmentDirectory directory) : ControllerBase
{
    private Guid Actor => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;
    [HttpGet("jurisdictions")]
    public async Task<IActionResult> Jurisdictions(CancellationToken ct) => Ok((await store.PoliciesAsync(ct))
        .Where(x => x.EffectiveFrom <= DateTimeOffset.UtcNow && (x.EffectiveUntil == null || x.EffectiveUntil > DateTimeOffset.UtcNow))
        .Select(x => new { x.JurisdictionCode, x.Identifier, x.Version }).Distinct());
    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken ct) => User.IsInRole("Applicant") ? StatusCode(403) : Ok(await store.ListAsync(null, Actor, null, false, ct));
    [HttpGet("applicant")]
    public async Task<IActionResult> Applicant(CancellationToken ct)
    {
        if (!User.IsInRole("Applicant")) return StatusCode(403);
        return Ok((await store.ListAsync(Actor, null, null, false, ct)).Select(x => new {
            x.CaseId, x.ApplicationReference, x.Status, responsibleOffice = x.Assignment.OfficeCode,
            assignmentStatus = CaseAssignmentService.IsActive(x.Assignment.Active, DateTimeOffset.UtcNow) ? "Officer assigned" : "Awaiting intake assignment" }));
    }
    [HttpGet]
    public async Task<IActionResult> Queue(CancellationToken ct)
    {
        try
        {
            var scope = service.RequireCoordinator(Actor);
            return Ok(new { scope.OfficeCode, scope.CanManageUnassigned,
                reviewers = directory.Reviewers.Where(x => x.OfficeCode == scope.OfficeCode),
                cases = await store.ListAsync(null, null, scope.OfficeCode, scope.CanManageUnassigned, ct) });
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = ex.Message }); }
    }
    public sealed record ChangeAssignment(int ExpectedRevision, Guid? ReviewerId, string OfficeCode,
        Guid DocumentId, string Reason, DateTimeOffset ValidUntil);
    [HttpPost("{caseId:guid}")]
    public async Task<IActionResult> Change(Guid caseId, ChangeAssignment request, CancellationToken ct)
    {
        try { return Ok(await service.ChangeAsync(Actor, caseId, request.ExpectedRevision, request.ReviewerId,
            request.OfficeCode, request.DocumentId, request.Reason, request.ValidUntil, ct)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }
}
