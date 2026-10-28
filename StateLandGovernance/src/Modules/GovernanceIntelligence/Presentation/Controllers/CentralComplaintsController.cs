using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
namespace StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;

[ApiController, Authorize(Roles = "Officer")]
[Route("api/governance-intelligence/central-complaints")]
public sealed class CentralComplaintsController(CentralComplaintService service, ComplaintRoutingPolicy policy,
    IComplaintRoutingStore routing, IComplaintActorContext actor, IComplaintHandlingStore? handling = null) : ControllerBase
{
    private async Task<IActionResult> Run(Func<Task<object>> action)
    {
        Response.Headers.CacheControl = "no-store";
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        if (actor.ActorId == Guid.Empty) return Forbid();
        try { return Ok(await action()); }
        catch (CentralComplaintUnavailableException) { return Problem(statusCode: 503, title: "Central complaint access is not available"); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ComplaintConcurrencyException) { return Problem(statusCode: 409, title: "Assignment changed or submission conflicts", detail: "Refresh the current assignment before trying a new action."); }
        catch (ArgumentException e) { return Problem(statusCode: 400, title: "Invalid assignment request", detail: e.Message); }
    }
    [HttpGet("capabilities")]
    public Task<IActionResult> Capabilities(CancellationToken ct) => Run(async () =>
    {
        var grant = await policy.RequireCentralAsync(actor.ActorId, false, ct);
        return new { CanRead = true, CanAssign = policy.WritesEnabled && grant.Permissions.Contains(ComplaintRoutingPolicy.Assign), CanReview=policy.WritesEnabled&&grant.Permissions.Contains(ComplaintRoutingPolicy.Review), ReviewOperations=grant.ReviewOperations };
    });
    [HttpGet] public Task<IActionResult> List([FromQuery] string? assignment = null, [FromQuery] string? cursor = null, CancellationToken ct = default, [FromQuery(Name="handling")] string? handlingFilter = null)
        => Run(async () => await service.ListAsync(assignment, cursor, ct, handlingFilter));
    [HttpGet("officers")] public Task<IActionResult> Officers(CancellationToken ct)
        => Run(async () => (await policy.DirectoryAsync(actor.ActorId, ct)).Officers);
    [HttpGet("{id:guid}")] public Task<IActionResult> Detail(Guid id, CancellationToken ct)
        => Run(async () => await service.DetailAsync(id, ct));
    [HttpGet("{id:guid}/assignments")] public Task<IActionResult> History(Guid id, CancellationToken ct)
        => Run(async () => await service.HistoryAsync(id, ct));
    [HttpPost("{id:guid}/assignments")] public Task<IActionResult> Assign(Guid id, AssignComplaint input, CancellationToken ct)
        => Run(async () => await routing.AssignAsync(id, input, ct));
    [HttpGet("{id:guid}/actions")] public Task<IActionResult> Actions(Guid id,CancellationToken ct)
        =>Run(async()=>await (handling??throw new CentralComplaintUnavailableException()).ViewAsync(id,true,true,ct));
    [HttpPost("{id:guid}/actions")] public Task<IActionResult> Act(Guid id,ComplaintHandlingAction input,CancellationToken ct)
        =>Run(async()=>await (handling??throw new CentralComplaintUnavailableException()).ActAsync(id,input,true,ct));

}
