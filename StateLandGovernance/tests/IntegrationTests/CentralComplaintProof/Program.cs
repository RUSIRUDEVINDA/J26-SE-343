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
var connection=Environment.GetEnvironmentVariable("C4_CENTRAL_ISOLATED_CONNECTION")??throw new Exception("Explicit isolated connection required");
var target=new NpgsqlConnectionStringBuilder(connection);
if(target.Host!="127.0.0.1"||target.Port!=57657||target.Database is not ("c4_central_test" or "c4_central_test_final"))throw new Exception("Refusing non-test target");
if(args.Contains("--verify-existing")) { await VerifyStorageAsync(connection); return; }
var checks=new List<string>();
void Check(bool yes,string label){if(!yes)throw new Exception("FAILED: "+label);checks.Add(label);}
DbContextOptions<GovernanceIntelligenceDbContext> Options(string c)=>new DbContextOptionsBuilder<GovernanceIntelligenceDbContext>().UseNpgsql(c,p=>p.MigrationsHistoryTable("__ef_migrations_history","governance_intelligence")).Options;
var owner=Guid.Parse("11111111-0000-4000-8000-000000000001");
var central=Guid.Parse("11111111-0000-4000-8000-000000000002");
var ds1=Guid.Parse("11111111-0000-4000-8000-000000000003");
var ds2=Guid.Parse("11111111-0000-4000-8000-000000000004");
var foreign=Guid.Parse("11111111-0000-4000-8000-000000000005");
var dir=new Directory(central,ds1,ds2);
var clock=TimeProvider.System;
var settings=new CentralComplaintOptions{Enabled=true,WritesEnabled=true,Grants=[new(central,Guid.NewGuid(),"COMMISSIONER",[ComplaintRoutingPolicy.Read,ComplaintRoutingPolicy.Assign],clock.GetUtcNow().AddHours(-1),clock.GetUtcNow().AddHours(2),"isolated fixture authorization")]};
var preserved=new RegisteredComplaint(Guid.NewGuid(),owner,Guid.NewGuid(),"original-fingerprint","Preserved isolated original","Fictional test concern; no real person is alleged.",null,null,DateTimeOffset.UtcNow,0,"Pending","Open",[],[]);
await using(var db=new GovernanceIntelligenceDbContext(Options(connection)))
{
 if((await db.Database.GetAppliedMigrationsAsync()).Any())throw new Exception("Fresh test database required; no reset supported");
 await db.GetService<IMigrator>().MigrateAsync("20261022152556_AddOfficerInvestigationsAndAssessmentRequests");
 await new RegisteredComplaintStore(db,new PostgresComplaintClassificationAssessmentStore(db)).InsertAsync(preserved,default);
 await db.GetService<IMigrator>().MigrateAsync("20261022200907_AddCentralComplaintAssignments");
 Check(await db.Set<ComplaintAssignmentEntity>().CountAsync()==0,"Additive migration creates empty assignment table; no backfill");
 Check((await new RegisteredComplaintStore(db,new PostgresComplaintClassificationAssessmentStore(db)).GetAsync(preserved.Id,default))!.ComplaintText==preserved.ComplaintText,"Pre-migration original payload preserved");
 // Reconstruct existing restricted rights in isolated storage, then apply only the approved new-table delta.
 await db.Database.ExecuteSqlRawAsync("""
 DO $$ BEGIN IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname='central_runtime') THEN CREATE ROLE central_runtime LOGIN PASSWORD 'isolated-runtime-only'; END IF; END $$;

 GRANT USAGE ON SCHEMA governance_intelligence TO central_runtime;
 GRANT SELECT ON ALL TABLES IN SCHEMA governance_intelligence TO central_runtime;
 GRANT INSERT ON governance_intelligence.registered_complaints, governance_intelligence.registered_complaint_classifications, governance_intelligence.complaint_classification_assessments TO central_runtime;
 GRANT UPDATE ("Revision","PayloadJson") ON governance_intelligence.registered_complaints TO central_runtime;
 GRANT SELECT, INSERT ON governance_intelligence.complaint_assignment_events TO central_runtime;
 """);
}
var runtime=new NpgsqlConnectionStringBuilder(connection){Username="central_runtime",Password="isolated-runtime-only"}.ConnectionString;
var counted=new Counted(new HttpComplaintClassificationClient(new HttpClient{BaseAddress=new("http://127.0.0.1:8104/"),Timeout=TimeSpan.FromSeconds(45)},NullLogger<HttpComplaintClassificationClient>.Instance));
async Task<WebApplication> Host()
{
 var b=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName="Testing",Args=[]});b.Logging.ClearProviders();b.WebHost.UseUrls("http://127.0.0.1:0");
 b.Configuration.AddInMemoryCollection(new Dictionary<string,string?>{["GovernanceIntelligence:Complaints:Enabled"]="true",["GovernanceIntelligence:Complaints:WritesEnabled"]="true"});
 b.Services.AddScoped<ComplaintAssignmentAuditInterceptor>();
 b.Services.AddDbContext<GovernanceIntelligenceDbContext>((sp,o)=>o.UseNpgsql(runtime).AddInterceptors(sp.GetRequiredService<ComplaintAssignmentAuditInterceptor>()));
 b.Services.AddSingleton(settings);b.Services.AddSingleton<IComplaintOfficerDirectory>(dir);b.Services.AddSingleton(clock);
 b.Services.AddHttpContextAccessor();b.Services.AddScoped<IComplaintActorContext,HttpActor>();
 b.Services.AddScoped<ComplaintRoutingPolicy>();b.Services.AddScoped<IComplaintRoutingStore,ComplaintRoutingStore>();b.Services.AddScoped<CentralComplaintService>();
 b.Services.AddScoped<IRegisteredComplaintStore,RegisteredComplaintStore>();b.Services.AddScoped<IComplaintClassificationAssessmentStore,PostgresComplaintClassificationAssessmentStore>();
 b.Services.AddSingleton<IComplaintReferenceAccess,References>();b.Services.AddSingleton<IComplaintClassificationClient>(counted);b.Services.AddScoped<RegisteredComplaintService>();
 b.Services.AddAuthentication("proof").AddScheme<AuthenticationSchemeOptions,ProofAuth>("proof",_=>{});b.Services.AddAuthorization();
 b.Services.AddControllers().AddApplicationPart(typeof(CentralComplaintsController).Assembly);
 var app=b.Build();app.UseAuthentication();app.UseAuthorization();app.MapControllers();await app.StartAsync();return app;
}
var app=await Host();
HttpClient Client(WebApplication host)=>new(){BaseAddress=new(host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single())};
var http=Client(app);
async Task<HttpResponseMessage> Send(string path,Guid? actor=null,string role="Officer",object? body=null)
{
 using var request=new HttpRequestMessage(body==null?HttpMethod.Get:HttpMethod.Post,path);
 if(actor!=null){request.Headers.Add("X-Proof-Actor",actor.ToString());request.Headers.Add("X-Proof-Role",role);}
 if(body!=null)request.Content=JsonContent.Create(body);
 return await http.SendAsync(request);
}
async Task<T> Get<T>(string path,Guid actor,string role="Officer")
{using var response=await Send(path,actor,role);response.EnsureSuccessStatusCode();return (await response.Content.ReadFromJsonAsync<T>())!;}
async Task<T> Post<T>(string path,Guid actor,object body,string role="Officer")
{using var response=await Send(path,actor,role,body);response.EnsureSuccessStatusCode();return (await response.Content.ReadFromJsonAsync<T>())!;}
const string basePath="/api/governance-intelligence/central-complaints",registered="/api/governance-intelligence/registered-complaints";
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
await using(var db=new GovernanceIntelligenceDbContext(Options(runtime)))
{
 Check(!await db.Database.SqlQueryRaw<bool>("SELECT has_table_privilege(current_user,'governance_intelligence.complaint_assignment_events','UPDATE') AS \"Value\"").SingleAsync(),"No new table UPDATE permission");
 Check(!await db.Database.SqlQueryRaw<bool>("SELECT has_table_privilege(current_user,'governance_intelligence.complaint_assignment_events','DELETE') AS \"Value\"").SingleAsync(),"No new table DELETE permission");
 try{await db.Database.ExecuteSqlRawAsync("DELETE FROM governance_intelligence.complaint_assignment_events");throw new Exception("unexpected DELETE");}catch(PostgresException e)when(e.SqlState=="42501"){checks.Add("Actual restricted DELETE denied");}
 Check((await db.Set<RegisteredComplaintEntity>().CountAsync())==2&&await db.ComplaintClassificationAssessments.CountAsync()==1,"No duplicate aggregate or model assessment");
 Check((await db.GovernanceAuditAnchorOutbox.CountAsync())==0,"Routing creates no blockchain/outbox entries");
}
await VerifyDirectoryAsync(connection,central,ds1,ds2,checks);
await app.StopAsync();await app.DisposeAsync();http.Dispose();
Console.WriteLine(JsonSerializer.Serialize(new{Status="Passed",Database=target.Database,Port=target.Port,Checks=checks,ActualClassifierCalls=counted.Calls,IsolatedComplaintId=complaint.Id,AssignmentIds=events.Select(x=>x.Id)},new JsonSerializerOptions{WriteIndented=true}));

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
 var c=new NpgsqlConnectionStringBuilder(admin){Database="c4_central_accounts_test"};
 await using(var connection=new NpgsqlConnection(admin)) { await connection.OpenAsync();await using var cmd=new NpgsqlCommand("CREATE DATABASE c4_central_accounts_test",connection);await cmd.ExecuteNonQueryAsync(); }
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
