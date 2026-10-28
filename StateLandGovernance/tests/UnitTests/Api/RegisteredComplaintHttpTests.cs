using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
using StateLandGovernance.GovernanceIntelligence.Application.ComplaintClassification;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;
using Xunit;
namespace StateLandGovernance.UnitTests.Api;
public class RegisteredComplaintHttpTests
{
 sealed class Memory:IRegisteredComplaintStore,IComplaintClassificationAssessmentStore,IComplaintClassificationClient,IComplaintReferenceAccess{
  public Guid Reviewer=Guid.NewGuid(),Case=Guid.NewGuid();public Dictionary<Guid,RegisteredComplaint> Rows=new();
  public Task<ComplaintReference> ValidateAsync(string? c,string? p,CancellationToken ct)=>Task.FromResult(new ComplaintReference(Case,p));
  public Task<IReadOnlyList<Guid>> ReviewerCasesAsync(Guid actor,CancellationToken ct)=>Task.FromResult<IReadOnlyList<Guid>>(actor==Reviewer?new[]{Case}:Array.Empty<Guid>());
  public Task<ComplaintClassificationResult> ClassifyAsync(string text,string? id=null,CancellationToken ct=default)=>Task.FromResult(ComplaintClassificationResult.Failure(ComplaintClassificationErrorCode.ServiceUnavailable,"Isolated HTTP test failure"));
  public Task<RegisteredComplaint?> GetAsync(Guid id,CancellationToken ct)=>Task.FromResult(Rows.GetValueOrDefault(id));
  public Task<RegisteredComplaint?> FindSubmissionAsync(Guid a,Guid k,CancellationToken ct)=>Task.FromResult(Rows.Values.SingleOrDefault(x=>x.ApplicantId==a&&x.SubmissionKey==k));
  public Task<IReadOnlyList<RegisteredComplaint>> ListAsync(Guid? a,IReadOnlyList<Guid> cases,CancellationToken ct)=>Task.FromResult<IReadOnlyList<RegisteredComplaint>>(Rows.Values.Where(x=>a!=null?x.ApplicantId==a:x.CaseId!=null&&cases.Contains(x.CaseId.Value)).ToArray());
  public Task<bool> InsertAsync(RegisteredComplaint c,CancellationToken ct){Rows.Add(c.Id,c);return Task.FromResult(true);}
  public Task SaveAsync(RegisteredComplaint c,int revision,ComplaintClassificationAssessment? assessment,CancellationToken ct){if(Rows[c.Id].Revision!=revision)throw new ComplaintConcurrencyException();Rows[c.Id]=c;return Task.CompletedTask;}
  public Task<Guid?> ComplaintForAssessmentAsync(Guid id,CancellationToken ct)=>Task.FromResult<Guid?>(null);
  public Task AddAsync(ComplaintClassificationAssessment a,CancellationToken ct=default)=>Task.CompletedTask;
  public Task<ComplaintClassificationAssessment?> GetByIdAsync(Guid id,CancellationToken ct=default)=>Task.FromResult<ComplaintClassificationAssessment?>(null);
  public Task<IReadOnlyList<ComplaintClassificationAssessment>> GetByCaseIdAsync(string id,int limit=100,CancellationToken ct=default)=>Task.FromResult<IReadOnlyList<ComplaintClassificationAssessment>>(Array.Empty<ComplaintClassificationAssessment>());
 }
 [Fact]public async Task HttpLifecycle_RegistrationSurvivesFailure_OwnershipAndAssignedReviewEnforced(){
  var m=new Memory();var key=new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes("complaint-isolated-http-test-key-1234567890"));
  var b=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName="Testing",Args=Array.Empty<string>()});b.WebHost.UseUrls("http://127.0.0.1:0");
  b.Configuration["GovernanceIntelligence:Complaints:Enabled"]="true";b.Configuration["GovernanceIntelligence:Complaints:WritesEnabled"]="true";
  b.Services.AddAuthentication("Bearer").AddJwtBearer("Bearer",o=>o.TokenValidationParameters=new(){ValidateIssuer=false,ValidateAudience=false,ValidateLifetime=true,IssuerSigningKey=key,ValidateIssuerSigningKey=true});b.Services.AddAuthorization();
  b.Services.AddControllers().AddApplicationPart(typeof(RegisteredComplaintsController).Assembly);
  b.Services.AddSingleton<IRegisteredComplaintStore>(m);b.Services.AddSingleton<IComplaintClassificationClient>(m);b.Services.AddSingleton<IComplaintClassificationAssessmentStore>(m);b.Services.AddSingleton<IComplaintReferenceAccess>(m);b.Services.AddSingleton(TimeProvider.System);b.Services.AddSingleton<RegisteredComplaintService>();
  await using var app=b.Build();app.UseAuthentication();app.UseAuthorization();app.MapControllers();await app.StartAsync();
  try{
   using var client=new HttpClient{BaseAddress=new Uri(app.Urls.Single())};const string path="/api/governance-intelligence/registered-complaints";
   Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync(path)).StatusCode);
   void Login(Guid actor,string role){var token=new JwtSecurityToken(claims:new[]{new Claim(ClaimTypes.NameIdentifier,actor.ToString()),new Claim(ClaimTypes.Role,role)},expires:DateTime.UtcNow.AddMinutes(2),signingCredentials:new(key,SecurityAlgorithms.HmacSha256));client.DefaultRequestHeaders.Authorization=new("Bearer",new JwtSecurityTokenHandler().WriteToken(token));}
   var owner=Guid.NewGuid();Login(owner,"Applicant");var input=new SubmitRegisteredComplaint(Guid.NewGuid(),"Fictional concern","Original fictional text","known",null);
   var first=await client.PostAsJsonAsync(path,input);Assert.Equal(HttpStatusCode.OK,first.StatusCode);var saved=(await first.Content.ReadFromJsonAsync<RegisteredComplaint>())!;Assert.Equal(owner,saved.ApplicantId);Assert.Equal("Failed",saved.ClassificationState);
   var duplicate=await (await client.PostAsJsonAsync(path,input)).Content.ReadFromJsonAsync<RegisteredComplaint>();Assert.Equal(saved.Id,duplicate!.Id);Assert.Single(m.Rows);
   Login(Guid.NewGuid(),"Applicant");Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync(path+"/"+saved.Id)).StatusCode);
   Login(Guid.NewGuid(),"Officer");Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync(path+"/"+saved.Id)).StatusCode);
   Login(m.Reviewer,"Officer");Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync(path+"/"+saved.Id)).StatusCode);
   Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsJsonAsync(path+"/"+saved.Id+"/reviews",new{revision=saved.Revision,status="Verified",reason="No such power"})).StatusCode);
   Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsJsonAsync(path+"/"+saved.Id+"/reviews",new{revision=saved.Revision,status="UnderReview",reason="Reading allegation"})).StatusCode);
   Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsJsonAsync(path+"/"+saved.Id+"/reviews",new{revision=saved.Revision,status="ReviewRecorded",reason="Stale"})).StatusCode);
  }finally{await app.StopAsync();}
 }
}
