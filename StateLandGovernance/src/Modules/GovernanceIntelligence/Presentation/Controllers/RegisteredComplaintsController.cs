using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
namespace StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;
[ApiController,Authorize(Roles="Applicant,Officer,ComplianceOfficer,LandOfficer")]
[Route("api/governance-intelligence/registered-complaints")]
public sealed class RegisteredComplaintsController(RegisteredComplaintService service,IConfiguration configuration,IComplaintHandlingStore? handling=null):ControllerBase
{
    private Guid Actor=>Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub"),out var id)?id:Guid.Empty;
    private bool Officer=>User.IsInRole("Officer")||User.IsInRole("ComplianceOfficer")||User.IsInRole("LandOfficer");
    private bool Enabled=>configuration.GetValue<bool>("GovernanceIntelligence:Complaints:Enabled");
    private bool Writes=>configuration.GetValue<bool>("GovernanceIntelligence:Complaints:WritesEnabled");
    private async Task<IActionResult> Run(Func<Task<object>> operation,bool write=false){
        if(User.Identity?.IsAuthenticated!=true)return Unauthorized();if(Actor==Guid.Empty)return Forbid();
        if(!Enabled||(write&&!Writes))return Problem(statusCode:503,title:"Complaint registration is not activated",detail:"The complaint lifecycle schema and write scope require separate operator authorization.");
        Response.Headers.CacheControl="no-store";
        try{return Ok(await operation());}
        catch(CentralComplaintUnavailableException){return Problem(statusCode:503,title:"Complaint handling is unavailable");}
        catch(UnauthorizedAccessException){return Forbid();}catch(KeyNotFoundException){return NotFound();}
        catch(ArgumentException e){return Problem(statusCode:400,title:"Invalid complaint request",detail:e.Message);}
        catch(ComplaintConcurrencyException){return Problem(statusCode:409,title:"Complaint changed or request conflicts",detail:"Refresh the saved complaint. A classification may already be running; do not resubmit as a new complaint.");}
    }
    [HttpGet] public Task<IActionResult> List(CancellationToken ct)=>Run(async()=>{
        var rows=await service.ListAsync(Actor,Officer,ct);
        if(handling==null)return rows;
        var result=new List<System.Text.Json.Nodes.JsonNode>();
        var options=new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        foreach(var row in rows){
            try{var view=await handling.ViewAsync(row.Id,false,Officer,ct);
                var node=System.Text.Json.JsonSerializer.SerializeToNode(row,options)!;
                node["handling"]=System.Text.Json.JsonSerializer.SerializeToNode(view.State,options);result.Add(node);
            }catch(KeyNotFoundException){}
        }
        return result;
    });
    [HttpGet("{id:guid}")]public Task<IActionResult> Detail(Guid id,CancellationToken ct)=>Run(async()=>await service.DetailAsync(Actor,Officer,id,ct));
    [HttpGet("intake-capabilities"),Authorize(Roles="Applicant")]
    public Task<IActionResult> IntakeCapabilities()=>Run(()=>Task.FromResult<object>(new{version="c4-complaint-intake/2",unverifiedLandLocation=true}));
    [HttpPost,Authorize(Roles="Applicant")]public Task<IActionResult> Submit(SubmitRegisteredComplaint request,CancellationToken ct)=>Run(async()=>await service.SubmitAsync(Actor,request,ct),true);
    public sealed record Retry(int Revision);
    [HttpPost("{id:guid}/classification-retries")]public Task<IActionResult> Classify(Guid id,Retry request,CancellationToken ct)=>Run(async()=>await service.ClassifyAsync(Actor,Officer,id,request.Revision,ct),true);
    public sealed record Review(int Revision,string Status,string Reason,int? ExpectedHandlingRevision=null);
    [HttpPost("{id:guid}/reviews"),Authorize(Roles="Officer,ComplianceOfficer,LandOfficer")]public Task<IActionResult> ReviewComplaint(Guid id,Review request,CancellationToken ct)=>Run(async()=>await service.ReviewAsync(Actor,id,request.Revision,request.Status,request.Reason,ct,request.ExpectedHandlingRevision),true);
    [HttpGet("{id:guid}/actions")]public Task<IActionResult> Actions(Guid id,CancellationToken ct)=>Run(async()=>await (handling??throw new CentralComplaintUnavailableException()).ViewAsync(id,false,Officer,ct));
    [HttpPost("{id:guid}/actions"),Authorize(Roles="Officer,ComplianceOfficer,LandOfficer")]public Task<IActionResult> Act(Guid id,ComplaintHandlingAction input,CancellationToken ct)=>Run(async()=>await (handling??throw new CentralComplaintUnavailableException()).ActAsync(id,input,false,ct),true);

}
