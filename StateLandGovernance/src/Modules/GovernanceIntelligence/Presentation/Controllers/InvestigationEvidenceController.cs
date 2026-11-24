using System.Data.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
using StateLandGovernance.GovernanceIntelligence.Application.OfficerFlows;
namespace StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;
[ApiController,Authorize(Roles="Officer"),Route("api/governance-intelligence/officer-investigations/{investigation:guid}/evidence")]
public sealed class InvestigationEvidenceController(IInvestigationEvidenceStore store,IHostEnvironment environment,IConfiguration configuration):ControllerBase {
 [HttpGet("/api/governance-intelligence/officer-investigation-evidence-capabilities")]
 public Task<IActionResult> Capabilities()=>Execute(()=>Task.FromResult<IActionResult>(Ok(new{version="investigation-evidence/1"})));
 async Task<IActionResult> Execute(Func<Task<IActionResult>> action){if(!environment.IsDevelopment()||!configuration.GetValue<bool>("GovernanceIntelligence:KiwulaWorkspace:Enabled"))return Problem(statusCode:503,title:"Manual screening evidence unavailable");Response.Headers.CacheControl="no-store";try{return await action();}catch(UnauthorizedAccessException){return NotFound();}catch(KeyNotFoundException){return NotFound();}catch(OfficerConflictException){return Conflict(new{message="Revision or submission content changed. Refresh history; uncertain writes retain their original key."});}catch(ArgumentException e){return BadRequest(new{message=e.Message});}catch(Exception e)when(e is OfficerUnavailableException or CentralComplaintUnavailableException or DbException){return Problem(statusCode:503,title:"Evidence persistence or authorization unavailable");}}
 [HttpGet]public Task<IActionResult> Get(Guid investigation,CancellationToken ct)=>Execute(async()=>Ok(await store.ReadAsync(investigation,ct)));
 [HttpPost("attachments"),RequestSizeLimit(6*1024*1024),RequestFormLimits(MultipartBodyLengthLimit=6*1024*1024)]
 public Task<IActionResult> Attach(Guid investigation,[FromForm]Guid submissionKey,[FromForm]string concern,[FromForm]IFormFile file,CancellationToken ct)=>Execute(async()=>{if(file==null||file.Length is <=0 or >ManualEvidenceInput.MaxBytes)return BadRequest(new{message="Attach one supported file up to5MiB."});await using var stream=file.OpenReadStream();using var memory=new MemoryStream();var buffer=new byte[8192];int read;while((read=await stream.ReadAsync(buffer,ct))>0){if(memory.Length+read>ManualEvidenceInput.MaxBytes)return BadRequest();await memory.WriteAsync(buffer.AsMemory(0,read),ct);}return Ok(await store.AttachAsync(investigation,new(submissionKey,concern,file.FileName,file.ContentType,memory.ToArray()),ct));});
 [HttpGet("attachments/{attachment:guid}")]public Task<IActionResult> Download(Guid investigation,Guid attachment,CancellationToken ct)=>Execute(async()=>{var result=await store.DownloadAsync(investigation,attachment,ct);Response.Headers["X-Content-Type-Options"]="nosniff";Response.Headers["Content-Security-Policy"]="sandbox";return File(result.Content,"application/octet-stream",result.Metadata.FileName);});
 [HttpPost("finalize")]public Task<IActionResult> Finalize(Guid investigation,FinalizeInvestigation input,CancellationToken ct)=>Execute(async()=>Ok(await store.FinalizeAsync(investigation,input,ct)));
}
