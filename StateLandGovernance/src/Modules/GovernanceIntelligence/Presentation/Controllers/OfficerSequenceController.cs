using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using StateLandGovernance.GovernanceIntelligence.Application.OfficerFlows;
using StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;
namespace StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;
[ApiController,Authorize(Roles="Officer"),Route("api/governance-intelligence/officer-sequence")]
public sealed class OfficerSequenceController(IOfficerSequenceStore store,IHostEnvironment environment,IConfiguration config):ControllerBase {
 [HttpGet] public async Task<IActionResult> Get([FromQuery]Guid? earlyRequestId,[FromQuery]Guid? regulatoryEvaluationId,CancellationToken ct){
  Response.Headers.CacheControl="no-store";if(!environment.IsDevelopment()||!config.GetValue<bool>("GovernanceIntelligence:KiwulaWorkspace:Enabled"))return StatusCode(503);
  try{return Ok(await store.ReadAsync(earlyRequestId,regulatoryEvaluationId,ct));}catch(UnauthorizedAccessException){return NotFound();}catch(KeyNotFoundException){return NotFound();}catch(ArgumentException e){return BadRequest(new{message=e.Message});}catch(Exception e)when(e is System.Data.Common.DbException or OfficerUnavailableException or RegulatoryFollowupUnavailableException or IndependentRegulatoryReportUnavailableException or StateLandGovernance.GovernanceIntelligence.Application.Complaints.CentralComplaintUnavailableException){return Problem(statusCode:503,title:"Officer sequence unavailable");}
 }
}
