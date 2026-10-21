using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StateLandGovernance.Api.Security;
namespace StateLandGovernance.Api.Controllers;
[ApiController, Route("api/development-auth")]
public sealed class DevelopmentAuthController(DevelopmentAccounts accounts) : ControllerBase
{
    public sealed record Credentials(string Username,string Password);
    [HttpGet("screening-scenarios")]
    public IActionResult ScreeningScenarios([FromQuery]Guid caseId,[FromServices]IConfiguration configuration)
    {
        if(!accounts.Local(HttpContext))return NotFound();
        if(User.Identity?.IsAuthenticated!=true)return Unauthorized();
        if(!User.HasClaim("authority_case_id",caseId.ToString("D")) || !User.HasClaim("capability","CaseWorkspaceReviewer"))return StatusCode(403);
        var scenarios=configuration.GetSection("LocalDevelopmentSignIn:ScreeningScenarios");
        var item=scenarios.GetSection("cases").GetChildren().SingleOrDefault(c=>c["caseId"]==caseId.ToString("D"));
        return Ok(new { available=item!=null, label=item?["label"],spatialParcelId=item?["spatialParcelId"],
            syntheticEvidenceParcelId=item?["syntheticEvidenceParcelId"], noConflictSpatialParcelId=item?["noConflictSpatialParcelId"],
            noConflictSyntheticEvidenceParcelId=item?["noConflictSyntheticEvidenceParcelId"],researchCaseId=scenarios["researchCaseId"],
            researchAccount=scenarios["researchAccount"],researchLabel=scenarios["researchLabel"] });
    }
    [HttpGet("cases/{caseId:guid}/screening-scenarios")]
    public async Task<IActionResult> UploadedScenarios(Guid caseId,[FromServices] UploadedCaseResearchScenarios scenarios,CancellationToken ct)
    {
        if(!scenarios.Local)return NotFound();
        if(User.Identity?.IsAuthenticated!=true)return Unauthorized();
        if(!await scenarios.AssignedAsync(caseId,ct:ct))return StatusCode(403);
        var designation=await scenarios.ReadAsync(caseId,ct);
        return Ok(new { available=designation!=null,canDesignate=true,designation,
            label="Development mock 12-acre overlap inputs (synthetic, not actual clearance)",
            spatialParcelId="de4c0000-0000-4000-8000-000000000031",syntheticEvidenceParcelId="de4c0000-0000-4000-8000-000000000032",
            noConflictSpatialParcelId="de4c0000-0000-4000-8000-000000000033",noConflictSyntheticEvidenceParcelId="de4c0000-0000-4000-8000-000000000034" });
    }
    public sealed record DesignateSynthetic(Guid DocumentId,Guid VersionId,string Reason,bool ConfirmSynthetic);
    [HttpPost("cases/{caseId:guid}/screening-scenarios/designation")]
    public async Task<IActionResult> Designate(Guid caseId,DesignateSynthetic request,[FromServices] UploadedCaseResearchScenarios scenarios,CancellationToken ct)
    {
        if(!scenarios.Local)return NotFound();
        if(User.Identity?.IsAuthenticated!=true)return Unauthorized();
        if(!request.ConfirmSynthetic)return BadRequest(new {error="Explicit synthetic-source confirmation is required."});
        try{return Ok(await scenarios.DesignateAsync(caseId,request.DocumentId,request.VersionId,request.Reason,ct));}
        catch(UnauthorizedAccessException ex){return StatusCode(403,new {error=ex.Message});}
        catch(ArgumentException ex){return BadRequest(new {error=ex.Message});}
        catch(InvalidOperationException ex){return Conflict(new {error=ex.Message});}
    }
    [HttpGet("status"),AllowAnonymous] public IActionResult Status() => Ok(new { enabled=accounts.Local(HttpContext),developmentOnly=true });
    [HttpPost("sign-in"),AllowAnonymous] public IActionResult SignIn(Credentials request)
    {
        if(!accounts.Local(HttpContext))return NotFound();
        Response.Headers.CacheControl="no-store";
        var result=accounts.SignIn(request.Username??"",request.Password??"");
        return result is null ? Unauthorized(new ProblemDetails { Status=401,Title="Sign-in failed",Detail="Incorrect local test username or password." }) : Ok(result);
    }
    [HttpGet("me")] public IActionResult Me()
    {
        if(!accounts.Local(HttpContext))return NotFound();
        if(User.Identity?.IsAuthenticated!=true || User.FindFirst("development_account")?.Value is not {} username)return Unauthorized();
        var a=accounts.Account(username);Response.Headers.CacheControl="no-store";
        return a is null ? Unauthorized() : Ok(accounts.View(a));
    }
    [HttpPost("sign-out")] public IActionResult SignOutSession()
    {
        if(!accounts.Local(HttpContext))return NotFound();
        accounts.SignOut(User);return NoContent();
    }
}
