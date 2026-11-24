using System.Data.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
using StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;

namespace StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;

[ApiController, Authorize(Roles = "Officer,ComplianceOfficer,LandOfficer")]
[Route("api/governance-intelligence/regulatory-assessments/{id:guid}/follow-ups")]
public sealed class RegulatoryFollowupsController(IRegulatoryFollowupStore store, IHostEnvironment environment, IConfiguration config) : ControllerBase
{
    async Task<IActionResult> Execute(Func<Task<IActionResult>> action)
    {
        Response.Headers.CacheControl = "no-store";
        if (!environment.IsDevelopment() || !config.GetValue<bool>("GovernanceIntelligence:RegulatoryProvenance:Enabled"))
            return Problem(statusCode: 503, title: "Regulatory follow-up unavailable");
        try { return await action(); }
        catch (UnauthorizedAccessException) { return NotFound(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (RegulatoryFollowupConflictException) { return Conflict(new { message = "Revision or submission content changed. Refresh history; retain uncertain submission keys." }); }
        catch (ArgumentException e) { return BadRequest(new { message = e.Message }); }
        catch (Exception e) when (e is RegulatoryFollowupUnavailableException or IndependentRegulatoryReportUnavailableException or CentralComplaintUnavailableException or DbException)
        { return Problem(statusCode: 503, title: "Follow-up persistence or authorization unavailable", detail: "No screening was run. Check saved history before replaying an uncertain request."); }
    }
    [HttpGet] public Task<IActionResult> Get(Guid id, CancellationToken ct) => Execute(async () => Ok(await store.ReadAsync(id, ct)));
    [HttpGet("previous")]
    public Task<IActionResult> Previous(Guid id, [FromQuery] string? cursor = null, [FromQuery] int limit = 25, CancellationToken ct = default)
        => Execute(async () => Ok(await store.PreviousAsync(id, cursor, limit, ct)));
    [HttpPost, RequestSizeLimit(32768)] public Task<IActionResult> Post(Guid id, RecordRegulatoryFollowup input, CancellationToken ct)
        => Execute(async () => Ok(await store.RecordAsync(id, input, ct)));
}
