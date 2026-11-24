using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using StateLandGovernance.GovernanceIntelligence.Application.WorkflowAnomaly;
using StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;
public class PostWorkflowDemonstrationTests
{
    sealed class Env : IHostEnvironment {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
    sealed class Settings : IOptionsMonitor<PostWorkflowDemonstrationOptions> {
        public PostWorkflowDemonstrationOptions CurrentValue { get; set; } = new() { Enabled = true, WritesEnabled = true };
        public PostWorkflowDemonstrationOptions Get(string? n) => CurrentValue;
        public IDisposable? OnChange(Action<PostWorkflowDemonstrationOptions,string?> listener) => null;
    }
    sealed class Store : IPostWorkflowDemonstrationStore {
        public int Writes; public Guid Seen; public bool StorageFailure;
        public Task<IReadOnlyList<PostWorkflowReport>> ListAsync(Guid actor, CancellationToken ct) { if(StorageFailure)throw new Npgsql.NpgsqlException("isolated simulated database failure");Seen = actor; return Task.FromResult<IReadOnlyList<PostWorkflowReport>>([]); }
        public Task<PostWorkflowReport> GetAsync(Guid a, Guid id, CancellationToken ct) => throw new UnauthorizedAccessException();
        public Task<PostWorkflowReport> RunAsync(Guid a, string s, bool r, CancellationToken ct, int expectedAttempt = 0) { Writes++; throw new ArgumentException(); }
    }
    static PostWorkflowDemonstrationsController Controller(Store s, Env e, Settings o, Guid actor, string? role) {
        var c = new PostWorkflowDemonstrationsController(s,e,o) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        if (role != null) c.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, actor.ToString()), new(ClaimTypes.Role, role)], "test"));
        return c;
    }
    [Theory][InlineData("Production")][InlineData("Staging")]
    public async Task NonDevelopmentRejectsEvenEnabled(string environment) {
        var s = new Store(); var c = Controller(s,new() { EnvironmentName = environment },new(),Guid.NewGuid(),"Officer");
        Assert.Equal(503,Assert.IsType<ObjectResult>(await c.Run(new("administrative"),default)).StatusCode); Assert.Equal(0,s.Writes);
    }
    [Fact] public async Task AnonymousApplicantAndUnassignedDetailDenied() {
        var s = new Store(); var actor = Guid.NewGuid();
        Assert.IsType<UnauthorizedResult>(await Controller(s,new(),new(),actor,null).List(default));
        Assert.IsType<ForbidResult>(await Controller(s,new(),new(),actor,"Applicant").List(default));
        Assert.IsType<ForbidResult>(await Controller(s,new(),new(),actor,"Officer").Get(Guid.NewGuid(),default));
        Assert.Equal(0,s.Writes);
    }
    [Fact] public async Task AuthenticatedIdentityIsServerDerivedAndWritesGateDoesNotBlockReads() {
        var s = new Store(); var actor = Guid.NewGuid();var o = new Settings { CurrentValue = new() { Enabled = true } };
        var c = Controller(s,new(),o,actor,"Officer");Assert.IsType<OkObjectResult>(await c.List(default)); Assert.Equal(actor,s.Seen);
        Assert.Equal(503,Assert.IsType<ObjectResult>(await c.Run(new("administrative"),default)).StatusCode);Assert.Equal(0,s.Writes);
    }
    [Fact] public void BothFixturesPreserveSeparateFinalNotificationAndNaiveTimeSemantics() {
        foreach(var scenario in PostWorkflowDemonstrations.Scenarios) {
            var p = PostWorkflowDemonstrations.Create(scenario,Guid.NewGuid(),Guid.NewGuid(),DateTimeOffset.UtcNow);
            Assert.True(p.Provenance.IsSynthetic);Assert.Equal("DS Final Notification",p.Events[^1].Activity);
            Assert.Equal("Commissioner Decision",p.Events[^2].Activity);Assert.True(p.Events[^1].Timestamp > p.Events[^2].Timestamp);
            Assert.All(p.Events,x=>Assert.DoesNotContain("Z",x.AnomalyInputTimestamp!));
            Assert.True(p.TryExtractAnomalyScoringEvents(out var events,out _));Assert.Equal(p.Events.Count,events.Count);
        }
        Assert.Throws<ArgumentException>(()=>PostWorkflowDemonstrations.Create("browser-completed",Guid.NewGuid(),Guid.NewGuid(),DateTimeOffset.UtcNow));
    }
    [Fact] public async Task LegacyPersistedSandboxIsDisabledWithoutExplicitDevelopmentOptIn() {
        var c = new GovernanceIntelligenceController(null!,null!,null!,null!,null!,null!);
        var response = await c.AssessWorkflowAnomaly(new("known-case", Guid.NewGuid(), Guid.NewGuid()),null!,default);
        Assert.Equal(503,Assert.IsType<ObjectResult>(response).StatusCode);
    }
    [Fact] public async Task OfficerCataloguePreviewsBothServerHistoriesWithoutPersistenceOrInference() {
        var store = new Store();var c = Controller(store,new(),new(),Guid.NewGuid(),"Officer");
        var response = Assert.IsType<OkObjectResult>(await c.Catalogue());
        var json = System.Text.Json.JsonSerializer.Serialize(response.Value);
        Assert.Contains("Administrative workflow",json);Assert.Contains("Delays and revisions workflow",json);
        Assert.Contains("DS Final Notification",json);Assert.Contains("canRun",json);Assert.Equal(0,store.Writes);Assert.Equal(Guid.Empty,store.Seen);
    }
    [Fact] public async Task StorageFailureReturnsActionableProblemWithoutInternalDatabaseDetail() {
        var c=Controller(new() { StorageFailure=true },new(),new(),Guid.NewGuid(),"Officer");
        var result=Assert.IsType<ObjectResult>(await c.List(default));Assert.Equal(503,result.StatusCode);
        var problem=Assert.IsType<ProblemDetails>(result.Value);Assert.Equal("Post-workflow persistence unavailable",problem.Title);
        Assert.Contains("refresh saved history",problem.Detail);Assert.DoesNotContain("simulated",problem.Detail);
    }
}
