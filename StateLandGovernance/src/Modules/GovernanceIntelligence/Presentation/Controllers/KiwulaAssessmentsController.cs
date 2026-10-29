using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using StateLandGovernance.GovernanceIntelligence.Application.Demonstrations;
namespace StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;
[ApiController, Authorize(Roles = "Officer,ComplianceOfficer,LandOfficer")]
[Route("api/governance-intelligence/kiwula-assessments")]
public sealed class KiwulaAssessmentsController(IKiwulaAssessmentStore store, KiwulaAssessmentService service,
    IHostEnvironment environment, IConfiguration config) : ControllerBase
{
    private Guid Actor => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    private async Task<IActionResult> Execute(Func<Task<object>> action)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        if (Actor == Guid.Empty || !(User.IsInRole("Officer") || User.IsInRole("ComplianceOfficer") || User.IsInRole("LandOfficer"))) return Forbid();
        if (!environment.IsDevelopment() || !config.GetValue<bool>("GovernanceIntelligence:KiwulaWorkspace:Enabled")) return Problem(statusCode:503, title:"Kiwula workspace activation pending");
        Response.Headers.CacheControl = "no-store";
        try { return Ok(await action()); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException) { return BadRequest(); }
        catch (KiwulaSubmissionConflictException) { return Conflict(); }
        catch (Exception e) when (e is KiwulaUnavailableException or DbException || e.InnerException is DbException)
        { return Problem(statusCode:503, title:"Kiwula assessment storage unavailable", detail:"Activation or storage is unavailable. Refresh saved history before replaying the same submission key."); }
    }
    [HttpGet("catalogue")] public Task<IActionResult> Catalogue(CancellationToken ct) => Execute(async () => new { proposal = KiwulaCatalogue.Preview, canRun = await store.ReadyAsync(ct) });
    [HttpGet] public Task<IActionResult> List(CancellationToken ct) => Execute(async () => await store.ListAsync(Actor, ct));
    [HttpGet("{id:guid}")] public Task<IActionResult> Get(Guid id, CancellationToken ct) => Execute(async () => await store.GetAsync(Actor, id, ct));
    [HttpPost] public Task<IActionResult> Run(KiwulaRunRequest request, CancellationToken ct) => Execute(async () => await service.RunAsync(Actor, request, ct));
}
