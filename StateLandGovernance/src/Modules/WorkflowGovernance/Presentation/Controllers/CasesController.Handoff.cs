namespace StateLandGovernance.WorkflowGovernance.Presentation.Controllers;

using Microsoft.AspNetCore.Mvc;
using StateLandGovernance.WorkflowGovernance.Application.Commands;

public sealed partial class CasesController
{
    [HttpGet("{caseId:guid}/handoff")]
    public async Task<IActionResult> GetGovernedHandoff(Guid caseId, [FromServices] GovernedHandoffService service, CancellationToken ct)
    {
        if (!IsOfficer()) return StatusCode(403);
        return Ok(await service.GetAsync(caseId, ct));
    }

    [HttpPost("{caseId:guid}/handoff/prepare")]
    public async Task<IActionResult> PrepareGovernedHandoff(Guid caseId, PrepareHandoffRequest request,
        [FromServices] GovernedHandoffService service, CancellationToken ct)
    {
        if (!IsOfficer() || ExecutionAuthority(caseId, "HandoffPreparer") is not { } actor) return StatusCode(403);
        return Ok(await service.PrepareAsync(caseId, request, actor, ct));
    }

    [HttpPost("{caseId:guid}/handoff/dispatch")]
    public async Task<IActionResult> DispatchGovernedHandoff(Guid caseId, DispatchHandoffRequest request,
        [FromServices] GovernedHandoffService service, CancellationToken ct)
    {
        if (!IsOfficer() || ExecutionAuthority(caseId, "HandoffDispatcher") is not { } actor) return StatusCode(403);
        return Ok(await service.DispatchAsync(caseId, request, actor, ct));
    }
}
