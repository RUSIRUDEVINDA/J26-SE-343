using System.Text.Json;
using System.Security.Claims;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Text.Encodings.Web;
using StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Npgsql;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;

// Explicit isolated target only. Never load .env or API startup/provisioning.
var connection=Environment.GetEnvironmentVariable("C4_REGULATORY_ISOLATED_TEST_CONNECTION")??throw new Exception("Explicit isolated PostgreSQL connection required.");
var target=new NpgsqlConnectionStringBuilder(connection);
if(target.Host!="127.0.0.1"||target.Port<55000||target.Database?.StartsWith("c4_regulatory_test_",StringComparison.Ordinal)!=true)throw new Exception("Refusing non-isolated database target.");
var options=new DbContextOptionsBuilder<GovernanceIntelligenceDbContext>().UseNpgsql(connection,o=>o.MigrationsHistoryTable("__ef_migrations_history","governance_intelligence")).Options;
var checks=new List<string>();void Check(bool ok,string name){if(!ok)throw new Exception("FAILED: "+name);checks.Add(name);}
using var setup=new GovernanceIntelligenceDbContext(options);
if((await setup.Database.GetAppliedMigrationsAsync()).Any())throw new Exception("Use a fresh isolated database; proof never rolls back or resets existing data.");
await setup.GetService<IMigrator>().MigrateAsync("20261021170456_AddRegisteredComplaints");
Check((await setup.Database.GetAppliedMigrationsAsync()).Last()=="20261021170456_AddRegisteredComplaints","Pending complaints migration applies as predecessor on isolated PostgreSQL");
var root=Path.GetFullPath(args.Length==0?Directory.GetCurrentDirectory():args[0]);
var rules=JsonSerializer.Deserialize<ComplianceRuleDefinition[]>(File.ReadAllText(Path.Combine(root,"StateLandGovernance/tests/IntegrationTests/RegulatoryAssessmentPersistenceProof/catalogue-input.json")),RegulatoryJson.Options)!;
foreach(var r in rules){var source=r.SourceReference;var id=Guid.NewGuid();setup.RegulatorySources.Add(new(){Id=id,SourceType=source.SourceType.ToString(),Authority=source.SourceAuthority,DocumentTitle=source.SourceDocument,DocumentVersion=source.SourceVersion??"",DocumentReference=source.SourceDocumentReference,Checksum=source.SourceChecksumLabel,EffectiveFrom=source.EffectiveFrom,EffectiveTo=source.EffectiveTo});setup.ComplianceRules.Add(new(){Id=Guid.NewGuid(),RuleCode=r.RuleCode,RuleVersion=r.RuleVersion,Category=r.EvaluationType.ToString(),RuleType=r.EvaluationType.ToString(),SourceId=id,SourceSection=source.SourceSection,Description=r.Description,CalculationKey=r.CalculationKey,Severity=r.Severity,IsBlocking=r.IsBlocking,Enabled=r.Enabled});}
await setup.SaveChangesAsync();setup.ChangeTracker.Clear();
var configOff=new ConfigurationBuilder().Build();
var historicalHandler=new EvaluateComplianceCommandHandler(new Legacy(),new RegulatoryComplianceEngine(),new PostgresGovernanceEvaluationStore(setup),new PostgresComplianceRuleCatalogue(setup));
var input=JsonSerializer.Deserialize<ProposalComplianceInputDto>(File.ReadAllText(Path.Combine(root,"output/kiulakale-pp1-demonstration/c4-regulatory-input.json")),RegulatoryJson.Options)!;
await historicalHandler.HandleAsync(new("Historical isolated record",Input:input));
var historicalId=await setup.ComplianceEvaluations.Select(x=>x.Id).SingleAsync();var historicalJson=await setup.ComplianceEvaluations.Select(x=>x.FindingsJson).SingleAsync();
var historicalActor=Guid.NewGuid();var historicalGrants=new Grants(historicalActor,historicalId);
var reportsBefore=new IndependentRegulatoryReportStore(setup);var offOwnership=new RegulatorySubmissionStore(setup,reportsBefore,configOff);
var historicalBefore=await new IndependentRegulatoryReportQueries(reportsBefore,historicalGrants,offOwnership).GetAsync(historicalActor,historicalId,default);
Check(historicalBefore?.Provenance==null,"Historical exact grant works before provenance migration with feature disabled");
await setup.Database.MigrateAsync();
Check(await setup.Set<RegulatoryEvaluationProvenanceEntity>().CountAsync()==0,"Additive provenance migration does not backfill historical identity");
var configured=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"GovernanceIntelligence:RegulatoryProvenance:Enabled","true"},{"GovernanceIntelligence:RegulatoryProvenance:WritesEnabled","true"}}).Build();
var roTarget=new NpgsqlConnectionStringBuilder(connection){Options="-c default_transaction_read_only=on"};
await using(var ro=new GovernanceIntelligenceDbContext(new DbContextOptionsBuilder<GovernanceIntelligenceDbContext>().UseNpgsql(roTarget.ConnectionString).Options)){
 var roStore=new RegulatorySubmissionStore(ro,new IndependentRegulatoryReportStore(ro),configured);
 Check(!await roStore.CanSubmitAsync(default),"Read-only PostgreSQL sessions cannot advertise run readiness");
 var invoked=false;try{await roStore.RunAsync(Guid.NewGuid(),Guid.NewGuid(),"readonly",()=>{invoked=true;return Task.FromResult(Guid.NewGuid());},default);}catch(RegulatorySubmissionUnavailableException){}
 Check(!invoked,"Server rejects a read-only write attempt before engine invocation");
}
var owner=Guid.NewGuid();var key=Guid.NewGuid();var request=new EvaluateComplianceCommand("Isolated Kiulakale",Input:input,SubmissionKey:key,InputVersion:"kiulakale-test-1",SchemaVersion:"c4-regulatory-input/1",SourceReference:"Explicit isolated test of prepared demonstration assumptions; no real-case binding.");
var counter=new Counter();
async Task<RegulatoryRunResponse> Run(EvaluateComplianceCommand command,Guid actor){await using var db=new GovernanceIntelligenceDbContext(options);var store=new RegulatorySubmissionStore(db,new IndependentRegulatoryReportStore(db),configured);var handler=new EvaluateComplianceCommandHandler(new Legacy(),new RegulatoryComplianceEngine(),new PostgresGovernanceEvaluationStore(db),new CountingCatalogue(new PostgresComplianceRuleCatalogue(db),counter));return await new RegulatorySubmissionService(handler,store,new UnavailableRegulatoryCaseBindingValidator()).RunAsync(actor,command,default);}
var both=await Task.WhenAll(Run(request,owner),Run(request,owner));
Check(both[0].Report.Metadata.Id==both[1].Report.Metadata.Id&&counter.Count==1&&both.Count(x=>x.Reused)==1,"Concurrent duplicate submissions execute actual engine once and return one saved ID");
var result=both[0].Report;Check(result.Findings?.Count==24,"Real regulatory engine returned24 findings on isolated PostgreSQL catalogue");
var saved=result.Provenance!;var domain=ProposalComplianceInputMapper.Map(input);
Check(saved.CreatorActorId==owner&&saved.EvaluatedInputHash==RegulatoryJson.Hash(JsonSerializer.Serialize(domain,RegulatoryJson.Options)),"Creator and exact mapped engine-input hash persisted");
var loaded=(await new PostgresComplianceRuleCatalogue(setup).GetActiveRulesAsync(DateTime.UtcNow)).OrderBy(x=>x.RuleCode,StringComparer.Ordinal).ThenBy(x=>x.RuleVersion,StringComparer.Ordinal).ToArray();
Check(saved.CatalogueDigest==RegulatoryJson.Hash(JsonSerializer.Serialize(loaded,RegulatoryJson.Options))&&saved.SchemaVersion==request.SchemaVersion&&saved.InputVersion==request.InputVersion,"Actual mapped catalogue digest and input/schema versions persisted");
var storeRead=new RegulatorySubmissionStore(setup,new IndependentRegulatoryReportStore(setup),configured);var queries=new IndependentRegulatoryReportQueries(new IndependentRegulatoryReportStore(setup),historicalGrants,storeRead);
Check((await queries.GetAsync(owner,result.Metadata.Id,default))?.Findings?.Count==24,"Authenticated creator retrieves saved report without exact grant");
try{await queries.GetAsync(Guid.NewGuid(),result.Metadata.Id,default);throw new Exception("Cross actor read accepted");}catch(UnauthorizedAccessException){checks.Add("Cross-actor read denied");}
Check(await storeRead.FindSubmissionAsync(Guid.NewGuid(),key,default)==null,"Submission-status lookup does not disclose another actor's receipt");
try{await Run(request with{InputVersion="different-version"},owner);throw new Exception("Key conflict accepted");}catch(RegulatorySubmissionConflictException){checks.Add("Same key with changed version/input rejected");}
var count=await setup.ComplianceEvaluations.CountAsync();
try{await storeRead.RunAsync(owner,Guid.NewGuid(),"rollback-test",async()=>{await historicalHandler.HandleAsync(new("Rollback isolated record",Input:input));throw new InvalidOperationException("Injected transaction failure");},default);}catch(InvalidOperationException){}
setup.ChangeTracker.Clear();Check(await setup.ComplianceEvaluations.CountAsync()==count,"Failure rolls back evaluation/audit/outbox transaction");
var old=await queries.GetAsync(historicalActor,historicalId,default);Check(old?.Provenance==null&&await setup.ComplianceEvaluations.Where(x=>x.Id==historicalId).Select(x=>x.FindingsJson).SingleAsync()==historicalJson,"Historical findings and missing provenance remain unchanged after migration");
var edited=input with{Location=input.Location! with{Province="Explicit isolated test value"}};
var next=await Run(request with{SubmissionKey=Guid.NewGuid(),Input=edited,InputVersion="edited-2"},owner);Check(next.Report.Provenance!.EvaluatedInputHash!=saved.EvaluatedInputHash&&next.Report.Metadata.Id!=result.Metadata.Id,"Changed structured input gets a distinct immutable saved record/hash");
var versionRow=await setup.ComplianceRules.OrderBy(x=>x.RuleCode).FirstAsync();versionRow.RuleVersion="isolated-version-2";await setup.SaveChangesAsync();setup.ChangeTracker.Clear();
var replayAfterVersion=await Run(request,owner);Check(replayAfterVersion.Reused&&replayAfterVersion.Report.Provenance!.CatalogueDigest==saved.CatalogueDigest,"Same-key retry returns original immutable receipt when catalogue later changes, explicitly reused");
var newVersion=await Run(request with{SubmissionKey=Guid.NewGuid()},owner);Check(newVersion.Report.Provenance!.CatalogueDigest!=saved.CatalogueDigest&&newVersion.Report.Provenance.CatalogueVersion.Contains("isolated-version-2"),"New submission records changed isolated catalogue version/digest without incompatible reuse");
Check(await setup.GovernanceAuditAnchorOutbox.AllAsync(x=>x.Status==0&&x.RetryCount==0&&x.ProcessedAtUtc==null),"Audit outbox entries remain pending; no worker or receipt fabricated");
// Real HTTP/controller path with test-only authentication (no production tokens/credentials/configuration).
var b=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName="Testing",Args=Array.Empty<string>()});b.WebHost.UseUrls("http://127.0.0.1:0");
b.Services.AddAuthentication("Proof").AddScheme<AuthenticationSchemeOptions,ProofAuthentication>("Proof",_=>{});b.Services.AddAuthorization();
b.Services.AddControllers().AddApplicationPart(typeof(GovernanceIntelligenceController).Assembly).AddControllersAsServices();
foreach(var item in configured.AsEnumerable())b.Configuration[item.Key]=item.Value;
b.Logging.ClearProviders();b.Services.AddDbContext<GovernanceIntelligenceDbContext>(o=>o.UseNpgsql(connection,x=>x.MigrationsHistoryTable("__ef_migrations_history","governance_intelligence")));
b.Services.AddSingleton<IRegulatoryRuleProvider,Legacy>();b.Services.AddSingleton<IRegulatoryComplianceEngine,RegulatoryComplianceEngine>();
b.Services.AddScoped<IComplianceRuleCatalogue,PostgresComplianceRuleCatalogue>();b.Services.AddScoped<IGovernanceEvaluationStore,PostgresGovernanceEvaluationStore>();
b.Services.AddScoped<EvaluateComplianceCommandHandler>();b.Services.AddScoped<IIndependentRegulatoryReportStore,IndependentRegulatoryReportStore>();b.Services.AddScoped<IRegulatorySubmissionStore,RegulatorySubmissionStore>();b.Services.AddScoped<IIndependentRegulatoryReadAssignments,DenyIndependentRegulatoryReadAssignments>();b.Services.AddScoped<IRegulatoryCaseBindingValidator,UnavailableRegulatoryCaseBindingValidator>();b.Services.AddScoped<IndependentRegulatoryReportQueries>();b.Services.AddScoped<RegulatorySubmissionService>();
b.Services.AddScoped<GovernanceIntelligenceController>(sp=>new(sp.GetRequiredService<EvaluateComplianceCommandHandler>(),null!,null!,null!,null!,null!));
await using(var app=b.Build()){
 app.UseAuthentication();app.UseAuthorization();app.MapControllers();await app.StartAsync();
 try{using var client=new HttpClient{BaseAddress=new Uri(app.Urls.Single())};var endpoint="/api/governance-intelligence/evaluate-compliance";
  var httpRequest=request with{SubmissionKey=Guid.NewGuid(),ActionName="HTTP isolated assessment"};
  Check((await client.PostAsJsonAsync(endpoint,httpRequest)).StatusCode==System.Net.HttpStatusCode.Unauthorized,"HTTP anonymous submission401");
  client.DefaultRequestHeaders.Add("X-Proof-Actor",owner.ToString());client.DefaultRequestHeaders.Add("X-Proof-Role","Applicant");
  Check((await client.PostAsJsonAsync(endpoint,httpRequest)).StatusCode==System.Net.HttpStatusCode.Forbidden,"HTTP applicant cannot submit officer assessment");
  client.DefaultRequestHeaders.Remove("X-Proof-Role");client.DefaultRequestHeaders.Add("X-Proof-Role","Officer");
  var body=JsonSerializer.SerializeToNode(httpRequest,RegulatoryJson.Options)!.AsObject();body["creatorActorId"]=Guid.NewGuid().ToString();
  var capability=await client.GetFromJsonAsync<JsonElement>("/api/governance-intelligence/regulatory-assessments/capabilities");Check(capability.GetProperty("enabled").GetBoolean(),"HTTP run readiness confirmed only with flags, schema, writable session and INSERT privileges");
  var response=await client.PostAsJsonAsync(endpoint,body);Check(response.IsSuccessStatusCode,"HTTP officer submission succeeds with actual engine and PostgreSQL");
  var httpResult=(await response.Content.ReadFromJsonAsync<RegulatoryRunResponse>())!;
  Check((await client.PostAsJsonAsync(endpoint,httpRequest with{SchemaVersion="unsupported-schema"})).StatusCode==System.Net.HttpStatusCode.BadRequest,"HTTP unsupported input schema rejected");
  Check((await client.PostAsJsonAsync(endpoint,httpRequest with{CaseId="untrusted-case-label"})).StatusCode==System.Net.HttpStatusCode.BadRequest,"HTTP unvalidated free-form C3 reference rejected");
  Check((await client.PostAsJsonAsync(endpoint,httpRequest with{C3Binding=new(Guid.NewGuid())})).StatusCode==System.Net.HttpStatusCode.Forbidden,"HTTP supplied inaccessible C3 binding denied");
  Check(httpResult.Report.Provenance!.CreatorActorId==owner,"HTTP creator spoofing ignored; creator derived server-side");
  var savedPath="/api/governance-intelligence/regulatory-assessments/"+httpResult.Report.Metadata.Id;
  Check((await client.GetAsync(savedPath)).IsSuccessStatusCode,"HTTP creator saved-report GET succeeds");
  client.DefaultRequestHeaders.Remove("X-Proof-Actor");client.DefaultRequestHeaders.Add("X-Proof-Actor",Guid.NewGuid().ToString());
  Check((await client.GetAsync(savedPath)).StatusCode==System.Net.HttpStatusCode.Forbidden,"HTTP cross-actor saved-report GET403");
  var auditPath="/api/governance-intelligence/audits/"+httpResult.Report.Metadata.AuditRecordId+"/receipt";
  Check((await client.GetAsync(auditPath)).StatusCode==System.Net.HttpStatusCode.Forbidden,"Legacy audit receipt denies another officer for owned evaluation");
  client.DefaultRequestHeaders.Remove("X-Proof-Actor");
  Check((await client.GetAsync(auditPath)).StatusCode==System.Net.HttpStatusCode.Unauthorized,"Legacy audit receipt denies anonymous access to owned evaluation");
  client.DefaultRequestHeaders.Add("X-Proof-Actor",owner.ToString());
  Check((await client.GetAsync(auditPath)).StatusCode==System.Net.HttpStatusCode.NotFound,"Creator passes audit ownership guard without fabricated receipt");
  Check(await storeRead.ProtectedEvaluationForAuditAsync(httpResult.Report.Metadata.AuditRecordId,default)==httpResult.Report.Metadata.Id,"Legacy audit lookup resolves persisted owned association");

 }finally{await app.StopAsync();}
}
Console.WriteLine(JsonSerializer.Serialize(new{status="passed",isolatedTarget=target.Database,port=target.Port,checks},new JsonSerializerOptions{WriteIndented=true}));
sealed class Legacy:IRegulatoryRuleProvider{public Task<IEnumerable<RegulatoryRule>> GetActiveRulesAsync(CancellationToken ct=default)=>Task.FromResult<IEnumerable<RegulatoryRule>>(Array.Empty<RegulatoryRule>());}
sealed class Counter{public int Count;}
sealed class CountingCatalogue(IComplianceRuleCatalogue inner,Counter counter):IComplianceRuleCatalogue{public async Task<IReadOnlyList<ComplianceRuleDefinition>> GetActiveRulesAsync(DateTime t,CancellationToken ct=default){Interlocked.Increment(ref counter.Count);await Task.Delay(50,ct);return await inner.GetActiveRulesAsync(t,ct);}}
sealed class Grants(Guid actor,Guid id):IIndependentRegulatoryReadAssignments{public Task<IReadOnlyList<Guid>> ReadAllowedIdsAsync(Guid current,CancellationToken ct)=>Task.FromResult<IReadOnlyList<Guid>>(current==actor?new[]{id}:Array.Empty<Guid>());}

sealed class ProofAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder){
 protected override Task<AuthenticateResult> HandleAuthenticateAsync(){
  if(!Guid.TryParse(Request.Headers["X-Proof-Actor"],out var actor))return Task.FromResult(AuthenticateResult.NoResult());
  var role=Request.Headers["X-Proof-Role"].ToString();var identity=new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,actor.ToString()),new Claim(ClaimTypes.Role,role)},Scheme.Name);
  return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity),Scheme.Name)));
 }
}
