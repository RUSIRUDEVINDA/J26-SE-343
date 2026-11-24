using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
using StateLandGovernance.GovernanceIntelligence.Application.ComplaintClassification;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Integrations.ComplaintClassification;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
using StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;

// Dedicated disposable Docker PostgreSQL only. Never reads .env or ordinary account configuration.
if(args.Contains("--review-grant"))
{
 var reviewed=new ConfigurationBuilder().AddJsonFile(Path.GetFullPath("StateLandGovernance/docs/verification/owner-proposals/central-complaint-intake/COMBINED_PROPOSED_ACTIVATION_SETTINGS.json")).Build().GetSection("GovernanceIntelligence:CentralComplaints").Get<CentralComplaintOptions>()!;
 var proposed=reviewed.Grants.Single();var grantClock=new ReviewClock{Now=proposed.ValidFromUtc};
 if(proposed.ActorId!=Guid.Parse("0ab669fe-d0ab-5695-ad55-10f6b3b5aab3")||proposed.GrantId!=Guid.Parse("3970d3d2-5fca-4858-a9ae-e98ed04f9809")||proposed.ReviewOperations.Length!=9||proposed.Permissions.Length!=3)throw new Exception("Unexpected reviewed grant configuration");
 var policy=new ComplaintRoutingPolicy(reviewed,new Directory(proposed.ActorId,Guid.NewGuid(),Guid.NewGuid()),grantClock);
 foreach(var operation in ComplaintHandlingRules.ReviewOperations)await policy.RequireReviewAsync(proposed.ActorId,operation,default);
 async Task DeniedAt(DateTimeOffset when){grantClock.Now=when;try{await policy.RequireCentralAsync(proposed.ActorId,false,default);throw new Exception("Unexpected timed authority");}catch(UnauthorizedAccessException){}}
 await DeniedAt(proposed.ValidFromUtc.AddTicks(-1));await DeniedAt(proposed.ExpiresAtUtc!.Value);
 Console.WriteLine(JsonSerializer.Serialize(new{Status="Passed",ActualConfigurationBinding=true,proposed.ActorId,proposed.GrantId,proposed.ValidFromUtc,proposed.ExpiresAtUtc,Operations=proposed.ReviewOperations,BeforeWindowDenied=true,ExpiryDenied=true,OrdinaryAuthorityIssued=false},new JsonSerializerOptions{WriteIndented=true}));return;
}
var connection=Environment.GetEnvironmentVariable("C4_HANDLING_ISOLATED_CONNECTION")??throw new Exception("Explicit isolated connection required");
var target=new NpgsqlConnectionStringBuilder(connection);
var restoredProof=args.Contains("--restored-copy")&&target.Database?.StartsWith("c4_central_restore_preparation_",StringComparison.Ordinal)==true&&target.Database["c4_central_restore_preparation_".Length..].All(x=>char.IsDigit(x)||x=='_');
if(target.Host!="127.0.0.1"||target.Port!=57657||(!restoredProof&&target.Database is not ("c4_handling_test" or "c4_handling_test_final" or "c4_handling_test_complete")))throw new Exception("Refusing non-test target");
if(args.Contains("--verify-existing")) { await VerifyStorageAsync(connection); return; }
var checks=new List<string>();
void Check(bool yes,string label){if(!yes)throw new Exception("FAILED: "+label);checks.Add(label);File.WriteAllText(Path.Combine(Path.GetTempPath(),"slg-handling-"+target.Database+"-checks.json"),JsonSerializer.Serialize(checks));}
DbContextOptions<GovernanceIntelligenceDbContext> Options(string c)=>new DbContextOptionsBuilder<GovernanceIntelligenceDbContext>().UseNpgsql(c,p=>p.MigrationsHistoryTable("__ef_migrations_history","governance_intelligence")).Options;
var owner=Guid.Parse("11111111-0000-4000-8000-000000000001");
var central=Guid.Parse("11111111-0000-4000-8000-000000000002");
var ds1=Guid.Parse("11111111-0000-4000-8000-000000000003");
var ds2=Guid.Parse("11111111-0000-4000-8000-000000000004");
var foreign=Guid.Parse("11111111-0000-4000-8000-000000000005");
var dir=new Directory(central,ds1,ds2);
var clock=TimeProvider.System;
var faults=new FaultSwitch();
var settings=new CentralComplaintOptions{Enabled=true,WritesEnabled=true,Grants=[new(central,Guid.NewGuid(),"COMMISSIONER",[ComplaintRoutingPolicy.Read,ComplaintRoutingPolicy.Assign,ComplaintRoutingPolicy.Review],clock.GetUtcNow().AddHours(-1),clock.GetUtcNow().AddHours(2),"isolated fixture authorization")]};
settings.Grants[0].ReviewOperations=ComplaintHandlingRules.ReviewOperations;
var persistentProof=args.Contains("--persistent-note-proof")||args.Contains("--persistent-source");
CentralComplaintGrant? persistentGrant=null;
if(persistentProof){
 var folder=Path.Combine(Path.GetTempPath(),"slg-handling-persistent-"+Guid.NewGuid().ToString("N"));System.IO.Directory.CreateDirectory(folder);
 persistentGrant=settings.Grants.Single() with{Persistent=true,ExpiresAtUtc=null};settings.Grants=[];settings.GrantFilePath=Path.Combine(folder,"permissions.json");
 await File.WriteAllTextAsync(settings.GrantFilePath,JsonSerializer.Serialize(new StateLandGovernance.Api.Security.CentralComplaintGrantFile.Document("c4-central-permissions/1",[persistentGrant])));
}
var preserved=new RegisteredComplaint(Guid.NewGuid(),owner,Guid.NewGuid(),"original-fingerprint","Preserved isolated original","Fictional test concern; no real person is alleged.",null,null,DateTimeOffset.UtcNow,0,"Pending","Open",[],[]);
if(!args.Contains("--manual-feedback-proof")&&!restoredProof&&!args.Contains("--persistent-note-proof")&&!args.Contains("--diagnose-review")&&!args.Contains("--verify-handling-existing")&&!args.Contains("--read-final"))
await using(var db=new GovernanceIntelligenceDbContext(Options(connection)))
{
 if((await db.Database.GetAppliedMigrationsAsync()).Any())throw new Exception("Fresh test database required; no reset supported");
 await db.GetService<IMigrator>().MigrateAsync("20261022152556_AddOfficerInvestigationsAndAssessmentRequests");
 await new RegisteredComplaintStore(db,new PostgresComplaintClassificationAssessmentStore(db)).InsertAsync(preserved,default);
 await db.GetService<IMigrator>().MigrateAsync("20261022205550_AddComplaintHandlingEvents");
 Check(await db.Set<ComplaintAssignmentEntity>().CountAsync()==0,"Additive migration creates empty assignment table; no backfill");
 Check((await new RegisteredComplaintStore(db,new PostgresComplaintClassificationAssessmentStore(db)).GetAsync(preserved.Id,default))!.ComplaintText==preserved.ComplaintText,"Pre-migration original payload preserved");
 // Reconstruct existing restricted rights in isolated storage, then apply only the approved new-table delta.
 await db.Database.ExecuteSqlRawAsync("""
 DO $$ BEGIN IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname='central_runtime') THEN CREATE ROLE central_runtime LOGIN PASSWORD 'isolated-runtime-only'; END IF; END $$;

 GRANT USAGE ON SCHEMA governance_intelligence TO central_runtime;
 GRANT SELECT ON ALL TABLES IN SCHEMA governance_intelligence TO central_runtime;
 GRANT INSERT ON governance_intelligence.registered_complaints, governance_intelligence.registered_complaint_classifications, governance_intelligence.complaint_classification_assessments TO central_runtime;
 GRANT UPDATE ("Revision","PayloadJson") ON governance_intelligence.registered_complaints TO central_runtime;
 GRANT SELECT, INSERT ON governance_intelligence.complaint_assignment_events, governance_intelligence.complaint_handling_events TO central_runtime;
 """);
}
var runtime=new NpgsqlConnectionStringBuilder(connection){Username=restoredProof?"slg_c4_regulatory_runtime":"central_runtime",Password="isolated-runtime-only"}.ConnectionString;
var counted=new Counted(new HttpComplaintClassificationClient(new HttpClient{BaseAddress=new("http://127.0.0.1:8104/"),Timeout=TimeSpan.FromSeconds(45)},NullLogger<HttpComplaintClassificationClient>.Instance));
async Task<WebApplication> Host()
{
 var b=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName="Testing",Args=[]});b.Logging.ClearProviders();b.WebHost.UseUrls("http://127.0.0.1:0");
 b.Configuration.AddInMemoryCollection(new Dictionary<string,string?>{["GovernanceIntelligence:Complaints:Enabled"]="true",["GovernanceIntelligence:Complaints:WritesEnabled"]="true"});
 b.Services.AddScoped<ComplaintAssignmentAuditInterceptor>();
 b.Services.AddDbContext<GovernanceIntelligenceDbContext>((sp,o)=>o.UseNpgsql(runtime).AddInterceptors(sp.GetRequiredService<ComplaintAssignmentAuditInterceptor>()));
 b.Services.AddSingleton(settings);b.Services.AddSingleton<IComplaintOfficerDirectory>(dir);b.Services.AddSingleton(clock);
 if(persistentProof)b.Services.AddScoped<IComplaintCentralGrantSource,StateLandGovernance.Api.Security.CentralComplaintGrantFile>();
 b.Services.AddHttpContextAccessor();b.Services.AddScoped<IComplaintActorContext,HttpActor>();
 b.Services.AddScoped<ComplaintHandlingStore>();b.Services.AddScoped<IComplaintHandlingStore>(sp=>new FaultHandling(sp.GetRequiredService<ComplaintHandlingStore>(),faults));b.Services.AddScoped<ComplaintRoutingPolicy>();b.Services.AddScoped<IComplaintRoutingStore,ComplaintRoutingStore>();b.Services.AddScoped<CentralComplaintService>();
 b.Services.AddScoped<IRegisteredComplaintStore,RegisteredComplaintStore>();b.Services.AddScoped<IComplaintClassificationAssessmentStore,PostgresComplaintClassificationAssessmentStore>();
 b.Services.AddSingleton<IComplaintReferenceAccess,References>();b.Services.AddSingleton<IComplaintClassificationClient>(counted);b.Services.AddScoped<RegisteredComplaintService>();
 b.Services.AddAuthentication("proof").AddScheme<AuthenticationSchemeOptions,ProofAuth>("proof",_=>{});b.Services.AddAuthorization();
 b.Services.AddControllers().AddApplicationPart(typeof(CentralComplaintsController).Assembly);
 var app=b.Build();app.Use(async (context,next)=>{try{await next(context);}catch(Exception e){context.Response.StatusCode=500;await Microsoft.AspNetCore.Http.HttpResponseWritingExtensions.WriteAsync(context.Response,e.ToString());}});app.UseAuthentication();app.UseAuthorization();app.MapControllers();await app.StartAsync();return app;
}
var app=await Host();
HttpClient Client(WebApplication host)=>new(){BaseAddress=new(host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single())};
var http=Client(app);
var handlingKeys=new System.Collections.Concurrent.ConcurrentDictionary<Guid,int>();
async Task<HttpResponseMessage> Send(string path,Guid? actor=null,string role="Officer",object? body=null)
{
 using var request=new HttpRequestMessage(body==null?HttpMethod.Get:HttpMethod.Post,path);
 if(actor!=null){request.Headers.Add("X-Proof-Actor",actor.ToString());request.Headers.Add("X-Proof-Role",role);}
 if(body is AssignComplaint assign && assign.ExpectedHandlingRevision==null){
  if(!handlingKeys.TryGetValue(assign.SubmissionKey,out var revision)){
   using var stateResponse=await Send(path[..path.LastIndexOf("/assignments")]+"/actions",actor,role);
   stateResponse.EnsureSuccessStatusCode();revision=(await stateResponse.Content.ReadFromJsonAsync<ComplaintHandlingView>())!.State.Revision;handlingKeys[assign.SubmissionKey]=revision;
  }
  body=assign with{ExpectedHandlingRevision=revision};
 }
 if(body!=null&&path.EndsWith("/reviews")){
  var node=JsonSerializer.SerializeToNode(body,new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
  using var stateResponse=await Send(path[..path.LastIndexOf("/reviews")]+"/actions",actor,role);
  if(stateResponse.IsSuccessStatusCode&&node["expectedHandlingRevision"]==null){node["expectedHandlingRevision"]=(await stateResponse.Content.ReadFromJsonAsync<ComplaintHandlingView>())!.State.Revision;body=node;}
 }
 if(body!=null)request.Content=JsonContent.Create(body);
 return await http.SendAsync(request);
}
async Task<T> Get<T>(string path,Guid actor,string role="Officer")
{using var response=await Send(path,actor,role);if(!response.IsSuccessStatusCode)throw new Exception(response.StatusCode+":"+await response.Content.ReadAsStringAsync());return (await response.Content.ReadFromJsonAsync<T>())!;}
async Task<T> Post<T>(string path,Guid actor,object body,string role="Officer")
{using var response=await Send(path,actor,role,body);if(!response.IsSuccessStatusCode)throw new Exception(response.StatusCode+":"+await response.Content.ReadAsStringAsync());return (await response.Content.ReadFromJsonAsync<T>())!;}
const string basePath="/api/governance-intelligence/central-complaints",registered="/api/governance-intelligence/registered-complaints";
if(args.Contains("--manual-feedback-proof"))
{
 await using(var seed=new GovernanceIntelligenceDbContext(Options(connection)))await new RegisteredComplaintStore(seed,new PostgresComplaintClassificationAssessmentStore(seed)).InsertAsync(preserved,default);
 var id=preserved.Id;var actionPath=registered+"/"+id+"/actions";
 await Post<ComplaintAssignmentResult>(basePath+"/"+id+"/assignments",central,new AssignComplaint(Guid.NewGuid(),0,ds1,"Isolated fictional manual feedback proof only",0));
 var start=new ComplaintHandlingAction(Guid.NewGuid(),1,"StartInvestigation","Start the fictional isolated investigation",new(){["notes"]="Isolated UI regression; no real allegations or evidence."},[],"");
 var manualFirst=await Post<HandlingActionResult>(actionPath,ds1,start);var manualReplay=await Post<HandlingActionResult>(actionPath,ds1,start);
 Check(manualFirst.EventId==manualReplay.EventId&&manualReplay.Reused,"Start Investigation manualReplay returns identical event");
 Check((await Send(actionPath,ds1,body:start with{Reason="Changed same-key content"})).StatusCode==HttpStatusCode.Conflict,"Changed same-key handling action denied");
 var handling=await Get<ComplaintHandlingView>(actionPath,ds1);Check(handling.State.Status=="UnderInvestigation"&&handling.History.Count(x=>x.Operation=="StartInvestigation")==1,"Start Investigation persisted once with current lifecycle state");
 var manualComplaint=(await Get<RegisteredComplaintDetail>(registered+"/"+id,ds1)).Complaint;
 var reviewBody=new{Revision=manualComplaint.Revision,Status="UnderReview",Reason="Repeated triage is an explicit new isolated entry"};
 var firstReview=await Post<RegisteredComplaint>(registered+"/"+id+"/reviews",ds1,reviewBody);
 Check((await Send(registered+"/"+id+"/reviews",ds1,body:reviewBody)).StatusCode==HttpStatusCode.Conflict,"Stale repeated triage request conflicts without duplicate");
 var secondReview=await Post<RegisteredComplaint>(registered+"/"+id+"/reviews",ds1,new{Revision=firstReview.Revision,Status="UnderReview",Reason=reviewBody.Reason});
 Check(secondReview.Reviews.Length==2&&secondReview.Reviews.Select(x=>x.Id).Distinct().Count()==2,"Two intentional triage submissions retain separate IDs/revisions");
 var current=await Get<ComplaintHandlingView>(actionPath,ds1);Check(current.History.Count(x=>x.Operation=="StartInvestigation")==1&&current.History.Count(x=>x.Operation=="DsReviewRecorded")==2,"Triage mirror events do not duplicate Start Investigation");
 var ownerView=await Get<ComplaintHandlingView>(actionPath,owner,"Applicant");Check(ownerView.History.All(x=>x.ActorId==null&&x.Reason==null&&x.Details==null),"Applicant handling history remains public only");
 Check((await Send(actionPath,ds2)).StatusCode==HttpStatusCode.NotFound,"Unassigned DS denied handling history");
 Check(counted.Calls==0,"No classifier/anomaly inference in manual feedback proof");
 await app.StopAsync();await app.DisposeAsync();http.Dispose();
 await using(var next=await Host()){using var client=new HttpClient{BaseAddress=new Uri(next.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single())};client.DefaultRequestHeaders.Add("X-Proof-Actor",ds1.ToString());client.DefaultRequestHeaders.Add("X-Proof-Role","Officer");var reread=await client.GetFromJsonAsync<ComplaintHandlingView>(actionPath);Check(reread!.History.Any(x=>x.Id==manualFirst.EventId),"New host retrieves the persisted Start Investigation event");await next.StopAsync();}
 Console.WriteLine(JsonSerializer.Serialize(new{Status="Passed",Checks=checks,Database=target.Database,IsolatedFixtureId=id,StartInvestigationId=manualFirst.EventId,ModelCalls=0},new JsonSerializerOptions{WriteIndented=true}));return;
}
if(args.Contains("--persistent-note-proof"))
{
 await using var storage=new GovernanceIntelligenceDbContext(Options(runtime));
 var before=await storage.ComplaintClassificationAssessments.CountAsync();
 await new RegisteredComplaintStore(storage,new PostgresComplaintClassificationAssessmentStore(storage)).InsertAsync(preserved,default);
 var auditInput=new ComplaintHandlingAction(Guid.NewGuid(),0,"TakeDirect","Isolated persistent permission audit proof",new(){["notes"]="Only newly seeded fictional fixture; no original complaint action."},[],"");
 var saved=await Post<HandlingActionResult>(basePath+"/"+preserved.Id+"/actions",central,auditInput);
 var record=await storage.Set<ComplaintHandlingEntity>().AsNoTracking().SingleAsync(x=>x.Id==saved.EventId);
 Check(record.AuthorizationGrantId==persistentGrant!.GrantId&&record.AuthorizationSourceReference!.Contains("grant-document-sha256="),"Actual persisted handling operation retains persistent grant/source/document-digest provenance");
 Check(record.EventCanonicalJson==record.Canonical()&&record.EventSha256==ComplaintRoutingPolicy.Hash(record.EventCanonicalJson),"Persistent permission provenance participates in canonical handling hash");
 await File.WriteAllTextAsync(settings.GrantFilePath!,JsonSerializer.Serialize(new StateLandGovernance.Api.Security.CentralComplaintGrantFile.Document("c4-central-permissions/1",[persistentGrant with{Revoked=true}])));
 Check((await Send(basePath+"/"+preserved.Id+"/actions",central,body:auditInput with{SubmissionKey=Guid.NewGuid(),ExpectedHandlingRevision=1,Operation="RecordNote"})).StatusCode==HttpStatusCode.Forbidden,"Revoked file denies actual write in same running host");
 Check(await storage.Set<ComplaintHandlingEntity>().CountAsync(x=>x.ComplaintId==preserved.Id)==1,"Revoked operation appends no history");
 Check(await storage.ComplaintClassificationAssessments.CountAsync()==before&&counted.Calls==0,"Persistent operation verification preserves existing assessments and invokes no models");
 Console.WriteLine(JsonSerializer.Serialize(new{Status="Passed",Checks=checks,Database=target.Database,IsolatedFixtureId=preserved.Id,HandlingEventId=record.Id,ModelCalls=0},new JsonSerializerOptions{WriteIndented=true}));
 await app.StopAsync();await app.DisposeAsync();return;
}
if(restoredProof)
{
 await using(var seed=new GovernanceIntelligenceDbContext(Options(runtime)))
 {
  Check(await seed.Set<ComplaintAssignmentEntity>().CountAsync()==0&&await seed.Set<ComplaintHandlingEntity>().CountAsync()==0,"Reviewed restored migrations create empty tables without backfill");
  var priorFixture=(await seed.Set<RegisteredComplaintEntity>().Where(x=>x.ApplicantId==owner).ToArrayAsync()).SingleOrDefault(x=>x.PayloadJson.Contains("Preserved isolated original"));
  if(priorFixture!=null)preserved=JsonSerializer.Deserialize<RegisteredComplaint>(priorFixture.PayloadJson)!;
  else await new RegisteredComplaintStore(seed,new PostgresComplaintClassificationAssessmentStore(seed)).InsertAsync(preserved,default);
 }
 var id=preserved.Id;
 Check((await Send(basePath)).StatusCode==HttpStatusCode.Unauthorized,"Anonymous restored-copy queue denied");
 Check((await Send(basePath,ds1)).StatusCode==HttpStatusCode.Forbidden,"DS has no central authority");
 Check((await Get<CentralComplaintPage>(basePath+"?handling=unassigned",central)).Items.Any(x=>x.Complaint.Id==id),"Real repository finds isolated fixture among preserved restored records");
 Check((await Send(registered+"/"+id,foreign,"Applicant")).StatusCode==HttpStatusCode.NotFound,"Foreign applicant denied without enumeration");
 async Task<HandlingActionResult> RestoreAct(string op,Dictionary<string,string> detail,string summary="",Guid? actor=null,bool centralRoute=true){
  var person=actor??central;var route=(centralRoute?basePath:registered)+"/"+id+"/actions";
  var state=await Get<ComplaintHandlingView>(route,person);
  return await Post<HandlingActionResult>(route,person,new ComplaintHandlingAction(Guid.NewGuid(),state.State.Revision,op,"Authorized isolated restored-copy proof",detail,[],summary));
 }
 await RestoreAct("TakeDirect",new(){["notes"]="Direct responsibility for the isolated fixture only."});
 await RestoreAct("StartInvestigation",new(){["notes"]="Fictional fixture only; preserved restored complaints are not acted upon."});
 await RestoreAct("RecordFindings",new(){["findings"]="No actual allegation is evaluated.",["actionsTaken"]="Recorded isolated test limits."});
 var decisionDetails=new Dictionary<string,string>{{"outcome","UnableToDetermine"},{"findings","Fictional isolated fixture; no verified evidence."},{"actionsTaken","Recorded test limitations."},{"decisionBasis","No real complaint adjudication."}};
 await RestoreAct("Resolve",decisionDetails,"Isolated test only: unable to determine.");
 await RestoreAct("Close",new(),"Isolated test closure.");
 await RestoreAct("Reopen",new(){["proceduralReason"]="Exercise approved isolated second responsibility path."},"Isolated test reopened.");
 var beforeAssign=await Get<ComplaintHandlingView>(basePath+"/"+id+"/actions",central);
 var restoreInput=new AssignComplaint(Guid.NewGuid(),0,ds1,"Only the newly seeded fictional fixture is routed.",beforeAssign.State.Revision);
 var assignment=await Post<ComplaintAssignmentResult>(basePath+"/"+id+"/assignments",central,restoreInput);
 Check((await Send(registered+"/"+id,ds1)).IsSuccessStatusCode,"Assigned DS can read isolated fixture on restored schema");
 await RestoreAct("StartInvestigation",new(){["notes"]="Current assigned DS investigation."},actor:ds1,centralRoute:false);
 await RestoreAct("RecordFindings",new(){["findings"]="Only fictional proof facts.",["actionsTaken"]="Returned limits for central decision."},actor:ds1,centralRoute:false);
 await RestoreAct("Resolve",decisionDetails,"Isolated second-path decision.");
 Check((await Send(registered+"/"+id,ds1)).StatusCode==HttpStatusCode.NotFound,"Terminal handling revokes DS scope on restored copy");
 var restoredReplay=await Post<ComplaintAssignmentResult>(basePath+"/"+id+"/assignments",central,restoreInput);
 Check(restoredReplay.Reused&&restoredReplay.EffectiveHandling?.Status=="Resolved","Historical assignment replay preserves effective terminal handling");
 var safe=await Get<ComplaintHandlingView>(registered+"/"+id+"/actions",owner,"Applicant");
 Check(safe.History.All(x=>x.ActorId==null&&x.Details==null&&x.Reason==null&&x.EvidenceReferences==null),"Owner history omits internal metadata on restored copy");
 await using(var storage=new GovernanceIntelligenceDbContext(Options(runtime)))
 {
  var restoredEvents=await storage.Set<ComplaintHandlingEntity>().Where(x=>x.ComplaintId==id).ToArrayAsync();
  Check(restoredEvents.All(x=>x.Canonical()==x.EventCanonicalJson&&ComplaintRoutingPolicy.Hash(x.EventCanonicalJson)==x.EventSha256),"Canonical event hashes roundtrip under copied restricted grants");
 }
 Check(counted.Calls==0,"Restored-copy proof invokes no classifier or anomaly inference");
 await app.StopAsync();await app.DisposeAsync();http.Dispose();
 Console.WriteLine(JsonSerializer.Serialize(new{Status="Passed",Database=target.Database,Checks=checks,FixtureId=id,AssignmentEvent=assignment.Event.Id,ModelCalls=counted.Calls},new JsonSerializerOptions{WriteIndented=true}));return;
}
if(args.Contains("--diagnose-review"))
{
 await using var db=new GovernanceIntelligenceDbContext(Options(runtime));
 var row=JsonSerializer.Deserialize<RegisteredComplaint>((await db.Set<RegisteredComplaintEntity>().ToArrayAsync()).Single(x=>x.PayloadJson.Contains("Isolated unlinked complaint")).PayloadJson)!;
 var v=await Get<ComplaintHandlingView>(registered+"/"+row.Id+"/actions",ds1);
 var response=await Send(registered+"/"+row.Id+"/reviews",ds1,body:new{Revision=row.Revision,Status="UnderReview",Reason="Read original fictional concern",ExpectedHandlingRevision=v.State.Revision});
 Console.WriteLine(await response.Content.ReadAsStringAsync());await app.StopAsync();await app.DisposeAsync();return;
}

if(args.Contains("--read-final"))
{
 await using var storage=new GovernanceIntelligenceDbContext(Options(runtime));
 var row=JsonSerializer.Deserialize<RegisteredComplaint>((await storage.Set<RegisteredComplaintEntity>().ToArrayAsync()).Single(x=>x.PayloadJson.Contains("Isolated unlinked complaint")).PayloadJson)!;
 var centralView=await Get<ComplaintHandlingView>(basePath+"/"+row.Id+"/actions",central);
 Check(centralView.State.Status=="Resolved","Final decision remains available after another process restart");
 var page=await Get<CentralComplaintPage>(basePath+"?handling=terminal&cursor="+row.Id,central);
 Check(page.Items.Count==0,"Server keyset cursor filters terminal queue correctly");
 Check((await Send(basePath+"?handling=terminal&assignment=assigned",central)).StatusCode==HttpStatusCode.BadRequest,"Conflicting queue filter contracts rejected");
 var history=await storage.Set<ComplaintHandlingEntity>().AsNoTracking().Where(x=>x.ComplaintId==row.Id).OrderBy(x=>x.Revision).ToArrayAsync();
 Check(history.Select(x=>x.Revision).SequenceEqual(Enumerable.Range(1,history.Length))&&history.Zip(history.Skip(1)).All(x=>x.Second.PreviousEventId==x.First.Id&&x.Second.FromState==x.First.ToState),"Every persisted lifecycle revision links to its actual previous same-complaint state");
 Check(history.All(x=>x.Canonical()==x.EventCanonicalJson&&ComplaintRoutingPolicy.Hash(x.EventCanonicalJson)==x.EventSha256),"Final history canonical digests roundtrip after restart");
 var publicView=await Get<ComplaintHandlingView>(registered+"/"+row.Id+"/actions",owner,"Applicant");
 Check(publicView.State.ResponsibleActorId==null&&publicView.History.All(x=>x.ActorId==null&&x.Details==null&&x.Reason==null),"Final public history contains no private responsibility or notes");
 Check((await Send(registered+"/"+row.Id+"/actions",foreign,"Applicant")).StatusCode==HttpStatusCode.NotFound,"Foreign applicant action history is non-enumerating");
 Check((await Send(basePath+"/"+row.Id+"/assignments",central,body:new{SubmissionKey=Guid.NewGuid(),ExpectedAssignmentRevision=4,TargetActorId=ds1,Reason="Missing handling revision"})).StatusCode==HttpStatusCode.BadRequest,"Missing required assignment handling revision is400");
 var stored=await storage.ComplaintClassificationAssessments.AsNoTracking().Select(x=>new{x.AssessmentId,x.ModelVersion,x.PredictedCategory,x.AdministrativeProceduralIntegrityProbability,x.LeaseRevenuePaymentEnforcementProbability,x.UnauthorizedAllocationTransferUseProbability,x.ProtectedEnvironmentalLeaseMisuseProbability}).ToArrayAsync();
 Check(stored.Length==1&&counted.Calls==0,"Final read-only process retrieves original real prediction with zero inference");
 Console.WriteLine(JsonSerializer.Serialize(new{Status="Passed",Checks=checks,NewModelCalls=counted.Calls,IsolatedComplaintId=row.Id,FinalState=centralView.State,Predictions=stored,EventIds=history.Select(x=>new{x.Id,x.Operation,x.Revision,x.EventSha256})},new JsonSerializerOptions{WriteIndented=true}));
 await app.StopAsync();await app.DisposeAsync();return;
}
if(args.Contains("--verify-handling-existing"))
{
 await using var storage=new GovernanceIntelligenceDbContext(Options(runtime));
 var existing=JsonSerializer.Deserialize<RegisteredComplaint>((await storage.Set<RegisteredComplaintEntity>().ToArrayAsync()).Single(x=>x.PayloadJson.Contains("Isolated unlinked complaint")).PayloadJson)!;
 var id=existing.Id;
 var view=await Get<ComplaintHandlingView>(basePath+"/"+id+"/actions",central);
 Check(view.State.Status=="Reopened"&&view.History.Count>=18,"Separate process retrieves completed handling history with no model call");
 var lastAssignment=(await Get<ComplaintAssignment[]>(basePath+"/"+id+"/assignments",central)).Last();
 var assignmentInput=new AssignComplaint(Guid.NewGuid(),lastAssignment.Revision,ds2,"New isolated DS episode for race verification",view.State.Revision);
 var beforeAssignments=await storage.Set<ComplaintAssignmentEntity>().CountAsync();
 var beforeEvents=await storage.Set<ComplaintHandlingEntity>().CountAsync();
 faults.Assignment=true;
 Check((await Send(basePath+"/"+id+"/assignments",central,body:assignmentInput)).StatusCode==HttpStatusCode.InternalServerError,"Injected post-assignment failure is observed");
 faults.Assignment=false;
 Check(await storage.Set<ComplaintAssignmentEntity>().CountAsync()==beforeAssignments&&await storage.Set<ComplaintHandlingEntity>().CountAsync()==beforeEvents,"Assignment and handling rollback together; no partial assignment");
 await Post<ComplaintAssignmentResult>(basePath+"/"+id+"/assignments",central,assignmentInput);
 var current=await Get<RegisteredComplaintDetail>(registered+"/"+id,ds2);
 var staleReview=new{Revision=current.Complaint.Revision,Status="ReviewRecorded",Reason="Old-cycle request",ExpectedHandlingRevision=1};
 Check((await Send(registered+"/"+id+"/reviews",ds2,body:staleReview)).StatusCode==HttpStatusCode.Conflict,"Correct complaint revision cannot bypass stale handling revision after reopening/reassignment");
 var reviewInput=new{Revision=current.Complaint.Revision,Status="UnderReview",Reason="Actual isolated triage",ExpectedHandlingRevision=current.Handling!.State.Revision};
 faults.Review=true;
 Check((await Send(registered+"/"+id+"/reviews",ds2,body:reviewInput)).StatusCode==HttpStatusCode.InternalServerError,"Injected post-review failure is observed");
 faults.Review=false;
 var after=await Get<RegisteredComplaintDetail>(registered+"/"+id,ds2);
 Check(JsonSerializer.Serialize(current.Complaint)==JsonSerializer.Serialize(after.Complaint)&&after.Handling!.State.Revision==current.Handling.State.Revision,"Legacy triage and handling rollback together without altering complaint payload");
 var start=new ComplaintHandlingAction(Guid.NewGuid(),after.Handling!.State.Revision,"StartInvestigation","Actual isolated investigation",new(){["notes"]="Only the existing isolated fixture is considered."},[],"");
 await Post<HandlingActionResult>(registered+"/"+id+"/actions",ds2,start);
 current=await Get<RegisteredComplaintDetail>(registered+"/"+id,ds2);
 var revision=current.Handling!.State.Revision;
 var decision=new ComplaintHandlingAction(Guid.NewGuid(),revision,"Resolve","Reasoned concurrent test decision",new(){["outcome"]="UnableToDetermine",["findings"]="No verified allegation evidence.",["actionsTaken"]="Recorded actual limitations.",["decisionBasis"]="Independent verification unavailable."},[],"Unable to determine on current evidence.");
 var reviewRace=new{Revision=current.Complaint.Revision,Status="ReviewRecorded",Reason="Concurrent permitted DS review",ExpectedHandlingRevision=revision};
 var results=await Task.WhenAll(Send(registered+"/"+id+"/reviews",ds2,body:reviewRace),Send(basePath+"/"+id+"/actions",central,body:decision));
 Check(results[0].StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound or HttpStatusCode.Conflict,"DS review has only valid ordering outcomes against decision");
 Check(results[1].StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict,"Resolution serializes with review using handling revision");
 if(results[1].StatusCode==HttpStatusCode.Conflict){var latest=await Get<ComplaintHandlingView>(basePath+"/"+id+"/actions",central);await Post<HandlingActionResult>(basePath+"/"+id+"/actions",central,decision with{SubmissionKey=Guid.NewGuid(),ExpectedHandlingRevision=latest.State.Revision});}
 Check((await Send(registered+"/"+id,ds2)).StatusCode==HttpStatusCode.NotFound&&(await Send(registered+"/"+id+"/actions",ds2)).StatusCode==HttpStatusCode.NotFound,"Committed resolution revokes DS detail and action history");
 var ownerView=await Get<ComplaintHandlingView>(registered+"/"+id+"/actions",owner,"Applicant");
 Check(ownerView.State.Status=="Resolved"&&ownerView.History.All(x=>x.ActorId==null&&x.Reason==null&&x.Details==null&&x.EvidenceReferences==null),"Applicant final state and timeline remain safe after concurrency");
 Check(await storage.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM information_schema.columns WHERE table_schema='governance_intelligence' AND table_name='complaint_handling_events'").SingleAsync()==22,"Handling migration has exactly the approved 22 columns");
 Check(await storage.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM pg_constraint WHERE conrelid='governance_intelligence.complaint_handling_events'::regclass AND contype='f' AND confdeltype='r'").SingleAsync()==3,"All three handling foreign keys restrict deletion");
 Check(await storage.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM pg_indexes WHERE schemaname='governance_intelligence' AND tablename='complaint_handling_events'").SingleAsync()==7,"Four constraint indexes and exactly three reviewed query indexes");
 Check(!await storage.Database.SqlQueryRaw<bool>("SELECT has_schema_privilege(current_user,'governance_intelligence','CREATE') AS \"Value\"").SingleAsync(),"No schema CREATE for runtime");
 try{await storage.Database.ExecuteSqlRawAsync("DELETE FROM governance_intelligence.complaint_handling_events");throw new Exception("Unexpected deletion");}catch(PostgresException e)when(e.SqlState=="42501"){checks.Add("Actual handling DELETE denied by restricted database role");}
 try{await storage.Database.ExecuteSqlRawAsync("UPDATE governance_intelligence.complaint_handling_events SET \"Reason\"='forbidden'");throw new Exception("Unexpected update");}catch(PostgresException e)when(e.SqlState=="42501"){checks.Add("Actual handling UPDATE denied by restricted database role");}
 var all=await storage.Set<ComplaintHandlingEntity>().AsNoTracking().ToArrayAsync();
 Check(all.All(x=>x.EventCanonicalJson==x.Canonical()&&x.EventSha256==ComplaintRoutingPolicy.Hash(x.EventCanonicalJson)),"Final persisted action fields/details recompute every canonical hash");
 Check(counted.Calls==0&&await storage.ComplaintClassificationAssessments.CountAsync()==1,"Separate-process verification makes zero inference calls and preserves the single original prediction");
 Console.WriteLine(JsonSerializer.Serialize(new{Status="Passed",Checks=checks,NewModelCalls=counted.Calls,IsolatedComplaintId=id,FinalState=ownerView.State,EventIds=all.Select(x=>new{x.Id,x.Operation,x.Revision,x.EventSha256})},new JsonSerializerOptions{WriteIndented=true}));
 await app.StopAsync();await app.DisposeAsync();return;
}
Check((await Send(basePath)).StatusCode==HttpStatusCode.Unauthorized,"Anonymous central queue denied 401");
Check((await Send(basePath,ds1)).StatusCode==HttpStatusCode.Forbidden,"DS institution alone has no central authority");
Check((await Send(basePath+"/"+Guid.NewGuid(),ds1)).StatusCode==HttpStatusCode.Forbidden,"Central permission checked before ID existence");
Check((await Send(basePath,owner,"Applicant")).StatusCode==HttpStatusCode.Forbidden,"Applicant denied central routes");
var input=new SubmitRegisteredComplaint(Guid.NewGuid(),"Isolated unlinked complaint","This fictional lease complaint concerns missing payment notices and unacknowledged correspondence. It is an isolated software test.",null,null);
var complaint=await Post<RegisteredComplaint>(registered,owner,input,"Applicant");
Check(complaint.CaseId==null&&complaint.ClassificationState=="Classified"&&counted.Calls==1,"Unlinked complaint saved with one genuine frozen classifier response");

Check((await Post<RegisteredComplaint>(registered,owner,input,"Applicant")).Id==complaint.Id&&counted.Calls==1,"Applicant idempotent replay invokes no second classification");
Check((await Get<RegisteredComplaintDetail>(registered+"/"+complaint.Id,owner,"Applicant")).Classifications.Count==1,"Owner sees exact saved classification");
Check((await Send(registered+"/"+complaint.Id,foreign,"Applicant")).StatusCode==HttpStatusCode.NotFound,"Foreign applicant and absent ID share 404");
Check((await Send(registered+"/"+complaint.Id,ds1)).StatusCode==HttpStatusCode.NotFound,"Unassigned DS cannot read unlinked complaint");
Check((await Get<CentralComplaintPage>(basePath+"?assignment=unassigned",central)).Items.Count==2,"Central queue includes existing and new unassigned complaints");
Check((await Get<ComplaintOfficer[]>(basePath+"/officers",central)).Length==2,"Safe trusted DS selector");
var assign=new AssignComplaint(Guid.NewGuid(),0,ds1,"Manual routing for isolated test office");
var first=await Post<ComplaintAssignmentResult>(basePath+"/"+complaint.Id+"/assignments",central,assign);
Check(first.Current.Revision==1&&!first.Reused,"Initial assignment saved under restricted role");
Check((await Get<RegisteredComplaint[]>(registered,ds1)).Single().Id==complaint.Id,"DS list scoped to current explicit assignment");
Check((await Send(registered+"/"+complaint.Id,ds2)).StatusCode==HttpStatusCode.NotFound,"Other eligible DS denied");
var replay=await Post<ComplaintAssignmentResult>(basePath+"/"+complaint.Id+"/assignments",central,assign);
Check(replay.Reused&&replay.Event.Id==first.Event.Id,"Same key/content assignment replay");
Check((await Send(basePath+"/"+complaint.Id+"/assignments",central,body:assign with{Reason="changed"})).StatusCode==HttpStatusCode.Conflict,"Changed body under same key conflicts 409");
Check((await Send(basePath+"/"+complaint.Id+"/assignments",central,body:assign with{SubmissionKey=Guid.NewGuid(),ExpectedAssignmentRevision=1})).StatusCode==HttpStatusCode.Conflict,"Redundant new-key assignment rejected");
var review=await Post<RegisteredComplaint>(registered+"/"+complaint.Id+"/reviews",ds1,new{Revision=complaint.Revision,Status="UnderReview",Reason="Read original fictional concern"});
Check(review.Reviews.Length==1&&review.ComplaintText==complaint.ComplaintText,"Authorized review preserves original and classification");
var reassigned=await Post<ComplaintAssignmentResult>(basePath+"/"+complaint.Id+"/assignments",central,new AssignComplaint(Guid.NewGuid(),1,ds2,"Reassign manually to second test office"));
Check(reassigned.Current.PreviousAssignmentId==first.Event.Id&&reassigned.Current.Revision==2,"Append-only reassignment chain");
Check((await Send(registered+"/"+complaint.Id,ds1)).StatusCode==HttpStatusCode.NotFound,"Previous DS immediately loses read access");
Check((await Send(registered+"/"+complaint.Id+"/reviews",ds1,body:new{Revision=review.Revision,Status="ReviewRecorded",Reason="revoked"})).StatusCode==HttpStatusCode.NotFound,"Previous DS loses review writes");
Check((await Send(registered+"/"+complaint.Id+"/classification-retries",ds1,body:new{Revision=review.Revision})).StatusCode==HttpStatusCode.NotFound,"Previous DS loses retry access without inference");
replay=await Post<ComplaintAssignmentResult>(basePath+"/"+complaint.Id+"/assignments",central,assign);
Check(replay.Event.Revision==1&&replay.Current.Revision==2&&replay.Current.TargetActorId==ds2,"Historical replay reports current assignment without restoring former access");
dir.Revoked=true;
Check((await Send(registered+"/"+complaint.Id,ds2)).StatusCode==HttpStatusCode.NotFound,"Live directory revocation fails closed");
Check(!(await Get<CentralComplaintDetail>(basePath+"/"+complaint.Id,central)).TargetEligible,"Central sees assigned-but-ineligible target");
dir.Revoked=false;
// Concurrent initial assignment transactions: exactly one winner, one stale-revision conflict.
var race=await Task.WhenAll(Send(basePath+"/"+preserved.Id+"/assignments",central,body:new AssignComplaint(Guid.NewGuid(),0,ds1,"Initial concurrent routing A")),Send(basePath+"/"+preserved.Id+"/assignments",central,body:new AssignComplaint(Guid.NewGuid(),0,ds2,"Initial concurrent routing B")));
Check(race.Count(x=>x.StatusCode==HttpStatusCode.OK)==1&&race.Count(x=>x.StatusCode==HttpStatusCode.Conflict)==1,"Concurrent initial assignments serialize with one winner");
// Race old DS triage with reassignment: review may commit before reassignment, never after revocation.
var race2=await Task.WhenAll(Send(registered+"/"+complaint.Id+"/reviews",ds2,body:new{Revision=review.Revision,Status="ReviewRecorded",Reason="Concurrent local review"}),Send(basePath+"/"+complaint.Id+"/assignments",central,body:new AssignComplaint(Guid.NewGuid(),2,ds1,"Concurrent controlled reassignment")));
Check(race2[1].StatusCode==HttpStatusCode.OK&&race2[0].StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,"Review/reassignment shared lock permits only authorized transaction ordering");
Check((await Send(registered+"/"+complaint.Id,ds2)).StatusCode==HttpStatusCode.NotFound,"Reassigned DS remains revoked after race");
var grant=settings.Grants[0];settings.Grants=[grant with{ExpiresAtUtc=DateTimeOffset.UtcNow.AddSeconds(-1)}];
Check((await Send(basePath,central)).StatusCode==HttpStatusCode.Forbidden,"Expired central grant denied immediately");settings.Grants=[grant];
await app.StopAsync();await app.DisposeAsync();http.Dispose();app=await Host();http=Client(app);
var restored=await Get<CentralComplaintDetail>(basePath+"/"+complaint.Id,central);
Check(restored.Detail.Complaint.ComplaintText==complaint.ComplaintText&&counted.Calls==1,"New host retrieves saved original/classification without inference");
var events=await Get<ComplaintAssignment[]>(basePath+"/"+complaint.Id+"/assignments",central);
Check(events.Count()==3&&events.All(e=>e.EventCanonicalJson==ComplaintRoutingPolicy.Canonical(e)&&e.EventSha256==ComplaintRoutingPolicy.Hash(e.EventCanonicalJson)&&e.AssignedAtUtc.Ticks%10==0),"Canonical bytes bind all typed fields and microsecond UTC timestamps after PostgreSQL roundtrip");

// Additional complete direct and DS handling paths, using the one already classified fictional complaint.
async Task<ComplaintHandlingView> View(Guid who)=>await Get<ComplaintHandlingView>(basePath+"/"+complaint.Id+"/actions",who);
async Task<HandlingActionResult> Act(string operation,Dictionary<string,string> details,string summary="",Guid? who=null,bool asCentral=true)
{
 var person=who??central;var view=asCentral?await View(person):await Get<ComplaintHandlingView>(registered+"/"+complaint.Id+"/actions",person);
 return await Post<HandlingActionResult>((asCentral?basePath:registered)+"/"+complaint.Id+"/actions",person,new ComplaintHandlingAction(Guid.NewGuid(),view.State.Revision,operation,"Reasoned isolated "+operation,details,[],summary));
}
var beforeDirect=await View(central);
await Act("TakeDirect",new(){["notes"]="Commissioner accepts responsibility for the isolated case."});
Check((await View(central)).State.Mode=="Commissioner","Unlinked complaint supports direct Commissioner responsibility without case ID");
Check((await Send(registered+"/"+complaint.Id,ds1)).StatusCode==HttpStatusCode.NotFound,"Direct takeover revokes historical DS access");
Check((await Get<RegisteredComplaint[]>(registered,ds1)).All(x=>x.Id!=complaint.Id),"DS list filters direct handling before pagination");
var note=await Act("RecordNote",new(){["notes"]="PRIVATE_INTERNAL_ONLY investigation note."});
var ownerHistory=await Get<ComplaintHandlingView>(registered+"/"+complaint.Id+"/actions",owner,"Applicant");
Check(!JsonSerializer.Serialize(ownerHistory).Contains("PRIVATE_INTERNAL_ONLY")&&ownerHistory.State.ResponsibleActorId==null,"Applicant-safe history excludes internal notes and private actor IDs");
Check((await View(central)).History.Any(x=>x.Details?.Values.Contains("PRIVATE_INTERNAL_ONLY investigation note.")==true),"Central action history preserves actual internal notes");
await Act("RequestInformation",new(){["notes"]="Request an actual source reference, without fabricating one."},"Please provide the relevant correspondence reference.");
Check((await View(central)).State.Status=="InformationRequested","Formal information request changes only handling status");
var unchanged=await Get<RegisteredComplaintDetail>(registered+"/"+complaint.Id,owner,"Applicant");
Check(unchanged.Complaint.ClassificationState=="Classified"&&unchanged.Complaint.ComplaintText==complaint.ComplaintText,"Handling never rewrites original/classification state");
await Act("StartInvestigation",new(){["notes"]="Considered the actual information available in the fixture."});
await Act("RecordFindings",new(){["findings"]="Reference information remains incomplete.",["actionsTaken"]="Recorded the limitation, without verifying allegations."});
var resolveDetails=new Dictionary<string,string>{["outcome"]="UnableToDetermine",["findings"]="Insufficient evidence to determine the allegation.",["actionsTaken"]="Recorded the actual test limitation.",["decisionBasis"]="No independent verification is available."};
var resolved=await Act("Resolve",resolveDetails,"Unable to determine on the available evidence.");
Check(resolved.Current.Status=="Resolved"&&resolved.Current.ResponsibleActorId==null,"Reasoned resolution has no active handler and preserves decision history");
var directResolveRequest=new ComplaintHandlingAction(Guid.NewGuid(),resolved.Current.Revision,"RecordNote","Terminal note rejected",new(){["notes"]="not allowed"},[],"");
Check((await Send(basePath+"/"+complaint.Id+"/actions",central,body:directResolveRequest)).StatusCode==HttpStatusCode.Conflict,"Terminal complaint rejects new investigation notes");
await Act("Close",new(),"Administrative closure after the recorded decision.");
var allowedBefore=settings.Grants[0].ReviewOperations;settings.Grants[0].ReviewOperations=allowedBefore.Where(x=>x!="Reopen").ToArray();
var closed=await View(central);
var reopenRequest=new ComplaintHandlingAction(Guid.NewGuid(),closed.State.Revision,"Reopen","New procedural reason",new(){["proceduralReason"]="Review the incomplete reference handling."},[],"Complaint reopened for further consideration.");
Check((await Send(basePath+"/"+complaint.Id+"/actions",central,body:reopenRequest)).StatusCode==HttpStatusCode.Forbidden,"Review permission alone cannot reopen without explicit operation");
settings.Grants[0].ReviewOperations=allowedBefore;
var reopened=await Post<HandlingActionResult>(basePath+"/"+complaint.Id+"/actions",central,reopenRequest);
Check(reopened.Current.Status=="Reopened"&&reopened.Current.Mode=="Commissioner","Explicit audited reopening returns responsibility to Commissioner");
Check((await Send(registered+"/"+complaint.Id,ds1)).StatusCode==HttpStatusCode.NotFound,"Reopening never restores former DS access");
var reopenReplay=await Post<HandlingActionResult>(basePath+"/"+complaint.Id+"/actions",central,reopenRequest);
Check(reopenReplay.Reused&&reopenReplay.EventId==reopened.EventId,"Same-key lifecycle replay creates no duplicate event");
Check((await Send(basePath+"/"+complaint.Id+"/actions",central,body:reopenRequest with{Reason="changed"})).StatusCode==HttpStatusCode.Conflict,"Changed lifecycle content under same key conflicts");
var assignmentCurrent=(await Get<ComplaintAssignment[]>(basePath+"/"+complaint.Id+"/assignments",central)).Last();
await Post<ComplaintAssignmentResult>(basePath+"/"+complaint.Id+"/assignments",central,new AssignComplaint(Guid.NewGuid(),assignmentCurrent.Revision,ds1,"Deliberately return to former DS after reopening",reopened.Current.Revision));
Check((await Send(registered+"/"+complaint.Id,ds1)).IsSuccessStatusCode,"Explicit new assignment to former DS restores only complaint scope");
await Act("StartInvestigation",new(){["notes"]="DS begins the permitted investigation."},who:ds1,asCentral:false);
var dsView=await Get<ComplaintHandlingView>(registered+"/"+complaint.Id+"/actions",ds1);
Check(!JsonSerializer.Serialize(dsView).Contains("PRIVATE_INTERNAL_ONLY"),"Current DS cannot read former Commissioner-private notes");
var forbiddenResolve=new ComplaintHandlingAction(Guid.NewGuid(),dsView.State.Revision,"Resolve","DS lacks final authority",resolveDetails,[],"No DS final authority.");
Check((await Send(registered+"/"+complaint.Id+"/actions",ds1,body:forbiddenResolve)).StatusCode==HttpStatusCode.Forbidden,"DS cannot use ordinary action route for central final decisions");
await Act("RecordFindings",new(){["findings"]="DS observed only the fictional supplied evidence.",["actionsTaken"]="Recorded findings for authorized central consideration."},who:ds1,asCentral:false);
await Act("Resolve",resolveDetails,"Central decision after DS investigation: unable to determine.");
Check((await Send(registered+"/"+complaint.Id,ds1)).StatusCode==HttpStatusCode.NotFound,"Resolution revokes DS reads/history/review/retry scope");
Check((await Get<CentralComplaintPage>(basePath+"?handling=terminal",central)).Items.Any(x=>x.Complaint.Id==complaint.Id),"Resolved complaint appears in server-filtered terminal queue");
// Two same-key commands serialize into one event and one replay.
await Act("Reopen",new(){["proceduralReason"]="Concurrency test with explicitly approved isolated fixture."},"Further review recorded.");
var concurrencyState=await View(central);
var sameKey=new ComplaintHandlingAction(Guid.NewGuid(),concurrencyState.State.Revision,"RecordNote","Concurrent identical action",new(){["notes"]="A single immutable note."},[],"");
var same=await Task.WhenAll(Post<HandlingActionResult>(basePath+"/"+complaint.Id+"/actions",central,sameKey),Post<HandlingActionResult>(basePath+"/"+complaint.Id+"/actions",central,sameKey));
Check(same.Count(x=>x.Reused)==1&&same[0].EventId==same[1].EventId,"Concurrent same-key actions have one insert and one replay");
var stale=sameKey with{SubmissionKey=Guid.NewGuid()};
Check((await Send(basePath+"/"+complaint.Id+"/actions",central,body:stale)).StatusCode==HttpStatusCode.Conflict,"Stale handling revision cannot mutate a reopened complaint");
var badEvidence=new ComplaintHandlingAction(Guid.NewGuid(),(await View(central)).State.Revision,"RecordNote","Invalid reference rollback",new(){["notes"]="Reference must exist for this complaint."},[new("C4Record",Guid.NewGuid().ToString(),"missing")],"");
var revisionBefore=(await View(central)).State.Revision;
Check((await Send(basePath+"/"+complaint.Id+"/actions",central,body:badEvidence)).StatusCode==HttpStatusCode.BadRequest&&(await View(central)).State.Revision==revisionBefore,"Rejected evidence leaves no handling row or revision change");
await using(var proofDb=new GovernanceIntelligenceDbContext(Options(runtime)))
{
 var allHandling=await proofDb.Set<ComplaintHandlingEntity>().AsNoTracking().ToArrayAsync();
 Check(allHandling.All(x=>x.EventCanonicalJson==x.Canonical()&&x.EventSha256==ComplaintRoutingPolicy.Hash(x.EventCanonicalJson)&&x.ActedAtUtc.Ticks%10==0),"Handling canonical bytes bind typed columns/details and normalized UTC after PostgreSQL roundtrip");
 Check(!await proofDb.Database.SqlQueryRaw<bool>("SELECT has_table_privilege(current_user,'governance_intelligence.complaint_handling_events','UPDATE') OR has_table_privilege(current_user,'governance_intelligence.complaint_handling_events','DELETE') AS \"Value\"").SingleAsync(),"Handling runtime has SELECT/INSERT only, no UPDATE/DELETE");
}
Check(counted.Calls==1,"All direct/DS decisions, replay and history reads invoke no additional classifier or anomaly model");
await using(var db=new GovernanceIntelligenceDbContext(Options(runtime)))
{
 Check(!await db.Database.SqlQueryRaw<bool>("SELECT has_table_privilege(current_user,'governance_intelligence.complaint_assignment_events','UPDATE') AS \"Value\"").SingleAsync(),"No new table UPDATE permission");
 Check(!await db.Database.SqlQueryRaw<bool>("SELECT has_table_privilege(current_user,'governance_intelligence.complaint_assignment_events','DELETE') AS \"Value\"").SingleAsync(),"No new table DELETE permission");
 try{await db.Database.ExecuteSqlRawAsync("DELETE FROM governance_intelligence.complaint_assignment_events");throw new Exception("unexpected DELETE");}catch(PostgresException e)when(e.SqlState=="42501"){checks.Add("Actual restricted DELETE denied");}
 Check((await db.Set<RegisteredComplaintEntity>().CountAsync())==2&&await db.ComplaintClassificationAssessments.CountAsync()==1,"No duplicate aggregate or model assessment");
 Check((await db.GovernanceAuditAnchorOutbox.CountAsync())==0,"Routing creates no blockchain/outbox entries");
}
await VerifyDirectoryAsync(connection,central,ds1,ds2,checks);
var finalHandling=(await View(central)).History.Select(x=>new{x.Id,x.Revision,x.Operation,x.Status}).ToArray();
await app.StopAsync();await app.DisposeAsync();http.Dispose();
Console.WriteLine(JsonSerializer.Serialize(new{Status="Passed",Database=target.Database,Port=target.Port,Checks=checks,ActualClassifierCalls=counted.Calls,IsolatedComplaintId=complaint.Id,AssignmentIds=events.Select(x=>x.Id),HandlingEvents=finalHandling},new JsonSerializerOptions{WriteIndented=true}));

static async Task VerifyStorageAsync(string admin)
{
 var runtime=new NpgsqlConnectionStringBuilder(admin){Username="central_runtime",Password="isolated-runtime-only"}.ConnectionString;
 var options=new DbContextOptionsBuilder<GovernanceIntelligenceDbContext>().UseNpgsql(runtime).Options;
 await using var db=new GovernanceIntelligenceDbContext(options);
 var checks=new List<string>();
 void Check(bool yes,string label){if(!yes)throw new Exception(label);checks.Add(label);}
 Check(await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM information_schema.columns WHERE table_schema='governance_intelligence' AND table_name='complaint_assignment_events'").SingleAsync()==16,"Exactly sixteen approved columns");
 Check(await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM pg_constraint WHERE conrelid='governance_intelligence.complaint_assignment_events'::regclass AND contype='f' AND confdeltype='r'").SingleAsync()==2,"Both FKs restrictive");
 Check(await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM pg_indexes WHERE schemaname='governance_intelligence' AND tablename='complaint_assignment_events'").SingleAsync()==6,"Four PK/unique indexes and exactly two reviewed query indexes");
 Check(!await db.Database.SqlQueryRaw<bool>("SELECT has_schema_privilege(current_user,'governance_intelligence','CREATE') AS \"Value\"").SingleAsync(),"Runtime has no schema CREATE");
 Check(!await db.Database.SqlQueryRaw<bool>("SELECT (rolsuper OR rolcreaterole OR rolcreatedb) AS \"Value\" FROM pg_roles WHERE rolname=current_user").SingleAsync(),"Runtime has no superuser or role/database creation");
 var source=await db.Set<ComplaintAssignmentEntity>().AsNoTracking().OrderBy(x=>x.ComplaintId).ThenByDescending(x=>x.Revision).FirstAsync();
 async Task Reject(string sql,string state,string label)
 {
  await using var tx=await db.Database.BeginTransactionAsync();
  try{await db.Database.ExecuteSqlRawAsync(sql);throw new Exception("Unexpected constraint success: "+label);}catch(PostgresException e)when(e.SqlState==state){checks.Add(label);}
  await tx.RollbackAsync();
 }
 const string cols="\"Id\",\"ComplaintId\",\"Revision\",\"TargetActorId\",\"TargetOfficeCode\",\"AssignedByActorId\",\"AssignedAtUtc\",\"Reason\",\"SubmissionKey\",\"InputSha256\",\"PreviousAssignmentId\",\"DirectorySnapshotSha256\",\"AuthorizationGrantId\",\"AuthorizationSourceReference\",\"EventCanonicalJson\",\"EventSha256\"";
 // Static adversarial fixtures only, no interpolated browser/user data.
 var select=string.Join(",",cols.Split(','));
 await Reject("INSERT INTO governance_intelligence.complaint_assignment_events ("+cols+") SELECT "+select+" FROM governance_intelligence.complaint_assignment_events LIMIT 1","23505","Database unique constraints reject duplicate event");
 await Reject("INSERT INTO governance_intelligence.complaint_assignment_events ("+cols+") SELECT "+select.Replace("\"Reason\"","' '")+" FROM governance_intelligence.complaint_assignment_events LIMIT 1","23514","Database rejects blank reason");
 await Reject("INSERT INTO governance_intelligence.complaint_assignment_events ("+cols+") SELECT "+select.Replace("\"EventSha256\"","'bad-hash'")+" FROM governance_intelligence.complaint_assignment_events LIMIT 1","23514","Database rejects malformed digest");
 var saved=source.ToRecord();
 db.Attach(source);source.Reason="attempted overwrite";
 try{await db.SaveChangesAsync();throw new Exception("Unexpected mutation");}catch(InvalidOperationException e)when(e.Message.Contains("append-only")){checks.Add("EF rejects history UPDATE before SQL");db.ChangeTracker.Clear();}
 var current=await db.Set<ComplaintAssignmentEntity>().AsNoTracking().SingleAsync(x=>x.Id==saved.Id);
 Check(current.Reason==saved.Reason&&current.EventSha256==saved.EventSha256,"Failed mutation and rejected inserts preserve event bytes");
 var results=await db.ComplaintClassificationAssessments.AsNoTracking().Select(x=>new{x.AssessmentId,x.ModelVersion,x.PredictedCategory,x.AdministrativeProceduralIntegrityProbability,x.LeaseRevenuePaymentEnforcementProbability,x.UnauthorizedAllocationTransferUseProbability,x.ProtectedEnvironmentalLeaseMisuseProbability}).ToArrayAsync();
 Console.WriteLine(JsonSerializer.Serialize(new{Status="Passed",Checks=checks,ActualSavedClassifications=results},new JsonSerializerOptions{WriteIndented=true}));
}

static async Task VerifyDirectoryAsync(string admin,Guid central,Guid ds1,Guid ds2,List<string> checks)
{
 var c=new NpgsqlConnectionStringBuilder(admin){Database="c4_handling_accounts_test"};
 await using(var connection=new NpgsqlConnection(admin)) { await connection.OpenAsync();await using var cmd=new NpgsqlCommand("CREATE DATABASE c4_handling_accounts_test",connection);await cmd.ExecuteNonQueryAsync(); }
 var options=new DbContextOptionsBuilder<StateLandGovernance.Api.Security.PortalAccountDbContext>().UseNpgsql(c.ConnectionString).Options;
 await using(var db=new StateLandGovernance.Api.Security.PortalAccountDbContext(options))
 {
  await db.Database.EnsureCreatedAsync();
  foreach(var pair in new[]{(central,"COMMISSIONER"),(ds1,"DS"),(ds2,"DS")})db.Accounts.Add(new(){Username=pair.Item1.ToString(),ActorId=pair.Item1,ProfileJson=JsonSerializer.Serialize(new{ActorId=pair.Item1,TokenRole="Officer",Institution=pair.Item2})});
  db.Settings.Add(new(){Key="WorkflowGovernance",Json=JsonSerializer.Serialize(new{IntakeReviewers=new[]{new{ActorId=ds1,DisplayName="DS One",OfficeCode="TEST-A"},new{ActorId=ds2,DisplayName="DS Two",OfficeCode="TEST-B"}}})});await db.SaveChangesAsync();
 }
 var previous=Environment.GetEnvironmentVariable("PORTAL_ACCOUNTS_CONNECTION");
 try
 {
  // Process-local test setting only; never writes ordinary configuration.
  Environment.SetEnvironmentVariable("PORTAL_ACCOUNTS_CONNECTION",c.ConnectionString);
  var adapter=new StateLandGovernance.Api.Security.CentralComplaintDirectory(new ConfigurationBuilder().Build());
  var first=await adapter.ReadAsync(default);
  if(first.Officers.Length!=2||!await adapter.IsCommissionerAsync(central,default))throw new Exception("Actual host directory fixture failure");
  await using(var db=new StateLandGovernance.Api.Security.PortalAccountDbContext(options))
  {var profile=await db.Accounts.SingleAsync(x=>x.ActorId==ds2);profile.ProfileJson=JsonSerializer.Serialize(new{ActorId=ds2,TokenRole="Officer",Institution="OTHER"});await db.SaveChangesAsync();}
  var second=await adapter.ReadAsync(default);
  if(second.Officers.Length!=1||second.Sha256==first.Sha256)throw new Exception("Host directory failed fresh revocation");
  checks.Add("Real host directory reads isolated accounts/settings and observes profile revocation without restart");
 }
 finally{Environment.SetEnvironmentVariable("PORTAL_ACCOUNTS_CONNECTION",previous);}
}

sealed class Directory(Guid central,Guid ds1,Guid ds2):IComplaintOfficerDirectory
{
 public bool Revoked;
 public Task<bool> IsCommissionerAsync(Guid actor,CancellationToken ct)=>Task.FromResult(actor==central);
 public Task<ComplaintDirectorySnapshot> ReadAsync(CancellationToken ct){ComplaintOfficer[] rows=Revoked?[new(ds1,"DS One","TEST-A")]:[new(ds1,"DS One","TEST-A"),new(ds2,"DS Two","TEST-B")];return Task.FromResult(new ComplaintDirectorySnapshot(rows,ComplaintRoutingPolicy.Hash(JsonSerializer.Serialize(rows))));}
}
sealed class References:IComplaintReferenceAccess
{
 public Task<ComplaintReference> ValidateAsync(string? c,string? p,CancellationToken ct)=>c!=null||p!=null?throw new ArgumentException("Unknown test reference"):Task.FromResult(new ComplaintReference(null,null));
 public Task<IReadOnlyList<Guid>> ReviewerCasesAsync(Guid actor,CancellationToken ct)=>throw new Exception("C3 reviewer fallback must never be called");
}
sealed class Counted(IComplaintClassificationClient inner):IComplaintClassificationClient
{
 public int Calls;
 public Task<ComplaintClassificationResult> ClassifyAsync(string text,string? caseId=null,CancellationToken ct=default){Interlocked.Increment(ref Calls);return inner.ClassifyAsync(text,caseId,ct);}
}
sealed class HttpActor(Microsoft.AspNetCore.Http.IHttpContextAccessor http):IComplaintActorContext
{public Guid ActorId=>Guid.TryParse(http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)?id:Guid.Empty;}
sealed class ProofAuth(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
{
 protected override Task<AuthenticateResult> HandleAuthenticateAsync()=>Task.FromResult(Guid.TryParse(Request.Headers["X-Proof-Actor"],out var actor)?AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,actor.ToString()),new Claim(ClaimTypes.Role,Request.Headers["X-Proof-Role"].ToString())],"proof")),"proof")):AuthenticateResult.NoResult());
}

sealed class ReviewClock : TimeProvider { public DateTimeOffset Now {get;set;} public override DateTimeOffset GetUtcNow()=>Now; }
sealed class FaultSwitch { public bool Assignment,Review; }
sealed class FaultHandling(IComplaintHandlingStore inner,FaultSwitch faults):IComplaintHandlingStore
{
 public Task<(IReadOnlyList<RegisteredComplaint> Items,string? Next)> QueueAsync(string filter,string? cursor,CancellationToken ct)=>inner.QueueAsync(filter,cursor,ct);
 public Task<HandlingState> CurrentAsync(Guid id,CancellationToken ct)=>inner.CurrentAsync(id,ct);
 public Task<bool> IsAssignedAsync(Guid actor,Guid id,CancellationToken ct)=>inner.IsAssignedAsync(actor,id,ct);
 public Task<ComplaintHandlingView> ViewAsync(Guid id,bool central,bool officer,CancellationToken ct)=>inner.ViewAsync(id,central,officer,ct);
 public Task<HandlingActionResult> ActAsync(Guid id,ComplaintHandlingAction input,bool central,CancellationToken ct)=>inner.ActAsync(id,input,central,ct);
 public Task<HandlingState> PrepareAssignmentAsync(Guid id,AssignComplaint input,CancellationToken ct)=>inner.PrepareAssignmentAsync(id,input,ct);
 public Task RecordAssignmentAsync(HandlingState previous,ComplaintAssignment assignment,AssignComplaint input,CancellationToken ct)=>faults.Assignment?throw new InvalidOperationException("Injected isolated handling failure"):inner.RecordAssignmentAsync(previous,assignment,input,ct);
 public Task RecordReviewAsync(Guid id,HandlingState previous,ComplaintReview review,CancellationToken ct)=>faults.Review?throw new InvalidOperationException("Injected isolated review failure"):inner.RecordReviewAsync(id,previous,review,ct);
}
