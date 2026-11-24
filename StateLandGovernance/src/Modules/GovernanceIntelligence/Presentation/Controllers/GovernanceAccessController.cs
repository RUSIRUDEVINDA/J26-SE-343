using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
namespace StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;
[ApiController,Authorize,Route("api/governance-intelligence/access")]
public sealed class GovernanceAccessController(IComplaintOfficerDirectory directory):ControllerBase {
 [HttpGet]public async Task<IActionResult> Get(CancellationToken ct){
  Response.Headers.CacheControl="no-store";
  if(User.Identity?.IsAuthenticated!=true)return Unauthorized();
  if(User.IsInRole("Applicant"))return StatusCode(403,new{allowed=false,applicant=true,home="/workflow-governance/applicant/cases"});
  if(!(User.IsInRole("Officer")||User.IsInRole("ComplianceOfficer")||User.IsInRole("LandOfficer"))||!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub"),out var actor))return Forbid();
  try{return Ok(new{allowed=true,applicant=false,isCommissioner=await directory.IsCommissionerAsync(actor,ct)});}
  catch(CentralComplaintUnavailableException){return Problem(statusCode:503,title:"Current governance profile unavailable");}
 }
}
