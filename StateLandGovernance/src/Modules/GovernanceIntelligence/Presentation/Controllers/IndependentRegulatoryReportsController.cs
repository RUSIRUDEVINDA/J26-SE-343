using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;
namespace StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;

[ApiController, Authorize(Roles = "Officer,ComplianceOfficer,LandOfficer")]
[Route("api/governance-intelligence/regulatory-assessments")]
public sealed class IndependentRegulatoryReportsController(IndependentRegulatoryReportQueries queries) : ControllerBase
{
    private Guid Actor => User.Identity?.IsAuthenticated == true &&
        (User.IsInRole("Officer") || User.IsInRole("ComplianceOfficer") || User.IsInRole("LandOfficer")) &&
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int limit = 30, CancellationToken ct = default)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        if (Actor == Guid.Empty) return Forbid();
        Response.Headers.CacheControl="no-store";
        try { return Ok(await queries.ListAsync(Actor, limit, ct)); }
        catch (ArgumentOutOfRangeException) { return Problem(statusCode: 400, title: "Limit must be between 1 and 100."); }
        catch (IndependentRegulatoryReportUnavailableException) { return Unavailable(); }
    }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct = default)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        if (Actor == Guid.Empty) return Forbid();
        Response.Headers.CacheControl="no-store";
        try
        {
            var report = await queries.GetAsync(Actor, id, ct);
            return report == null ? NotFound() : Ok(report);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (IndependentRegulatoryReportUnavailableException) { return Unavailable(); }
    }
    [HttpGet("capabilities")]
    public async Task<IActionResult> Capabilities([FromServices] IRegulatorySubmissionStore ownership,CancellationToken ct){
        if(User.Identity?.IsAuthenticated!=true)return Unauthorized();if(Actor==Guid.Empty)return Forbid();Response.Headers.CacheControl="no-store";
        return Ok(new{enabled=await ownership.CanSubmitAsync(ct),schemaVersion="c4-regulatory-input/1"});
    }
    [HttpGet("submissions/{key:guid}")]
    public async Task<IActionResult> BySubmission(Guid key,[FromServices] IRegulatorySubmissionStore ownership,CancellationToken ct){
        if(User.Identity?.IsAuthenticated!=true)return Unauthorized();if(Actor==Guid.Empty)return Forbid();Response.Headers.CacheControl="no-store";
        var id=await ownership.FindSubmissionAsync(Actor,key,ct);return id==null?NotFound():await Get(id.Value,ct);
    }
    private ObjectResult Unavailable()
    {
        var problem = new ProblemDetails { Status = 503, Title = "Stored report unavailable",
            Detail = "The saved findings could not be read. No engine evaluation was performed." };
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        return new ObjectResult(problem) { StatusCode = 503 };
    }
}
