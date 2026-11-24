using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
using StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;
var checks=new List<string>();void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);}
var builder=WebApplication.CreateBuilder();builder.WebHost.UseUrls("http://127.0.0.1:0");builder.Logging.ClearProviders();builder.Services.AddSingleton<IComplaintOfficerDirectory,newDirectory>();builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions,TestAuth>("test",_=>{});builder.Services.AddAuthorization();builder.Services.AddControllers().AddApplicationPart(typeof(GovernanceAccessController).Assembly);
await using var app=builder.Build();app.UseAuthentication();app.UseAuthorization();app.MapControllers();await app.StartAsync();var address=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();using var client=new HttpClient{BaseAddress=new Uri(address)};
client.DefaultRequestHeaders.Add("X-Test-Role","Applicant");
var routes=((IEndpointRouteBuilder)app).DataSources.SelectMany(x=>x.Endpoints).OfType<RouteEndpoint>().ToArray();var evidence=new List<object>();
foreach(var route in routes){var text=route.RoutePattern.RawText!;if(!text.StartsWith("api/governance-intelligence",StringComparison.Ordinal)||text.StartsWith("api/governance-intelligence/registered-complaints",StringComparison.Ordinal))continue;
 var allowed=route.Metadata.GetOrderedMetadata<IAuthorizeData>();Check(allowed.Count>0,"Endpoint has server authorization: "+text);
 var sample=System.Text.RegularExpressions.Regex.Replace(text,"\\{[^}]+\\}","11111111-0000-4000-8000-000000000001");foreach(var method in route.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods){using var request=new HttpRequestMessage(new HttpMethod(method),"/"+sample);if(method!="GET")request.Content=new StringContent("{}",Encoding.UTF8,"application/json");using var response=await client.SendAsync(request);Check(response.StatusCode==HttpStatusCode.Forbidden,"Applicant denied before protected handler: "+method+" "+text);evidence.Add(new{Method=method,Route=text,ApplicantStatus=(int)response.StatusCode});}}
var complaints=routes.Where(r=>r.RoutePattern.RawText=="api/governance-intelligence/registered-complaints").ToArray();Check(complaints.Length>0&&complaints.All(r=>r.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(x=>x.Roles?.Contains("Applicant")==true)),"Applicant-specific complaint endpoint remains deliberately authorized for ownership checks");
client.DefaultRequestHeaders.Remove("X-Test-Role");Check((await client.GetAsync("/api/governance-intelligence/access")).StatusCode==HttpStatusCode.Unauthorized,"Anonymous route admission denied401");client.DefaultRequestHeaders.Add("X-Test-Role","Officer");using var admitted=await client.GetAsync("/api/governance-intelligence/access");Check(admitted.StatusCode==HttpStatusCode.OK,"Existing officer role admitted without changing report/complaint scopes");await app.StopAsync();
Console.WriteLine(JsonSerializer.Serialize(new{Status="Passed",Checks=checks,ProtectedEndpointChecks=evidence,ModelCalls=0,DatabaseConnections=0},new JsonSerializerOptions{WriteIndented=true}));
sealed class newDirectory:IComplaintOfficerDirectory {public Task<bool> IsCommissionerAsync(Guid actor,CancellationToken ct)=>Task.FromResult(false);public Task<ComplaintDirectorySnapshot> ReadAsync(CancellationToken ct)=>Task.FromResult(new ComplaintDirectorySnapshot([],"isolated"));}
sealed class TestAuth(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder){protected override Task<AuthenticateResult> HandleAuthenticateAsync(){if(!Request.Headers.TryGetValue("X-Test-Role",out var role))return Task.FromResult(AuthenticateResult.NoResult());var identity=new ClaimsIdentity([new(ClaimTypes.Role,role.ToString()),new(ClaimTypes.NameIdentifier,"11111111-0000-4000-8000-000000000001")],Scheme.Name);return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity),Scheme.Name)));}}
