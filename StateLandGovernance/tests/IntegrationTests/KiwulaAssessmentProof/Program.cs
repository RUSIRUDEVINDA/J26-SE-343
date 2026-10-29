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
await using(var db=new GovernanceIntelligenceDbContext(options))await db.Database.MigrateAsync();
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
const string path="/api/governance-intelligence/kiwula-assessments";const string anomaly="/api/governance-intelligence/post-workflow-demonstrations";
void Actor(Guid? actor,string role="Officer"){client.DefaultRequestHeaders.Remove("X-Isolated-Proof-Actor");client.DefaultRequestHeaders.Remove("X-Isolated-Proof-Role");if(actor.HasValue){client.DefaultRequestHeaders.Add("X-Isolated-Proof-Actor",actor.ToString());client.DefaultRequestHeaders.Add("X-Isolated-Proof-Role",role);}}
async Task<T> Saved<T>(HttpResponseMessage response){if(!response.IsSuccessStatusCode)throw new Exception($"Unexpected HTTP{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");return (await response.Content.ReadFromJsonAsync<T>())!;}
Check((await client.GetAsync(path+"/catalogue")).StatusCode==HttpStatusCode.Unauthorized,"Anonymous401");Actor(owner,"Applicant");Check((await client.GetAsync(path+"/catalogue")).StatusCode==HttpStatusCode.Forbidden,"Applicant403");Actor(owner);
var catalogue=await client.GetFromJsonAsync<JsonElement>(path+"/catalogue");Check(catalogue.GetProperty("canRun").GetBoolean(),"Actual PostgreSQL readiness");
Check((await client.GetFromJsonAsync<JsonElement>(anomaly+"/catalogue")).GetArrayLength()==3,"Two preserved histories plus separately versioned Kiwula example preview");Check(real.Calls==0,"Previews invoke no ML");
Check((await client.PostAsJsonAsync(path,new KiwulaRunRequest(Guid.Empty,"conflict"))).StatusCode==HttpStatusCode.BadRequest,"Missing key rejected before computation");
Check((await client.PostAsJsonAsync(path,new KiwulaRunRequest(Guid.NewGuid(),"unsupported"))).StatusCode==HttpStatusCode.BadRequest,"Unknown engine rejected");
var readonlyTarget=new NpgsqlConnectionStringBuilder(connection){Options="-c default_transaction_read_only=on"};
await using(var ro=new GovernanceIntelligenceDbContext(new DbContextOptionsBuilder<GovernanceIntelligenceDbContext>().UseNpgsql(readonlyTarget.ConnectionString).Options)) {
 var roStore=new KiwulaAssessmentStore(ro,b.Configuration,TimeProvider.System);Check(!await roStore.ReadyAsync(default),"Read-only runtime cannot advertise writes");
 bool invoked=false;try{await roStore.RunAsync(owner,new(Guid.NewGuid(),"conflict"),"[]",_=>{invoked=true;throw new Exception();},default);}catch(KiwulaUnavailableException){}Check(!invoked,"Read-only submission denies before engine invocation");
}
var results=new List<KiwulaReport>();
if(!args.Contains("verify")){
 foreach(var engine in new[]{"conflict","early"}){
  var key=Guid.NewGuid();var simultaneous=await Task.WhenAll(client.PostAsJsonAsync(path,new {submissionKey=key,engine,creatorActorId=Guid.NewGuid()}),client.PostAsJsonAsync(path,new KiwulaRunRequest(key,engine)));
  var first=await Saved<KiwulaReport>(simultaneous[0]);var second=await Saved<KiwulaReport>(simultaneous[1]);Check(first.Id==second.Id,"Concurrent "+engine+" submission saved once");results.Add(first);Check(first.CreatorActorId==owner,"Browser creator spoof ignored");Check(KiwulaCatalogue.Hash(first.InputJson)==first.InputHash,"Exact stored input bytes reproduce hash");
  var replay=await Saved<KiwulaReport>(await client.PostAsJsonAsync(path,new KiwulaRunRequest(key,engine)));Check(replay.Id==first.Id,"HTTP idempotent "+engine+" replay");
  Check((await client.PostAsJsonAsync(path,new KiwulaRunRequest(key,engine=="early"?"conflict":"early"))).StatusCode==HttpStatusCode.Conflict,"Changed request under same key conflicts");
  Check(first.InputHash==KiwulaCatalogue.Hash(JsonSerializer.Serialize(engine=="conflict"?(object)KiwulaCatalogue.Decisions:KiwulaCatalogue.Indicators,KiwulaCatalogue.Json)),"Exact input hash "+engine);
 }
 var input=JsonSerializer.Deserialize<ProposalComplianceInputDto>(File.ReadAllText(Path.Combine(root,"StateLandGovernance.Web/src/modules/component-04/evidence/kiulakale-regulatory-input.json")),RegulatoryJson.Options)!;
 var regulatory=await Saved<RegulatoryRunResponse>(await client.PostAsJsonAsync("/api/governance-intelligence/evaluate-compliance",new EvaluateComplianceCommand("Kiwula isolated actual engine",Input:input,SubmissionKey:Guid.NewGuid(),InputVersion:"kiulakale-english-adaptation-v1",SchemaVersion:"c4-regulatory-input/1",SourceReference:"Existing English adaptation assumptions; isolated test; no actual C3 case")));
 Check(regulatory.Report.Findings?.Count==24,"Actual24-rule regulatory result saved with ownership");
 var workflow=await Saved<PostWorkflowReport>(await client.PostAsJsonAsync(anomaly,new{scenario=KiwulaCatalogue.WorkflowScenario}));Check(workflow.State=="Saved"&&workflow.Assessment!=null,"Real frozen ML result persisted");Check(real.Calls==1,"Exactly one real Kiwula inference");
 var again=await Saved<PostWorkflowReport>(await client.PostAsJsonAsync(anomaly,new{scenario=KiwulaCatalogue.WorkflowScenario}));Check(again.Id==workflow.Id&&real.Calls==1,"Anomaly replay invokes no extra inference");
 var evidence=new{checks,regulatory,results,workflow,realInferenceCalls=real.Calls,testOnlyAuthentication=true};Console.WriteLine(JsonSerializer.Serialize(evidence,KiwulaCatalogue.Json));
 // Deliberately fail after genuine deterministic persistence; outer transaction must roll everything back.
 await using var scope=app.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<GovernanceIntelligenceDbContext>();var store=scope.ServiceProvider.GetRequiredService<IKiwulaAssessmentStore>();var conflict=scope.ServiceProvider.GetRequiredService<DetectConflictsCommandHandler>();var before=await db.ConflictEvaluations.CountAsync();
 try{await store.RunAsync(owner,new(Guid.NewGuid(),"conflict"),"[]",async id=>{await conflict.HandleAsync(new(KiwulaCatalogue.Prefix+id,KiwulaCatalogue.Decisions));throw new InvalidOperationException("Isolated simulated transaction failure");},default);throw new Exception("Failure simulation did not throw");}catch(InvalidOperationException){}
 db.ChangeTracker.Clear();Check(await db.ConflictEvaluations.CountAsync()==before,"Simulated failure rolled back engine/result/audit/outbox association transaction");
}else{
 results.AddRange((await client.GetFromJsonAsync<KiwulaReport[]>(path))!);Check(results.Count==2,"Separate process saved deterministic retrieval");
 var workflows=await client.GetFromJsonAsync<PostWorkflowReport[]>(anomaly);Check(workflows!.Count(x=>x.Scenario==KiwulaCatalogue.WorkflowScenario)==1,"Separate process Kiwula ML report retrieval");Check(real.Calls==0,"Restart retrieval zero inference");
}
foreach(var saved in results){Actor(owner);Check((await client.GetFromJsonAsync<KiwulaReport>(path+"/"+saved.Id))!.InputHash==saved.InputHash,"Saved GET exact provenance");Actor(Guid.NewGuid());Check((await client.GetAsync(path+"/"+saved.Id)).StatusCode==HttpStatusCode.Forbidden,"Cross creator403");Check((await client.GetFromJsonAsync<KiwulaReport[]>(path))!.Length==0,"Other creator history empty");if(saved.AuditRecordId is {} auditId){Check((await client.GetAsync("/api/governance-intelligence/audits/"+auditId+"/receipt")).StatusCode==HttpStatusCode.NotFound,"Owned conflict audit hidden from legacy cross-creator lookup");Actor(null);Check((await client.GetAsync("/api/governance-intelligence/audits/"+auditId+"/receipt")).StatusCode==HttpStatusCode.NotFound,"Owned conflict audit hidden from anonymous legacy lookup");}}
Actor(owner);Check((await client.PostAsJsonAsync("/api/governance-intelligence/detect-conflicts",new DetectConflictsCommand("disabled sandbox",KiwulaCatalogue.Decisions))).StatusCode==HttpStatusCode.ServiceUnavailable,"Legacy conflict sandbox separately disabled");
Check((await client.PostAsJsonAsync("/api/governance-intelligence/screen-early-governance",new ScreenEarlyGovernanceCommand(Guid.NewGuid(),Guid.NewGuid(),"example","1",KiwulaCatalogue.Indicators))).StatusCode==HttpStatusCode.ServiceUnavailable,"Legacy early screening sandbox separately disabled");
Console.WriteLine(JsonSerializer.Serialize(new{checks,realInferenceCalls=real.Calls},KiwulaCatalogue.Json));await app.StopAsync();

sealed class CountedMl(IWorkflowAnomalyClient inner):IWorkflowAnomalyClient {
 public int Calls;public async Task<WorkflowAnomalyResult> PredictAnomalyAsync(string caseId,IReadOnlyList<WorkflowAnomalyEvent> events,CancellationToken ct=default){Interlocked.Increment(ref Calls);return await inner.PredictAnomalyAsync(caseId,events,ct);}
}
