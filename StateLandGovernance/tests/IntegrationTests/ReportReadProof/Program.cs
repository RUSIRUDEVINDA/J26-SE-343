using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Text.Json;
using StateLandGovernance.Api.Security;
using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
using StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Net;
using System.Net.Http.Json;
using StateLandGovernance.GovernanceIntelligence.Application.OfficerFlows;
using StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;
var input=Environment.GetEnvironmentVariable("C4_REPORT_READ_TEST_CONNECTION")??throw new Exception("Explicit isolated connection required");var c=new NpgsqlConnectionStringBuilder(input);
if(c.Host!="127.0.0.1"||c.Port!=57657||(c.Database!="c4_central_restore_preparation_20261023_053654"&&c.Database!="c4_step3_restore_20261024"))throw new Exception("Refusing non-isolated target");c.Options="-c default_transaction_read_only=on";
await using var db=new GovernanceIntelligenceDbContext(new DbContextOptionsBuilder<GovernanceIntelligenceDbContext>().UseNpgsql(c.ConnectionString).Options);
var actor=Guid.Parse("0ab669fe-d0ab-5695-ad55-10f6b3b5aab3");var foreign=Guid.NewGuid();var directory=new Directory();var checks=new List<string>();void Check(bool yes,string name){if(!yes)throw new Exception(name);checks.Add(name);}
var path=Path.Combine(Path.GetTempPath(),"c4-report-read-"+Guid.NewGuid().ToString("N")+".json");var grant=new CentralComplaintGrant(actor,Guid.NewGuid(),"COMMISSIONER",[CommissionerReportReadAccess.Permission],DateTimeOffset.UnixEpoch,null,"Isolated explicit report-read grant"){Persistent=true};
async Task Write(bool revoked)=>await File.WriteAllTextAsync(path,JsonSerializer.Serialize(new CentralComplaintGrantFile.Document("c4-central-permissions/1",[grant with{Revoked=revoked}])));
await Write(false);var options=new CommissionerReportReadOptions{Enabled=true,GrantFilePath=path};var policy=new CommissionerReportReadAccess(options,new CommissionerReportGrantFile(new Snapshot(options)),directory,TimeProvider.System);
var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"GovernanceIntelligence:RegulatoryProvenance:Enabled","true"}}).Build();
var regulatory=new IndependentRegulatoryReportStore(db);var ownership=new RegulatorySubmissionStore(db,regulatory,config);var queries=new IndependentRegulatoryReportQueries(regulatory,new DenyIndependentRegulatoryReadAssignments(),ownership,policy);
var projections=new PostgresComponent3WorkflowGovernanceProjectionStore(db);var anomalies=new PostgresWorkflowAnomalyAssessmentStore(db);var early=new PostgresEarlyGovernanceScreeningStore(db);
var kiwula=new KiwulaAssessmentStore(db,config,TimeProvider.System,policy);var workflow=new PostWorkflowDemonstrationStore(db,projections,anomalies,null!,TimeProvider.System,policy);var officer=new OfficerFlowStore(db,config,TimeProvider.System,projections,anomalies,early,null!,null!,policy);
var reports=new List<object>();
foreach(var row in await db.Set<RegulatoryEvaluationProvenanceEntity>().AsNoTracking().ToArrayAsync()){
 var r=await queries.GetAsync(actor,row.EvaluationId,default);Check(r?.Metadata.Id==row.EvaluationId&&r.Provenance!=null,"Commissioner reads foreign regulatory report with original provenance");reports.Add(new{kind="regulatory",id=row.EvaluationId});
}
foreach(var row in await db.Set<KiwulaAssessmentEntity>().AsNoTracking().ToArrayAsync()){
 var r=await kiwula.GetAsync(actor,row.Id,default);Check(r.CreatorActorId==row.CreatorActorId,"Kiwula "+row.Engine+" retains original creator");reports.Add(new{kind=row.Engine,id=row.Id});
 try{await kiwula.GetAsync(foreign,row.Id,default);throw new Exception("Foreign access");}catch(UnauthorizedAccessException){}
}
foreach(var row in await db.Set<PostWorkflowDemonstrationEntity>().AsNoTracking().Where(x=>x.State=="Saved").ToArrayAsync()){
 var r=await workflow.GetAsync(actor,row.Id,default);Check(r.CreatorActorId==row.CreatorActorId&&r.Assessment!=null,"Workflow report preserves creator and actual saved assessment");reports.Add(new{kind="workflow",id=row.Id});
}
foreach(var row in await db.Set<OfficerAssessmentRequestEntity>().AsNoTracking().Where(x=>x.State=="Saved").ToArrayAsync()){
 var r=await officer.GetAsync(actor,row.Id,default);Check(r.CreatorActorId==row.CreatorActorId,"Officer saved "+row.Kind+" preserves original creator");reports.Add(new{kind=row.Kind,id=row.Id});
}
Check((await queries.ListAsync(actor,100,default)).Count>0&&(await kiwula.ListAsync(actor,default)).Count>0&&(await workflow.ListAsync(actor,default)).Count>0&&(await officer.HistoryAsync(actor,default)).Count>0,"Cross-creator listings use actual saved records");
await Write(true);
var ownRegulatoryIds=await db.Set<RegulatoryEvaluationProvenanceEntity>().AsNoTracking().Where(x=>x.CreatorActorId==actor).Select(x=>x.EvaluationId).ToArrayAsync();
Check((await queries.ListAsync(actor,100,default)).All(x=>ownRegulatoryIds.Contains(x.Id))&&(await kiwula.ListAsync(actor,default)).All(x=>x.CreatorActorId==actor)&&(await workflow.ListAsync(actor,default)).All(x=>x.CreatorActorId==actor)&&(await officer.HistoryAsync(actor,default)).All(x=>x.CreatorActorId==actor),"Live report grant revocation removes every cross-creator listing while retaining legitimate own records");
await Write(false);directory.Active=false;Check(!await policy.CanReadAllAsync(actor,default),"Inactive profile denied");directory.Active=true;options.Enabled=false;Check(!await policy.CanReadAllAsync(actor,default),"Unactivated feature fails closed");
Check(await db.Database.SqlQueryRaw<string>("SELECT current_setting('transaction_read_only') AS \"Value\"").SingleAsync()=="on","All isolated repository checks enforced read-only");
// Authenticated HTTP regression for the boundary previously missed by repository tests.
// Loopback test-only authentication, never registered in the ordinary API.
options.Enabled=true;directory.Active=true;await Write(false);
var builder=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName="Development"});builder.WebHost.UseUrls("http://127.0.0.1:0");builder.Logging.ClearProviders();builder.Configuration["GovernanceIntelligence:KiwulaWorkspace:Enabled"]="true";
builder.Services.AddSingleton<StateLandGovernance.GovernanceIntelligence.Application.WorkflowAnomaly.IPostWorkflowDemonstrationStore>(workflow);builder.Services.Configure<StateLandGovernance.GovernanceIntelligence.Application.WorkflowAnomaly.PostWorkflowDemonstrationOptions>(o=>{o.Enabled=true;o.WritesEnabled=false;});
builder.Services.AddSingleton<IOfficerFlowStore>(officer);builder.Services.AddSingleton<ICommissionerReportReadAccess>(policy);
builder.Services.AddAuthentication("isolated").AddScheme<AuthenticationSchemeOptions,IsolatedAuthentication>("isolated",_=>{});builder.Services.AddAuthorization();builder.Services.AddControllers().AddApplicationPart(typeof(OfficerFlowsController).Assembly);
await using(var host=builder.Build()){
 host.UseAuthentication();host.UseAuthorization();host.MapControllers();await host.StartAsync();
 var url=host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();using var client=new HttpClient{BaseAddress=new Uri(url)};
 void Identity(Guid? who,string role="Officer",string institution="COMMISSIONER"){client.DefaultRequestHeaders.Remove("X-Isolated-Actor");client.DefaultRequestHeaders.Remove("X-Isolated-Role");client.DefaultRequestHeaders.Remove("X-Isolated-Institution");if(who!=null){client.DefaultRequestHeaders.Add("X-Isolated-Actor",who.ToString());client.DefaultRequestHeaders.Add("X-Isolated-Role",role);client.DefaultRequestHeaders.Add("X-Isolated-Institution",institution);}}
 async Task Expect(string route,HttpStatusCode status,string label){using var response=await client.GetAsync("/api/governance-intelligence/"+route);Check(response.StatusCode==status,label);}
 var saved=await db.Set<OfficerAssessmentRequestEntity>().AsNoTracking().Where(x=>x.State=="Saved").ToArrayAsync();Check(saved.Length>0,"Saved requests exist in isolated restored data");var target=saved.First(x=>x.CreatorActorId!=actor);var route="officer-assessments/"+target.Id;
 Identity(actor);foreach(var item in saved)await Expect("officer-assessments/"+item.Id,HttpStatusCode.OK,"HTTP explicit Commissioner saved "+item.Kind+" GET");await Expect("officer-assessments",HttpStatusCode.OK,"HTTP explicit Commissioner history");
 foreach(var item in await db.Set<OfficerAssessmentRequestEntity>().AsNoTracking().Where(x=>x.State!="Saved").ToArrayAsync())await Expect("officer-assessments/"+item.Id,HttpStatusCode.Forbidden,"HTTP foreign nonsaved request remains denied");
 foreach(var draft in await db.Set<OfficerInvestigationEntity>().AsNoTracking().ToArrayAsync())await Expect("officer-investigations/"+draft.Id,HttpStatusCode.Forbidden,"HTTP private investigation denied");
 await Expect("officer-investigations",HttpStatusCode.Forbidden,"HTTP private draft listing denied");await Expect("officer-assessments/catalogue",HttpStatusCode.Forbidden,"HTTP DS catalogue unchanged");
 foreach(var endpoint in new[]{"officer-investigations","officer-assessments/early","officer-assessments/anomaly"}){using var response=await client.PostAsJsonAsync("/api/governance-intelligence/"+endpoint,new{SubmissionKey=Guid.NewGuid(),InvestigationId=target.InvestigationId,ProposalId="test",Findings=Array.Empty<object>(),Scenario=NamedOfficerWorkflow.Scenario});Check(response.StatusCode==HttpStatusCode.Forbidden,"HTTP report permission cannot POST "+endpoint);}
 Identity(target.CreatorActorId,institution:"DS");await Expect(route,HttpStatusCode.OK,"HTTP DS owner unchanged");Identity(foreign,institution:"DS");await Expect(route,HttpStatusCode.Forbidden,"HTTP other DS denied");Identity(foreign,institution:"OTHER");await Expect(route,HttpStatusCode.Forbidden,"HTTP unrelated Officer denied");Identity(foreign,role:"Applicant",institution:"APPLICANT");await Expect(route,HttpStatusCode.Forbidden,"HTTP applicant denied");Identity(null);await Expect(route,HttpStatusCode.Unauthorized,"HTTP anonymous denied");
 Identity(actor);await Write(true);await Expect(route,HttpStatusCode.Forbidden,"HTTP same identity immediate revoked grant GET denial");await Expect("officer-assessments",HttpStatusCode.Forbidden,"HTTP revoked history denial");await Write(false);
 grant=grant with{ExpiresAtUtc=DateTimeOffset.UtcNow.AddMinutes(-1)};await Write(false);await Expect(route,HttpStatusCode.Forbidden,"HTTP expired report grant denied");grant=grant with{ExpiresAtUtc=null};await Write(false);
 await File.WriteAllTextAsync(path,"{malformed");await Expect(route,HttpStatusCode.Forbidden,"HTTP malformed grant fails closed");File.Delete(path);await Expect(route,HttpStatusCode.Forbidden,"HTTP unavailable grant fails closed");await Write(false);
 directory.Active=false;await Expect(route,HttpStatusCode.Forbidden,"HTTP inactive current profile denied");directory.Active=true;
 await File.WriteAllTextAsync(path,JsonSerializer.Serialize(new CentralComplaintGrantFile.Document("c4-central-permissions/1",[])));await Expect(route,HttpStatusCode.Forbidden,"HTTP absent grant denied");await Write(false);
 Identity(foreign);await Expect(route,HttpStatusCode.Forbidden,"HTTP institution alone cannot grant access");Identity(actor);await Expect(route,HttpStatusCode.OK,"HTTP restored exact grant reads saved report again");

 // Independent Post-Workflow reads require no Early/Regulatory journey IDs or completion state.
 Identity(actor);await Expect("post-workflow-demonstrations/catalogue",HttpStatusCode.OK,"HTTP Commissioner independent workflow preview");
 foreach(var job in await db.Set<PostWorkflowDemonstrationEntity>().AsNoTracking().Where(x=>x.State=="Saved").ToArrayAsync()){
  var jobRoute="post-workflow-demonstrations/"+job.Id;
  Identity(job.CreatorActorId,institution:"DS");await Expect(jobRoute,HttpStatusCode.OK,"HTTP owning DS legacy saved anomaly detail independently readable");await Expect("post-workflow-demonstrations/catalogue",HttpStatusCode.OK,"HTTP DS preview has no sequential-stage prerequisite");
  Identity(actor);await Expect(jobRoute,HttpStatusCode.OK,"HTTP explicit Commissioner legacy saved anomaly read");
  Identity(foreign,institution:"DS");await Expect(jobRoute,HttpStatusCode.Forbidden,"HTTP other DS legacy anomaly denied");
  Identity(foreign,role:"Applicant",institution:"APPLICANT");await Expect(jobRoute,HttpStatusCode.Forbidden,"HTTP applicant legacy anomaly denied");Identity(null);await Expect(jobRoute,HttpStatusCode.Unauthorized,"HTTP anonymous legacy anomaly denied");
  Identity(actor);await Write(true);await Expect(jobRoute,HttpStatusCode.Forbidden,"HTTP legacy saved anomaly read observes immediate report-grant revocation");await Write(false);
 }
 Identity(actor);using(var denied=await client.PostAsJsonAsync("/api/governance-intelligence/officer-assessments/anomaly",new{scenario="kiwula-kale-named-route-v2",submissionKey=Guid.NewGuid()}))Check(denied.StatusCode==HttpStatusCode.Forbidden,"HTTP Commissioner has no named-anomaly execution authority");
 using(var held=await client.PostAsJsonAsync("/api/governance-intelligence/post-workflow-demonstrations",new{scenario="administrative"}))Check(held.StatusCode==HttpStatusCode.ServiceUnavailable,"HTTP read-only proof gates legacy execution before any engine/store write");
 await host.StopAsync();
}
Console.WriteLine(JsonSerializer.Serialize(new{Status="Passed",Checks=checks,Reports=reports,Database=c.Database,OrdinaryChanges=false,ModelCalls=0,WriteCommandsInvoked=0},new JsonSerializerOptions{WriteIndented=true}));
sealed class Directory:IComplaintOfficerDirectory{public bool Active=true;public Task<bool> IsCommissionerAsync(Guid id,CancellationToken ct)=>Task.FromResult(Active);public Task<ComplaintDirectorySnapshot> ReadAsync(CancellationToken ct)=>Task.FromResult(new ComplaintDirectorySnapshot([],"unused"));}
sealed class Snapshot(CommissionerReportReadOptions value):IOptionsSnapshot<CommissionerReportReadOptions>{public CommissionerReportReadOptions Value=>value;public CommissionerReportReadOptions Get(string? name)=>value;}
sealed class IsolatedAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder){protected override Task<AuthenticateResult> HandleAuthenticateAsync(){if(!Request.Headers.TryGetValue("X-Isolated-Actor",out var actor))return Task.FromResult(AuthenticateResult.NoResult());var identity=new ClaimsIdentity([new(ClaimTypes.NameIdentifier,actor.ToString()),new(ClaimTypes.Role,Request.Headers["X-Isolated-Role"].ToString()),new("institution",Request.Headers["X-Isolated-Institution"].ToString())],Scheme.Name);return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity),Scheme.Name)));}}
