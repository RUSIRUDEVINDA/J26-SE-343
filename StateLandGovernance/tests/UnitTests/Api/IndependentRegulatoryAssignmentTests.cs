using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Xunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using StateLandGovernance.Api.Security;
namespace StateLandGovernance.UnitTests.Api;
public class IndependentRegulatoryAssignmentTests
{
    sealed class Env : IWebHostEnvironment
    { public string EnvironmentName{get;set;}="Development";public string ApplicationName{get;set;}="unit";public string ContentRootPath{get;set;}="";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();public string WebRootPath{get;set;}="";public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider(); }
    sealed class Monitor : IOptionsMonitor<DevelopmentRegulatoryReportReadOptions>
    {public DevelopmentRegulatoryReportReadOptions CurrentValue{get;set;}=new();public DevelopmentRegulatoryReportReadOptions Get(string? n)=>CurrentValue;public IDisposable? OnChange(Action<DevelopmentRegulatoryReportReadOptions,string?> x)=>null;}
    sealed class Clock : TimeProvider {public DateTimeOffset Now=DateTimeOffset.Parse("2026-10-21T00:00:00Z");public override DateTimeOffset GetUtcNow()=>Now;}
    static (Monitor,Clock,Env,RegulatoryReadAssignment) Setup()
    {
        var c=new Clock();var grant=new RegulatoryReadAssignment{AssignmentId=Guid.NewGuid(),EvaluationId=Guid.NewGuid(),ActorId=Guid.NewGuid(),AssignedByActorId=Guid.NewGuid(),SourceReference="Explicit server-owner review instruction",ValidFromUtc=c.Now.AddMinutes(-1),ExpiresAtUtc=c.Now.AddHours(1)};
        return(new(){CurrentValue=new(){Enabled=true,Assignments=new(){grant}}},c,new(),grant);
    }
    [Fact] public async Task OnlyExactActorEvaluationAssignmentIsGranted()
    {var(m,t,e,g)=Setup();var s=new DevelopmentRegulatoryReportAssignments(m,e,t);Assert.Equal(new[]{g.EvaluationId},await s.ReadAllowedIdsAsync(g.ActorId,default));Assert.Empty(await s.ReadAllowedIdsAsync(Guid.NewGuid(),default));}
    [Fact] public async Task DefaultDisabledAndProductionAlwaysDeny()
    {var(m,t,e,g)=Setup();m.CurrentValue.Enabled=false;var s=new DevelopmentRegulatoryReportAssignments(m,e,t);Assert.Empty(await s.ReadAllowedIdsAsync(g.ActorId,default));m.CurrentValue.Enabled=true;e.EnvironmentName="Production";Assert.Empty(await s.ReadAllowedIdsAsync(g.ActorId,default));}
    [Fact] public async Task ExpiryRevocationAndConfigurationReplacementAreEffectiveOnNextRead()
    {
        var(m,t,e,g)=Setup();var s=new DevelopmentRegulatoryReportAssignments(m,e,t);g.RevokedAtUtc=t.Now;Assert.Empty(await s.ReadAllowedIdsAsync(g.ActorId,default));g.RevokedAtUtc=null;t.Now=g.ExpiresAtUtc;Assert.Empty(await s.ReadAllowedIdsAsync(g.ActorId,default));t.Now=g.ValidFromUtc.AddMinutes(1);m.CurrentValue=new(){Enabled=true,Assignments=new()};Assert.Empty(await s.ReadAllowedIdsAsync(g.ActorId,default));
    }
    [Fact] public async Task IncompleteFutureAndMalformedAssignmentsFailClosed()
    {
        var(m,t,e,g)=Setup();var s=new DevelopmentRegulatoryReportAssignments(m,e,t);g.SourceReference="";Assert.Empty(await s.ReadAllowedIdsAsync(g.ActorId,default));g.SourceReference="source";g.AssignedByActorId=Guid.Empty;Assert.Empty(await s.ReadAllowedIdsAsync(g.ActorId,default));g.AssignedByActorId=Guid.NewGuid();g.ValidFromUtc=t.Now.AddHours(1);Assert.Empty(await s.ReadAllowedIdsAsync(g.ActorId,default));
    }
    [Fact] public async Task LocalOperatorGrant_IsExactDevelopmentLoopbackOnly_WithoutOfficerIssuer()
    {
        var(m,t,e,g)=Setup(); m.CurrentValue.Assignments.Clear();
        var grant=new LocalOperatorRegulatoryReadGrant {GrantId=Guid.NewGuid(),EvaluationId=g.EvaluationId,ActorId=g.ActorId,
            AuthorizationSource="Local operator explicit Development instruction",ValidFromUtc=t.Now,ExpiresAtUtc=t.Now.AddHours(24)};
        m.CurrentValue.LocalOperatorGrants.Add(grant);
        var http=new Microsoft.AspNetCore.Http.HttpContextAccessor {HttpContext=new Microsoft.AspNetCore.Http.DefaultHttpContext()};
        http.HttpContext.Connection.RemoteIpAddress=System.Net.IPAddress.Loopback;
        var service=new DevelopmentRegulatoryReportAssignments(m,e,t,http);
        Assert.Equal(new[]{g.EvaluationId},await service.ReadAllowedIdsAsync(g.ActorId,default));
        Assert.Empty(await service.ReadAllowedIdsAsync(Guid.NewGuid(),default));
        Assert.DoesNotContain(Guid.NewGuid(),await service.ReadAllowedIdsAsync(g.ActorId,default));
        e.EnvironmentName="Production";Assert.Empty(await service.ReadAllowedIdsAsync(g.ActorId,default));
        e.EnvironmentName="Development";http.HttpContext.Connection.RemoteIpAddress=System.Net.IPAddress.Parse("192.0.2.1");
        Assert.Empty(await service.ReadAllowedIdsAsync(g.ActorId,default));
        http.HttpContext.Connection.RemoteIpAddress=null;Assert.Empty(await service.ReadAllowedIdsAsync(g.ActorId,default));
    }
    [Fact] public async Task LocalOperatorGrant_ExpiryRevocationMalformedAndDisabled_FailClosed()
    {
        var(m,t,e,g)=Setup();m.CurrentValue.Assignments.Clear();
        var grant=new LocalOperatorRegulatoryReadGrant {GrantId=Guid.NewGuid(),EvaluationId=g.EvaluationId,ActorId=g.ActorId,
            AuthorizationSource="User instruction",ValidFromUtc=t.Now,ExpiresAtUtc=t.Now.AddHours(24)};
        m.CurrentValue.LocalOperatorGrants.Add(grant);
        var http=new Microsoft.AspNetCore.Http.HttpContextAccessor {HttpContext=new Microsoft.AspNetCore.Http.DefaultHttpContext()};
        http.HttpContext.Connection.RemoteIpAddress=System.Net.IPAddress.IPv6Loopback;
        var service=new DevelopmentRegulatoryReportAssignments(m,e,t,http);
        grant.RevokedAtUtc=t.Now;Assert.Empty(await service.ReadAllowedIdsAsync(g.ActorId,default));grant.RevokedAtUtc=null;
        t.Now=grant.ExpiresAtUtc;Assert.Empty(await service.ReadAllowedIdsAsync(g.ActorId,default));t.Now=grant.ValidFromUtc;
        grant.ExpiresAtUtc=grant.ValidFromUtc.AddHours(25);Assert.Empty(await service.ReadAllowedIdsAsync(g.ActorId,default));grant.ExpiresAtUtc=grant.ValidFromUtc.AddHours(24);
        grant.AuthorizationSource="";Assert.Empty(await service.ReadAllowedIdsAsync(g.ActorId,default));grant.AuthorizationSource="User instruction";
        m.CurrentValue.Enabled=false;Assert.Empty(await service.ReadAllowedIdsAsync(g.ActorId,default));m.CurrentValue.Enabled=true;
        Assert.Single(await service.ReadAllowedIdsAsync(g.ActorId,default));
        m.CurrentValue.LocalOperatorGrants.Clear();Assert.Empty(await service.ReadAllowedIdsAsync(g.ActorId,default));
    }
    [Fact] public async Task HttpRequests_AnonymousAndUnassignedOfficer_AreDenied()
    {
        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(new Microsoft.AspNetCore.Builder.WebApplicationOptions { EnvironmentName = "Testing", Args = Array.Empty<string>() });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes("isolated-test-key-not-a-runtime-secret-1234567890"));
        builder.Services.AddAuthentication("Bearer").AddJwtBearer("Bearer", options => options.TokenValidationParameters = new() {
            ValidateIssuer = false, ValidateAudience = false, ValidateLifetime = true, ValidateIssuerSigningKey = true, IssuerSigningKey = key });
        builder.Services.AddAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(StateLandGovernance.GovernanceIntelligence.Presentation.Controllers.IndependentRegulatoryReportsController).Assembly);
        builder.Services.AddSingleton<StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports.IIndependentRegulatoryReadAssignments, StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports.DenyIndependentRegulatoryReadAssignments>();
        builder.Services.AddSingleton<StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports.IIndependentRegulatoryReportStore, StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories.UnavailableIndependentRegulatoryReportStore>();
        builder.Services.AddSingleton<StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports.IndependentRegulatoryReportQueries>();
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        await app.StartAsync();
        try {
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            var path = "/api/governance-intelligence/regulatory-assessments/" + Guid.NewGuid();
            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
            var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(claims: new[] {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Officer") },
                expires: DateTime.UtcNow.AddMinutes(2), signingCredentials: new(key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new("Bearer", new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token));
            Assert.Equal(System.Net.HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
        } finally { await app.StopAsync(); }
    }
}
