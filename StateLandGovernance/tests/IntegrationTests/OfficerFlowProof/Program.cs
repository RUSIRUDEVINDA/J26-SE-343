using StateLandGovernance.GovernanceIntelligence.Application.OfficerFlows;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.Demonstrations;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;
using StateLandGovernance.GovernanceIntelligence.Application.WorkflowAnomaly;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.DependencyInjection;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Integrations.WorkflowAnomaly;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
using StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;

// Dedicated loopback test host. Never reads .env or starts the ordinary API, workers or accounts.
var connection=Environment.GetEnvironmentVariable("C4_KIWULA_ISOLATED_CONNECTION")??throw new Exception("Explicit isolated connection required");
var target=new NpgsqlConnectionStringBuilder(connection);
if(target.Host!="127.0.0.1"||target.Port<55000||target.Database?.StartsWith("c4_kiwula_test_",StringComparison.Ordinal)!=true)throw new Exception("Refusing ordinary database");
var root=Path.GetFullPath(args.FirstOrDefault(x=>x!="verify")??Directory.GetCurrentDirectory());
var options=new DbContextOptionsBuilder<GovernanceIntelligenceDbContext>().UseNpgsql(connection,p=>p.MigrationsHistoryTable("__ef_migrations_history","governance_intelligence")).Options;

var checks=new List<string>();void Check(bool yes,string label){if(!yes)throw new Exception(label);checks.Add(label);}
var owner=Guid.Parse("55188890-f112-47ec-9778-8bf8dc7b5533");
var real=new CountedMl(new HttpWorkflowAnomalyClient(new HttpClient{BaseAddress=new("http://127.0.0.1:8104/"),Timeout=TimeSpan.FromSeconds(45)},NullLogger<HttpWorkflowAnomalyClient>.Instance));
var b=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName="Development",Args=[]});b.Logging.ClearProviders();b.WebHost.UseUrls("http://127.0.0.1:0");
b.Configuration.AddInMemoryCollection(new Dictionary<string,string?>{
 ["ConnectionStrings:GovernanceIntelligenceConnection"]=connection,
 ["GovernanceIntelligence:Blockchain:OutboxWorker:Enabled"]="false",
 ["GovernanceIntelligence:KiwulaWorkspace:Enabled"]="true",["GovernanceIntelligence:KiwulaWorkspace:WritesEnabled"]="true",
 ["GovernanceIntelligence:PostWorkflowDemonstrations:Enabled"]="true",["GovernanceIntelligence:PostWorkflowDemonstrations:WritesEnabled"]="true",
 ["GovernanceIntelligence:RegulatoryProvenance:Enabled"]="true",["GovernanceIntelligence:RegulatoryProvenance:WritesEnabled"]="true",
 ["ComplaintClassifier:BaseUrl"]="http://127.0.0.1:8104/",["ComplaintClassifier:TimeoutSeconds"]="45",
 ["WorkflowAnomaly:BaseUrl"]="http://127.0.0.1:8104/",["WorkflowAnomaly:TimeoutSeconds"]="45"});
b.Services.AddSingleton<StateLandGovernance.GovernanceIntelligence.Domain.Services.IRegulatoryComplianceEngine,StateLandGovernance.GovernanceIntelligence.Domain.Services.RegulatoryComplianceEngine>();
b.Services.AddSingleton<IRegulatoryRuleProvider,InMemoryRegulatoryRuleProvider>();b.Services.AddGovernanceIntelligenceInfrastructure(b.Configuration,b.Environment);
b.Services.AddTransient<StateLandGovernance.GovernanceIntelligence.Application.Queries.GetAuditReceiptQueryHandler>();
b.Services.AddTransient<EvaluateComplianceCommandHandler>();b.Services.AddGovernanceConflictDetection();b.Services.AddGovernanceRiskIntelligence();b.Services.AddExplainableGovernanceEngine();b.Services.AddGovernanceConsensusEngine();b.Services.AddConditionalGovernanceVerification();b.Services.AddEarlyGovernanceScreening();b.Services.AddLeaseApprovalTimingMonitoring();b.Services.AddGovernanceScreeningOrchestration();
b.Services.AddSingleton<IWorkflowAnomalyClient>(real);
b.Services.AddControllers().AddApplicationPart(typeof(KiwulaAssessmentsController).Assembly);
b.Services.AddAuthentication("isolated-proof").AddScheme<AuthenticationSchemeOptions,ProofAuthentication>("isolated-proof",_=>{});b.Services.AddAuthorization();
await using var app=b.Build();app.UseAuthentication();app.UseAuthorization();app.MapControllers();await app.StartAsync();
var address=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
using var client=new HttpClient{BaseAddress=new(address),Timeout=TimeSpan.FromSeconds(90)};
const string path="/api/governance-intelligence/officer-assessments";
void Actor(Guid? actor,string role="Officer",string institution="DS") {
 client.DefaultRequestHeaders.Clear();
 if(actor.HasValue){client.DefaultRequestHeaders.Add("X-Actor",actor.ToString());client.DefaultRequestHeaders.Add("X-Role",role);client.DefaultRequestHeaders.Add("X-Institution",institution);}
}
async Task<T> Saved<T>(HttpResponseMessage response){if(!response.IsSuccessStatusCode)throw new Exception($"HTTP{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");return (await response.Content.ReadFromJsonAsync<T>())!;}
Check((await client.GetAsync(path+"/catalogue")).StatusCode==HttpStatusCode.Unauthorized,"Anonymous401");
Actor(owner,"Applicant");Check((await client.GetAsync(path+"/catalogue")).StatusCode==HttpStatusCode.Forbidden,"Applicant403");
Actor(owner,"Officer","EXTERNAL");Check((await client.GetAsync(path+"/catalogue")).StatusCode==HttpStatusCode.Forbidden,"NonDS403");Actor(owner);
var preview=await Saved<JsonElement>(await client.GetAsync(path+"/catalogue"));Check(preview.GetProperty("canRun").GetBoolean(),"Restricted-role readiness");Check(real.Calls==0,"Preview no inference");
if(args.Contains("verify")) {
 var history=await Saved<JsonElement>(await client.GetAsync(path));Check(history.GetArrayLength()>=3,"Separate process persisted history");
 foreach(var row in history.EnumerateArray()){
 Check((await client.GetAsync(path+"/"+row.GetProperty("id").GetGuid())).IsSuccessStatusCode,"Restart retrieval");
 if(row.GetProperty("kind").GetString()=="anomaly")Check((await client.GetAsync("/api/governance-intelligence/workflow-anomalies/"+row.GetProperty("anomalyAssessmentId").GetGuid())).StatusCode==HttpStatusCode.Forbidden,"Reserved anomaly hidden from legacy reader");
 else Check((await client.GetAsync("/api/governance-intelligence/screening/"+row.GetProperty("earlyAssessmentId").GetGuid())).StatusCode==HttpStatusCode.Forbidden,"Reserved early result hidden from legacy reader");
}
 Check(real.Calls==0,"Restart zero inference");Console.WriteLine(JsonSerializer.Serialize(new{checks,realInferenceCalls=real.Calls}));await app.StopAsync();return;
}
var names=Enum.GetNames<StateLandGovernance.GovernanceIntelligence.Domain.Enums.EarlyGovernanceIndicatorType>();
var input=new SaveInvestigation(Guid.NewGuid(),KiwulaCatalogue.ProposalId,names.Select((n,i)=>new OfficerFinding(n,i==0?"IssueFound":i==1?"NoIssueFound":"NotYetVerified",i==0?"An officer reports an issue requiring independent verification.":"")).ToArray());
var replies=await Task.WhenAll(client.PostAsJsonAsync("/api/governance-intelligence/officer-investigations",input),client.PostAsJsonAsync("/api/governance-intelligence/officer-investigations",input));
var investigation=await Saved<Investigation>(replies[0]);Check((await Saved<Investigation>(replies[1])).Id==investigation.Id,"Concurrent save idempotent");
Check(investigation.Findings.Single(f=>f.Concern==names[0]).Choice=="IssueFound","Actual observation preserved");
Check(OfficerFlowInput.Indicators(investigation).All(x=>x.EvidenceState is "Unverified" or "Missing"),"No verified promotion");
var earlyKey=Guid.NewGuid();var earlyResult=await Saved<JsonElement>(await client.PostAsJsonAsync(path+"/early",new EarlyRequest(investigation.Id,earlyKey)));
Check(earlyResult.GetProperty("state").GetString()=="Saved","Actual early result saved");
Check((await Saved<JsonElement>(await client.PostAsJsonAsync(path+"/early",new EarlyRequest(investigation.Id,earlyKey)))).GetProperty("id").GetGuid()==earlyResult.GetProperty("id").GetGuid(),"Early replay same request");
var runs=new List<JsonElement>();
for(var i=0;i<2;i++) {
 var key=Guid.NewGuid();
 if(i==0){real.Entered=new(TaskCreationOptions.RunContinuationsAsynchronously);real.Release=new(TaskCreationOptions.RunContinuationsAsynchronously);}
 var pending=client.PostAsJsonAsync(path+"/anomaly",new AnomalyRequest(NamedOfficerWorkflow.Scenario,key));
 if(i==0){await real.Entered!.Task.WaitAsync(TimeSpan.FromSeconds(10));
  var replayPending=await Saved<JsonElement>(await client.PostAsJsonAsync(path+"/anomaly",new AnomalyRequest(NamedOfficerWorkflow.Scenario,key)));
  Check(replayPending.GetProperty("state").GetString()=="Pending","Concurrent same key retrieves reservation");
  Check((await client.PostAsJsonAsync(path+"/anomaly",new AnomalyRequest(NamedOfficerWorkflow.Scenario,Guid.NewGuid()))).StatusCode==HttpStatusCode.Conflict,"Competing key409 during Pending");
  real.Release!.SetResult();
 }
 var result=await Saved<JsonElement>(await pending);real.Entered=null;real.Release=null;
 Check(result.GetProperty("state").GetString()=="Saved","Actual frozen inference saved");runs.Add(result);
 Check((await Saved<JsonElement>(await client.PostAsJsonAsync(path+"/anomaly",new AnomalyRequest(NamedOfficerWorkflow.Scenario,key)))).GetProperty("id").GetGuid()==result.GetProperty("id").GetGuid(),"Anomaly replay same request");Check(real.Calls==i+1,"No replay inference");
}
Check(runs[0].GetProperty("anomalyAssessmentId").GetGuid()!=runs[1].GetProperty("anomalyAssessmentId").GetGuid(),"Distinct genuine assessments same vetted scenario version");
Actor(Guid.NewGuid());Check((await client.GetAsync(path+"/"+runs[0].GetProperty("id").GetGuid())).StatusCode==HttpStatusCode.Forbidden,"Crosscreator403");Check((await Saved<JsonElement>(await client.GetAsync(path))).GetArrayLength()==0,"Other creator history empty");
Actor(owner);foreach(var run in runs)Check((await client.GetAsync(path+"/"+run.GetProperty("id").GetGuid())).IsSuccessStatusCode,"Saved GET");Check(real.Calls==2,"GET no inference");
// Failure simulation never supplies a prediction and never calls the HTTP model.
Actor(Guid.NewGuid());real.Fail=true;var uncertainKey=Guid.NewGuid();
var unknown=await Saved<JsonElement>(await client.PostAsJsonAsync(path+"/anomaly",new AnomalyRequest(NamedOfficerWorkflow.Scenario,uncertainKey)));
Check(unknown.GetProperty("state").GetString()=="Unknown","Uncertain result preserved Unknown");
Check((await Saved<JsonElement>(await client.PostAsJsonAsync(path+"/anomaly",new AnomalyRequest(NamedOfficerWorkflow.Scenario,uncertainKey)))).GetProperty("id").GetGuid()==unknown.GetProperty("id").GetGuid(),"Unknown replay no retry");
Check((await client.PostAsJsonAsync(path+"/anomaly",new AnomalyRequest(NamedOfficerWorkflow.Scenario,Guid.NewGuid()))).StatusCode==HttpStatusCode.Conflict,"Unknown blocks new intent");Check(real.Calls==2,"Simulated failure zero real HTTP calls");real.Fail=false;Actor(owner);
await using(var verifyScope=app.Services.CreateAsyncScope()) {
 var db=verifyScope.ServiceProvider.GetRequiredService<GovernanceIntelligenceDbContext>();
 var audit=await db.Set<OfficerActionEventEntity>().AsNoTracking().ToListAsync();
 Check(audit.All(e=>OfficerFlowInput.Hash(JsonDocument.Parse(e.DetailsJson).RootElement.GetProperty("canonical").GetString()!)==e.EventSha256),"All audit hashes recompute");
 Check(audit.Where(e=>e.RequestId!=null).GroupBy(e=>new{e.RequestId,e.Operation}).All(g=>g.Count()==1),"One start/completion event per request");
}
Console.WriteLine(JsonSerializer.Serialize(new{checks,investigation,earlyResult,runs,realInferenceCalls=real.Calls,testOnlyAuthentication=true},new JsonSerializerOptions(JsonSerializerDefaults.Web)));await app.StopAsync();
sealed class CountedMl(IWorkflowAnomalyClient inner):IWorkflowAnomalyClient {
 public int Calls;public bool Fail;public TaskCompletionSource? Entered,Release;
 public async Task<WorkflowAnomalyResult> PredictAnomalyAsync(string caseId,IReadOnlyList<WorkflowAnomalyEvent> events,CancellationToken ct=default){if(Fail)throw new HttpRequestException("Isolated simulated uncertain upstream outcome");Entered?.TrySetResult();if(Release!=null)await Release.Task.WaitAsync(ct);Interlocked.Increment(ref Calls);return await inner.PredictAnomalyAsync(caseId,events,ct);}
}
sealed class ProofAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder){
 protected override Task<AuthenticateResult> HandleAuthenticateAsync(){if(!Guid.TryParse(Request.Headers["X-Actor"],out var actor))return Task.FromResult(AuthenticateResult.NoResult());var principal=new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier,actor.ToString()),new(ClaimTypes.Role,Request.Headers["X-Role"].ToString()),new("institution",Request.Headers["X-Institution"].ToString())],Scheme.Name));return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal,Scheme.Name)));}
}

