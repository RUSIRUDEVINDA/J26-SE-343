using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using StateLandGovernance.Api.Security;
using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
using StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;
using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Presentation.Security;

var connection=Environment.GetEnvironmentVariable("C4_PERSISTENT_AUTH_ISOLATED_ACCOUNTS")??throw new Exception("Explicit isolated identity connection required");
var c=new NpgsqlConnectionStringBuilder(connection);
if(c.Host!="127.0.0.1"||c.Port!=57657||c.Database!="c4_persistent_auth_accounts_20261023")throw new Exception("Refusing ordinary/non-test identity target");
var actor=Guid.Parse("0ab669fe-d0ab-5695-ad55-10f6b3b5aab3");var other=Guid.Parse("22222222-0000-4000-8000-000000000001");var ds=Guid.NewGuid();var owner=Guid.NewGuid();
var checks=new List<string>();void Check(bool yes,string name){if(!yes)throw new Exception(name);checks.Add(name);}
var accountOptions=new DbContextOptionsBuilder<PortalAccountDbContext>().UseNpgsql(connection).Options;
var profile=new Dictionary<string,string?>{{"Username","isolated-commissioner"},{"ActorId",actor.ToString()},{"TokenRole","Officer"},{"Institution","COMMISSIONER"},{"DisplayName","Isolated Commissioner"}};
PortalAccountStore.SetDemoPassword(profile); // NEW isolated fixture only; never existing accounts/passwords.
await using(var db=new PortalAccountDbContext(accountOptions))
{
 await db.Database.EnsureCreatedAsync();
 if(await db.Accounts.AnyAsync())throw new Exception("Fresh isolated identity database required");
 db.Accounts.Add(new(){Username=profile["Username"]!,ActorId=actor,ProfileJson=JsonSerializer.Serialize(profile)});
 foreach(var pair in new[]{(other,"COMMISSIONER","other"),(ds,"DS","ds"),(owner,"APPLICANT","applicant")})
 {
  var p=new Dictionary<string,string?>(profile){["Username"]=pair.Item3,["ActorId"]=pair.Item1.ToString(),["Institution"]=pair.Item2,["TokenRole"]=pair.Item2=="APPLICANT"?"Applicant":"Officer"};
  db.Accounts.Add(new(){Username=pair.Item3,ActorId=pair.Item1,ProfileJson=JsonSerializer.Serialize(p)});
 }
 db.Settings.Add(new(){Key="WorkflowGovernance",Json=JsonSerializer.Serialize(new{IntakeReviewers=new[]{new{ActorId=ds,OfficeCode="ISOLATED-DS"}}})});await db.SaveChangesAsync();
}
var folder=Path.Combine(Path.GetTempPath(),"slg-persistent-auth-proof-"+Guid.NewGuid().ToString("N"));System.IO.Directory.CreateDirectory(folder);var path=Path.Combine(folder,"permissions.json");
var grant=new CentralComplaintGrant(actor,Guid.NewGuid(),"COMMISSIONER",[ComplaintRoutingPolicy.Read,ComplaintRoutingPolicy.Assign,ComplaintRoutingPolicy.Review],DateTimeOffset.UtcNow.AddMinutes(-1),null,"Explicit isolated persistent authorization") {Persistent=true,ReviewOperations=ComplaintHandlingRules.ReviewOperations};
async Task Write(CentralComplaintGrant[] grants)=>await File.WriteAllTextAsync(path,JsonSerializer.Serialize(new CentralComplaintGrantFile.Document("c4-central-permissions/1",grants)));
await Write([grant]);
var options=new CentralComplaintOptions{Enabled=true,WritesEnabled=true,GrantFilePath=path};
var oldAccountConnection=Environment.GetEnvironmentVariable("PORTAL_ACCOUNTS_CONNECTION");Environment.SetEnvironmentVariable("PORTAL_ACCOUNTS_CONNECTION",connection);
try
{
 var b=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName="Development",Args=[]});b.Logging.ClearProviders();b.WebHost.UseUrls("http://127.0.0.1:0");
 var values=new Dictionary<string,string?>{{"PortalAuthentication:Enabled","true"}};
 var profiles=new[]{profile,new Dictionary<string,string?>(profile){["Username"]="other",["ActorId"]=other.ToString()},new Dictionary<string,string?>(profile){["Username"]="ds",["ActorId"]=ds.ToString(),["Institution"]="DS"},new Dictionary<string,string?>(profile){["Username"]="applicant",["ActorId"]=owner.ToString(),["TokenRole"]="Applicant",["Institution"]="APPLICANT"}};
 for(int i=0;i<profiles.Length;i++)foreach(var x in profiles[i])values[$"LocalDevelopmentSignIn:Accounts:{i}:{x.Key}"]=x.Value;
 b.Configuration.AddInMemoryCollection(values);
 b.Services.AddSingleton(options);b.Services.AddSingleton<TimeProvider>(TimeProvider.System);b.Services.AddHttpContextAccessor();
 b.Services.AddScoped<IComplaintCentralGrantSource,CentralComplaintGrantFile>();b.Services.AddScoped<IComplaintOfficerDirectory,CentralComplaintDirectory>();b.Services.AddScoped<IComplaintActorContext,ComplaintHttpActor>();b.Services.AddScoped<ComplaintRoutingPolicy>();
 b.Services.AddScoped<CentralComplaintService>();b.Services.AddScoped<IComplaintRoutingStore,ComplaintRoutingStore>();b.Services.AddScoped<IRegisteredComplaintStore,RegisteredComplaintStore>();b.Services.AddScoped<IComplaintHandlingStore,ComplaintHandlingStore>();b.Services.AddScoped<IComplaintClassificationAssessmentStore,PostgresComplaintClassificationAssessmentStore>();
 // Capabilities never queries module tables. This context is deliberately the isolated identity target,
 // so an accidental C4 storage query fails rather than ever reaching ordinary data.
 b.Services.AddDbContext<GovernanceIntelligenceDbContext>(o=>o.UseNpgsql(connection));
 b.Services.AddSingleton(new LandIntelligenceJwtTokenService(Options.Create(new LandIntelligenceAuthenticationOptions{Issuer="isolated-proof",Audience="isolated-proof",JwtSigningKeyBase64=Convert.ToBase64String(new byte[32])})));
 b.Services.AddSingleton<DevelopmentAccounts>();
 b.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();b.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<LandIntelligenceJwtTokenService,DevelopmentAccounts>((o,tokens,accounts)=>{o.TokenValidationParameters=tokens.CreateValidationParameters();o.Events=new(){OnTokenValidated=context=>{if(!accounts.Validate(context.HttpContext,context.Principal!))context.Fail("Invalid live session");return Task.CompletedTask;}};});
 b.Services.AddAuthorization();b.Services.AddControllers().AddApplicationPart(typeof(CentralComplaintsController).Assembly);
 var app=b.Build();app.UseAuthentication();app.UseAuthorization();app.MapControllers();
 app.MapPost("/api/auth/sign-in",(StateLandGovernance.Api.Controllers.DevelopmentAuthController.Credentials credentials,DevelopmentAccounts accounts)=>accounts.SignIn(credentials.Username,credentials.Password) is {} result?Microsoft.AspNetCore.Http.Results.Ok(result):Microsoft.AspNetCore.Http.Results.Unauthorized());
 await app.StartAsync();var http=new HttpClient{BaseAddress=new(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single())};
 async Task<string> Login(string name){using var r=await http.PostAsJsonAsync("/api/auth/sign-in",new{Username=name,Password=DemoAccountProfiles.Password});r.EnsureSuccessStatusCode();return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;}
 async Task<HttpResponseMessage> Cap(string? token){using var request=new HttpRequestMessage(HttpMethod.Get,"/api/governance-intelligence/central-complaints/capabilities");if(token!=null)request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);return await http.SendAsync(request);}
 Check((await Cap(null)).StatusCode==HttpStatusCode.Unauthorized,"Anonymous capabilities denied");
 var token=await Login("isolated-commissioner");var result=await Cap(token);Check(result.IsSuccessStatusCode,"Existing sign-in/password verification/live JWT session yields persistent capabilities");
 var caps=await result.Content.ReadFromJsonAsync<JsonElement>();Check(caps.GetProperty("canRead").GetBoolean()&&caps.GetProperty("canAssign").GetBoolean()&&caps.GetProperty("canReview").GetBoolean()&&caps.GetProperty("reviewOperations").GetArrayLength()==9,"Exact central permissions and operations returned");
 Check((await Cap(await Login("other"))).StatusCode==HttpStatusCode.Forbidden,"Other Commissioner is not automatically authorized");
 Check((await Cap(await Login("ds"))).StatusCode==HttpStatusCode.Forbidden,"DS Officer cannot inherit central permissions");
 Check((await Cap(await Login("applicant"))).StatusCode==HttpStatusCode.Forbidden,"Applicant cannot access central capabilities");
 await Write([grant with{Revoked=true}]);Check((await Cap(token)).StatusCode==HttpStatusCode.Forbidden,"Same live JWT immediately denied after file revocation without restart");
 await Write([grant with{Persistent=false,ExpiresAtUtc=DateTimeOffset.UtcNow.AddSeconds(-1)}]);Check((await Cap(token)).StatusCode==HttpStatusCode.Forbidden,"Historical expired timed grant remains denied");
 await Write([]);Check((await Cap(token)).StatusCode==HttpStatusCode.Forbidden,"Removed server grant denies same live session");
 await Write([grant]);
 await using(var db=new PortalAccountDbContext(accountOptions)){var row=await db.Accounts.SingleAsync(x=>x.ActorId==actor);var p=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(row.ProfileJson)!;p["IsActive"]=JsonSerializer.SerializeToElement(false);row.ProfileJson=JsonSerializer.Serialize(p);await db.SaveChangesAsync();}
 Check((await Cap(token)).StatusCode==HttpStatusCode.Forbidden,"Fresh inactive account metadata revokes same live JWT without restart");
 await using(var db=new PortalAccountDbContext(accountOptions)){var row=await db.Accounts.SingleAsync(x=>x.ActorId==actor);row.ProfileJson=JsonSerializer.Serialize(profile);await db.SaveChangesAsync();}
 Check((await Cap(token)).IsSuccessStatusCode,"Explicit isolated account reactivation plus unrevoked grant restores eligibility");
 await File.WriteAllTextAsync(path,"{\"SchemaVersion\":\"c4-central-permissions/1\",\"Grants\":[],\"Grants\":[]}");Check((await Cap(token)).StatusCode==HttpStatusCode.ServiceUnavailable,"Duplicate permission document properties fail closed without old-grant fallback");
 await Write([grant]);using(var scope=app.Services.CreateScope()){var policy=scope.ServiceProvider.GetRequiredService<ComplaintRoutingPolicy>();foreach(var op in ComplaintHandlingRules.ReviewOperations)await policy.RequireReviewAsync(actor,op,default);var authorized=await policy.RequireCentralAsync(actor,false,default);Check(authorized.SourceReference.Contains("grant-document-sha256="),"Permission-file hash accompanies grant/source provenance for existing action audit events");}
 await app.StopAsync();await app.DisposeAsync();http.Dispose();
 Console.WriteLine(JsonSerializer.Serialize(new{Status="Passed",Checks=checks,IsolatedIdentityDatabase=c.Database,OrdinaryAccountChanges=false,ModelCalls=0,GrantExpiresAtUtc=grant.ExpiresAtUtc,GrantPersistent=grant.Persistent},new JsonSerializerOptions{WriteIndented=true}));
}
finally{Environment.SetEnvironmentVariable("PORTAL_ACCOUNTS_CONNECTION",oldAccountConnection);}
