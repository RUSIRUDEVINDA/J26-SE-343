using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
using StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;
namespace StateLandGovernance.UnitTests.GovernanceIntelligence;
public sealed class GovernanceAccessTests {
 sealed class Directory:IComplaintOfficerDirectory{public int Calls;public bool Unavailable;public Task<bool> IsCommissionerAsync(Guid actor,CancellationToken ct){Calls++;if(Unavailable)throw new CentralComplaintUnavailableException();return Task.FromResult(true);}public Task<ComplaintDirectorySnapshot> ReadAsync(CancellationToken ct)=>throw new NotImplementedException();}
 static GovernanceAccessController Controller(Directory directory,string? role){var c=new GovernanceAccessController(directory){ControllerContext=new(){HttpContext=new DefaultHttpContext()}};if(role!=null)c.HttpContext.User=new(new ClaimsIdentity([new(ClaimTypes.Role,role),new(ClaimTypes.NameIdentifier,Guid.NewGuid().ToString())],"authenticated"));return c;}
 [Fact]public async Task ApplicantDeniedBeforeDirectoryOrGovernanceData(){var d=new Directory();var result=Assert.IsType<ObjectResult>(await Controller(d,"Applicant").Get(default));Assert.Equal(403,result.StatusCode);Assert.Equal(0,d.Calls);}
 [Fact]public async Task AnonymousDenied(){Assert.IsType<UnauthorizedResult>(await Controller(new(),null).Get(default));}
 [Fact]public async Task CurrentOfficerAdmissionUsesServerProfile(){var d=new Directory();Assert.IsType<OkObjectResult>(await Controller(d,"Officer").Get(default));Assert.Equal(1,d.Calls);}
 [Fact]public async Task ProfileUnavailableFailsClosed(){var result=Assert.IsType<ObjectResult>(await Controller(new(){Unavailable=true},"Officer").Get(default));Assert.Equal(503,result.StatusCode);}
}
