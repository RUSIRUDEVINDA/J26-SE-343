using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using StateLandGovernance.GovernanceIntelligence.Application.OfficerFlows;
using StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;
namespace StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;
[ApiController,Authorize(Roles="Officer"),Route("api/governance-intelligence")]
public sealed class OfficerFlowsController(IOfficerFlowStore store,IHostEnvironment environment,IConfiguration configuration,ICommissionerReportReadAccess? reportReads=null):ControllerBase {
 Guid Actor=>Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var actor)?actor:Guid.Empty;
 bool IsDs()=>User.FindFirstValue("institution")=="DS"||configuration.GetSection("LocalDevelopmentSignIn:Accounts").GetChildren().Any(a=>a["Username"]==User.FindFirstValue("development_account")&&a["ActorId"]==Actor.ToString("D")&&a["TokenRole"]=="Officer"&&a["Institution"]=="DS");
 async Task<IActionResult> Execute(Func<Task<object>> work,bool savedReportRead=false,CancellationToken ct=default){
  if(User.Identity?.IsAuthenticated!=true)return Unauthorized();
  if(Actor==Guid.Empty||!User.IsInRole("Officer"))return Forbid();
  // Only saved GET/history can use the separate explicit read policy. Drafts and
  // all commands retain the existing DS gate; stores still enforce Saved/owner
  // scope and recheck the live permission after asynchronous reads.
  if(!IsDs()&&!(savedReportRead&&reportReads!=null&&await reportReads.CanReadAllAsync(Actor,ct)))return Forbid();
  if(!environment.IsDevelopment()||!configuration.GetValue<bool>("GovernanceIntelligence:KiwulaWorkspace:Enabled"))return Problem(statusCode:503,title:"Officer flow is not enabled");
  Response.Headers.CacheControl="no-store";
  try{return Ok(await work());}catch(UnauthorizedAccessException){return Forbid();}catch(KeyNotFoundException){return NotFound();}catch(OfficerConflictException){return Conflict(new{message="The key belongs to different input, or another request is Pending/Unknown. Retrieve history; do not retry uncertain inference."});}catch(ArgumentException e){return BadRequest(new{message=e.Message});}catch(Exception e)when(e is OfficerUnavailableException or DbException||e.InnerException is DbException){return Problem(statusCode:503,title:"Officer flow persistence unavailable",detail:"Refresh saved history before any further submission; an earlier request may be reserved.");}
 }
 [HttpGet("officer-assessments/catalogue")]public Task<IActionResult> Catalogue(CancellationToken ct)=>Execute(async()=>new{scenario=NamedOfficerWorkflow.Scenario,version=NamedOfficerWorkflow.Version,canRun=await store.ReadyAsync(ct),projection=NamedOfficerWorkflow.Create(Guid.Parse("ce077388-843a-4bad-a578-7b98475adcd1"),Guid.Parse("37134e90-2134-4747-bc80-3db7d639358e"),DateTimeOffset.Parse("2026-10-22T00:00:00Z"))});
 [HttpPost("officer-investigations")]public Task<IActionResult> Save(SaveInvestigation input,CancellationToken ct)=>Execute(async()=>await store.SaveAsync(Actor,input,ct));
 [HttpGet("officer-investigations")]public Task<IActionResult> Investigations(CancellationToken ct)=>Execute(async()=>await store.InvestigationsAsync(Actor,ct));
 [HttpGet("officer-investigations/{id:guid}")]public Task<IActionResult> Investigation(Guid id,CancellationToken ct)=>Execute(async()=>await store.InvestigationAsync(Actor,id,ct));
 [HttpPost("officer-assessments/early")]public Task<IActionResult> Early(EarlyRequest input,CancellationToken ct)=>Execute(async()=>await store.EarlyAsync(Actor,input,ct));
 [HttpPost("officer-assessments/anomaly")]public Task<IActionResult> Anomaly(AnomalyRequest input,CancellationToken ct)=>Execute(async()=>await store.AnomalyAsync(Actor,input,ct));
 [HttpGet("officer-assessments/{id:guid}")]public Task<IActionResult> Get(Guid id,CancellationToken ct)=>Execute(async()=>await store.GetAsync(Actor,id,ct),savedReportRead:true,ct:ct);
 [HttpGet("officer-assessments")]public Task<IActionResult> History(CancellationToken ct)=>Execute(async()=>await store.HistoryAsync(Actor,ct),savedReportRead:true,ct:ct);
}
