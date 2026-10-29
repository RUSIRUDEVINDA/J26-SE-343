using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
using StateLandGovernance.GovernanceIntelligence.Application.ComplaintClassification;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
using StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;

static class ComplaintLocationProof{
 public static async Task RunAsync(string restrictedConnection){
  var checks=new List<string>();void Check(bool value,string name){if(!value)throw new Exception(name);checks.Add(name);}
  var peer=new UnavailablePeer();var classifier=new UnavailableClassifier();var ownerId=Guid.NewGuid();
  async Task<WebApplication> Host(){var b=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName="Development"});b.Logging.ClearProviders();b.WebHost.UseUrls("http://127.0.0.1:0");b.Configuration["GovernanceIntelligence:Complaints:Enabled"]="true";b.Configuration["GovernanceIntelligence:Complaints:WritesEnabled"]="true";
   b.Services.AddDbContext<GovernanceIntelligenceDbContext>(o=>o.UseNpgsql(restrictedConnection));b.Services.AddScoped<IComplaintClassificationAssessmentStore,PostgresComplaintClassificationAssessmentStore>();b.Services.AddScoped<IRegisteredComplaintStore,RegisteredComplaintStore>();b.Services.AddSingleton<IComplaintReferenceAccess>(peer);b.Services.AddSingleton<IComplaintClassificationClient>(classifier);b.Services.AddSingleton(TimeProvider.System);b.Services.AddScoped<RegisteredComplaintService>();
   b.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions,Auth>("test",_=>{});b.Services.AddAuthorization();b.Services.AddControllers().AddApplicationPart(typeof(RegisteredComplaintsController).Assembly);var app=b.Build();app.UseAuthentication();app.UseAuthorization();app.MapControllers();await app.StartAsync();return app;}
  HttpClient Client(WebApplication h,Guid? actor,string role="Applicant"){var c=new HttpClient{BaseAddress=new Uri(h.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single())};if(actor!=null){c.DefaultRequestHeaders.Add("X-Test-Actor",actor.ToString());c.DefaultRequestHeaders.Add("X-Test-Role",role);}return c;}
  const string route="/api/governance-intelligence/registered-complaints";var input=new SubmitRegisteredComplaint(Guid.NewGuid(),"Fictional isolated location concern","Fictional English complaint used only to verify isolated address persistence.",null,null,"Village road, near the school\nMahakumbukkadawala");Guid savedId;
  await using(var h=await Host()){
   using var owner=Client(h,ownerId);using var other=Client(h,Guid.NewGuid());using var ds=Client(h,Guid.NewGuid(),"Officer");using var anonymous=Client(h,null);
   Check((await anonymous.PostAsJsonAsync(route,input)).StatusCode==HttpStatusCode.Unauthorized,"Anonymous complaint submission denied");Check((await ds.PostAsJsonAsync(route,input)).StatusCode==HttpStatusCode.Forbidden,"Officer cannot submit an applicant complaint");
   using var capability=JsonDocument.Parse(await owner.GetStringAsync(route+"/intake-capabilities"));Check(capability.RootElement.GetProperty("version").GetString()=="c4-complaint-intake/2"&&capability.RootElement.GetProperty("unverifiedLandLocation").GetBoolean(),"Existing address capability contract available");
   var response=await owner.PostAsJsonAsync(route,input);Check(response.StatusCode==HttpStatusCode.OK,"Free-text address accepted with absent parcel/case and unavailable peers");var saved=(await response.Content.ReadFromJsonAsync<RegisteredComplaint>())!;savedId=saved.Id;
   Check(saved.LandAddress==input.LandAddress&&saved.ApplicantId==ownerId&&saved.CaseId==null&&saved.ParcelId==null,"Original address and authenticated ownership persisted without trusted links");Check(saved.ClassificationState=="Failed"&&classifier.LastText==input.ComplaintText,"Unavailable classifier is recorded honestly; original complaint text is unchanged");
   Check((await (await owner.PostAsJsonAsync(route,input)).Content.ReadFromJsonAsync<RegisteredComplaint>())!.Id==saved.Id&&classifier.Calls==1,"Same-key replay preserves saved record and makes no extra classifier attempt");Check((await owner.PostAsJsonAsync(route,input with{LandAddress="A different location"})).StatusCode==HttpStatusCode.Conflict,"Changed address conflicts for same idempotency key");
   var unknown=input with{SubmissionKey=Guid.NewGuid(),ParcelId="unknown-optional-parcel",CaseReference="unknown-optional-application"};var second=(await (await owner.PostAsJsonAsync(route,unknown)).Content.ReadFromJsonAsync<RegisteredComplaint>())!;
   Check(second.UnverifiedParcelReference==unknown.ParcelId&&second.UnverifiedCaseReference==unknown.CaseReference&&second.ParcelId==null&&second.CaseId==null,"Unknown optional references remain unverified assertions without blocking submission");Check(peer.Calls==0,"No C1/C3 reference validation or lookup called");
   foreach(var bad in new[]{new string('a',2001),"invalid\0address"})Check((await owner.PostAsJsonAsync(route,input with{SubmissionKey=Guid.NewGuid(),LandAddress=bad})).StatusCode==HttpStatusCode.BadRequest,"Invalid address rejected before persistence/classifier");Check(classifier.Calls==2,"Rejected inputs never call the classifier adapter");
   Check((await other.GetAsync(route+"/"+savedId)).StatusCode==HttpStatusCode.NotFound,"Other applicant cannot read saved address/complaint");Check((await ds.GetAsync(route+"/"+savedId)).StatusCode==HttpStatusCode.NotFound,"Unassigned DS cannot read saved address/complaint");Check((await anonymous.GetAsync(route+"/"+savedId)).StatusCode==HttpStatusCode.Unauthorized,"Anonymous detail denied");
   Check((await owner.GetFromJsonAsync<RegisteredComplaint[]>(route))!.Count(x=>x.Id==savedId)==1,"Owner saved history contains the persisted complaint once");Check((await other.GetFromJsonAsync<RegisteredComplaint[]>(route))!.All(x=>x.Id!=savedId),"Other applicant list excludes saved complaint");await h.StopAsync();
  }
  await using(var h=await Host()){using var owner=Client(h,ownerId);var detail=(await owner.GetFromJsonAsync<RegisteredComplaintDetail>(route+"/"+savedId))!;Check(detail.Complaint.LandAddress==input.LandAddress&&detail.Complaint.ApplicantId==ownerId,"New host reload retains exact saved address and owner");Check(detail.Classifications.Count==0&&detail.Complaint.Attempts.Length==1,"Failed classification never fabricates probabilities/model output");Check(classifier.Calls==2,"Reload/list/detail perform no classification attempts");await h.StopAsync();}
  Console.WriteLine(JsonSerializer.Serialize(new{Status="Passed",Checks=checks,IsolatedComplaintId=savedId,ApplicantId=ownerId,TestAdapterCalls=classifier.Calls,RealModelCalls=0,PeerLookupCalls=peer.Calls,OrdinaryWrites=0},new JsonSerializerOptions{WriteIndented=true}));
 }
 sealed class UnavailablePeer:IComplaintReferenceAccess{public int Calls;public Task<ComplaintReference> ValidateAsync(string? c,string? p,CancellationToken ct){Calls++;throw new Exception("Peer lookup forbidden");}public Task<IReadOnlyList<Guid>> ReviewerCasesAsync(Guid actor,CancellationToken ct)=>Task.FromResult<IReadOnlyList<Guid>>([]);}
 sealed class UnavailableClassifier:IComplaintClassificationClient{public int Calls;public string? LastText;public Task<ComplaintClassificationResult> ClassifyAsync(string text,string? binding=null,CancellationToken ct=default){Calls++;LastText=text;return Task.FromResult(ComplaintClassificationResult.Failure(ComplaintClassificationErrorCode.ServiceUnavailable,"Deliberately unavailable isolated test adapter; no model called"));}}
}
