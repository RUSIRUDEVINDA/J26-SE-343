using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StateLandGovernance.GovernanceIntelligence.Application.WorkflowAnomaly;
using StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;

// Explicit isolated proof only: no production host/config/accounts, no ordinary connection.
internal static class HttpProof
{
    public static async Task Run(IPostWorkflowDemonstrationStore store, Guid creator)
    {
        var b = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development", Args = [] });
        b.Logging.ClearProviders(); b.WebHost.UseUrls("http://127.0.0.1:0");
        b.Services.AddSingleton(store);
        b.Services.Configure<PostWorkflowDemonstrationOptions>(o => { o.Enabled = true; o.WritesEnabled = true; });
        b.Services.AddControllers().AddApplicationPart(typeof(PostWorkflowDemonstrationsController).Assembly);
        b.Services.AddAuthentication("isolated-proof").AddScheme<AuthenticationSchemeOptions, ProofAuthentication>("isolated-proof", _ => { });
        b.Services.AddAuthorization();
        await using var app = b.Build(); app.UseAuthentication();app.UseAuthorization();app.MapControllers();await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            const string path = "/api/governance-intelligence/post-workflow-demonstrations";
            void Check(bool ok,string label) { if (!ok) throw new Exception(label); }
            Check((await client.GetAsync(path + "/catalogue")).StatusCode == HttpStatusCode.Unauthorized,"Anonymous HTTP denied");
            client.DefaultRequestHeaders.Add("X-Isolated-Proof-Actor", creator.ToString());
            client.DefaultRequestHeaders.Add("X-Isolated-Proof-Role", "Applicant");
            Check((await client.GetAsync(path + "/catalogue")).StatusCode == HttpStatusCode.Forbidden,"Applicant HTTP denied");
            client.DefaultRequestHeaders.Remove("X-Isolated-Proof-Role");client.DefaultRequestHeaders.Add("X-Isolated-Proof-Role", "Officer");
            var choices = await client.GetFromJsonAsync<System.Text.Json.JsonElement>(path + "/catalogue");
            Check(choices.GetArrayLength() == 2 && choices[0].GetProperty("canRun").GetBoolean(),"Officer HTTP catalogue and readiness");
            var reports = await client.GetFromJsonAsync<PostWorkflowReport[]>(path);
            Check(reports?.Length == 2,"Creator HTTP saved history");var saved = reports![0];
            var replay = await client.PostAsJsonAsync(path,new { scenario = saved.Scenario });
            Check(replay.IsSuccessStatusCode,"HTTP replay succeeds");
            var value = await replay.Content.ReadFromJsonAsync<PostWorkflowReport>();Check(value?.Id == saved.Id,"HTTP replay same saved job");
            Check((await client.GetFromJsonAsync<PostWorkflowReport>(path + "/" + saved.Id))?.Assessment?.AssessmentId == saved.Assessment?.AssessmentId,"HTTP saved report retrieval");
            Check((await client.GetAsync(path + "/" + Guid.NewGuid())).StatusCode == HttpStatusCode.NotFound,"Missing report HTTP404");
            client.DefaultRequestHeaders.Remove("X-Isolated-Proof-Actor");client.DefaultRequestHeaders.Add("X-Isolated-Proof-Actor",Guid.NewGuid().ToString());
            Check((await client.GetAsync(path + "/" + saved.Id)).StatusCode == HttpStatusCode.Forbidden,"Cross creator HTTP denied");
            Console.WriteLine("Isolated HTTP proof: anonymous401, applicant403, officer catalogue200/two previews, creator history/detail200, idempotent POST same job, cross-creator403. Test-only authentication; not ordinary DS/browser evidence.");
        }
        finally { await app.StopAsync(); }
    }
}
internal sealed class ProofAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Guid.TryParse(Request.Headers["X-Isolated-Proof-Actor"],out var actor)) return Task.FromResult(AuthenticateResult.NoResult());
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier,actor.ToString()),new(ClaimTypes.Role,Request.Headers["X-Isolated-Proof-Role"].ToString())],Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal,Scheme.Name)));
    }
}
