using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using StateLandGovernance.GovernanceIntelligence.Application.Demonstrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Data.Common;
using StateLandGovernance.GovernanceIntelligence.Application.WorkflowAnomaly;

namespace StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;

[ApiController, Authorize(Roles = "Officer,ComplianceOfficer,LandOfficer")]
[Route("api/governance-intelligence/post-workflow-demonstrations")]
public sealed class PostWorkflowDemonstrationsController(IPostWorkflowDemonstrationStore store,
    IHostEnvironment environment, IOptionsMonitor<PostWorkflowDemonstrationOptions> options,
    ILogger<PostWorkflowDemonstrationsController>? logger = null, IConfiguration? configuration = null) : ControllerBase
{
    private Guid Actor => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;
    private async Task<IActionResult> Execute(Func<Task<object>> action, bool write = false)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        if (Actor == Guid.Empty || !(User.IsInRole("Officer") || User.IsInRole("ComplianceOfficer") || User.IsInRole("LandOfficer"))) return Forbid();
        if (!environment.IsDevelopment() || !options.CurrentValue.Enabled || (write && !options.CurrentValue.WritesEnabled))
            return Problem(statusCode: 503, title: "Development post-workflow demonstration is not activated",
                detail: "The server requires Development mode and explicit post-workflow feature/write activation. No real C3 case prerequisites are required.");
        Response.Headers.CacheControl = "no-store";
        try { return Ok(await action()); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return Problem(statusCode: 404, title: "Saved post-workflow report not found"); }
        catch (ArgumentException) { return BadRequest("Unknown demonstration selection."); }
        catch (Exception e) when (e is DbException || e.InnerException is DbException)
        {
            logger?.LogError("Post-workflow persistence unavailable ({ExceptionType}); reference {TraceId}", e.GetType().Name, HttpContext.TraceIdentifier);
            var problem = new ProblemDetails { Status = 503, Title = "Post-workflow persistence unavailable",
                Detail = "The demonstration storage schema or runtime permissions are unavailable. A submission may already be reserved; refresh saved history before further action." };
            problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
            return StatusCode(503, problem);
        }
    }
    [HttpGet("catalogue")]
    public Task<IActionResult> Catalogue() => Execute(() => Task.FromResult<object>(PostWorkflowDemonstrations.Scenarios.Where(s => s != KiwulaCatalogue.WorkflowScenario || configuration?.GetValue<bool>("GovernanceIntelligence:KiwulaWorkspace:Enabled") == true).Select(s => new {
        scenario = s, name = s == KiwulaCatalogue.WorkflowScenario ? "Kiwula Kale example completed workflow" : s == "administrative" ? "Administrative workflow" : "Delays and revisions workflow",
        description = s == KiwulaCatalogue.WorkflowScenario ? "Example completed Kiwula Kale review history based on a proposed route; regional involvement remains unconfirmed. No official decisions or notification document." : s == "administrative" ? "Fictional administrative review sequence with a separate final notification." :
            "Fictional sequence containing extended gaps, repeated reviews and a decision revision; no model outcome is assumed.",
        canRun = options.CurrentValue.WritesEnabled && (s != KiwulaCatalogue.WorkflowScenario || configuration?.GetValue<bool>("GovernanceIntelligence:KiwulaWorkspace:WritesEnabled") == true), version = PostWorkflowDemonstrations.VersionFor(s),
        projection = PostWorkflowDemonstrations.Create(s, Guid.Parse("9b8bf7a5-f120-4e2f-9d02-e4945ed77546"),
            Guid.Parse("13817e68-7218-456c-b93f-9c27eb9c20f1"), new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.Zero))
    }).ToArray()));
    [HttpGet] public Task<IActionResult> List(CancellationToken ct) => Execute(async () => await store.ListAsync(Actor, ct));
    [HttpGet("{id:guid}")] public Task<IActionResult> Get(Guid id, CancellationToken ct) => Execute(async () => await store.GetAsync(Actor, id, ct));
    private Task<PostWorkflowReport> RunSelected(Selection request, bool retry, CancellationToken ct)
    {
        if (request.Scenario == KiwulaCatalogue.WorkflowScenario && (!environment.IsDevelopment() ||
            configuration?.GetValue<bool>("GovernanceIntelligence:KiwulaWorkspace:Enabled") != true ||
            configuration.GetValue<bool>("GovernanceIntelligence:KiwulaWorkspace:WritesEnabled") != true)) throw new UnauthorizedAccessException();
        return store.RunAsync(Actor, request.Scenario, retry, ct, request.ExpectedAttempt);
    }
    public sealed record Selection(string Scenario, int ExpectedAttempt = 0);
    [HttpPost] public Task<IActionResult> Run(Selection request, CancellationToken ct) => Execute(async () => await RunSelected(request, false, ct), true);
    [HttpPost("retries")] public Task<IActionResult> Retry(Selection request, CancellationToken ct) => Execute(async () => await RunSelected(request, true, ct), true);
}
