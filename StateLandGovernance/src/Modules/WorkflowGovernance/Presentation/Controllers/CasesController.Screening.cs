namespace StateLandGovernance.WorkflowGovernance.Presentation.Controllers;

using Microsoft.AspNetCore.Mvc;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;

public sealed partial class CasesController
{
    [HttpGet("{caseId:guid}/screening")]
    public async Task<IActionResult> GetCaseScreening(Guid caseId, [FromServices] CaseScreeningService screening,
        [FromQuery] Guid? snapshotId = null, [FromQuery] Guid? parcelId = null, CancellationToken cancellationToken = default)
    {
        if (!IsOfficer()) return StatusCode(403);
        return Ok(await screening.GetAsync(caseId, snapshotId, parcelId, cancellationToken));
    }

    [HttpPost("{caseId:guid}/screening")]
    public async Task<IActionResult> RequestCaseScreening(Guid caseId, [FromBody] StartCaseScreeningRequest request,
        [FromServices] CaseScreeningService screening, CancellationToken cancellationToken = default)
    {
        if (!IsOfficer()) return StatusCode(403);
        var result = await screening.RunAsync(caseId, request, ResolveActorId(), cancellationToken);
        return result.Status == "BlockedIncomplete"
            ? Conflict(new { status = 409, title = "Screening blocked / incomplete", detail = string.Join(" ", result.MissingInputs), screeningState = result })
            : Ok(result);
    }
}
