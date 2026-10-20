namespace StateLandGovernance.UnitTests.WorkflowGovernance;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Presentation.Security;
using Xunit;

public sealed class CaseAssignmentLifecycleTests
{
    private readonly Guid caseId = Guid.NewGuid(), docId = Guid.NewGuid(), coordinator = Guid.NewGuid(), reviewer = Guid.NewGuid();
    [Fact] public async Task AssignmentRequiresCoordinatorAndExactOfficeDocument()
    {
        var store = Store(); var directory = Directory(); var service = new CaseAssignmentService(store, directory);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ChangeAsync(reviewer, caseId, 0, reviewer, "DS", docId, "test", DateTimeOffset.UtcNow.AddDays(1), default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ChangeAsync(coordinator, caseId, 0, reviewer, "OTHER", docId, "test", DateTimeOffset.UtcNow.AddDays(1), default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ChangeAsync(coordinator, caseId, 0, reviewer, "DS", Guid.NewGuid(), "test", DateTimeOffset.UtcNow.AddDays(1), default));
        Assert.Null(store.State.Active);
    }
    [Fact] public async Task ReassignmentPreservesHistoryRejectsStaleRevisionAndRevokesAccessWithoutTokenRefresh()
    {
        var store = Store(); var service = new CaseAssignmentService(store, Directory());
        var state = await service.ChangeAsync(coordinator, caseId, 0, reviewer, "DS", docId, "Assigned for source review", DateTimeOffset.UtcNow.AddDays(1), default);
        Assert.Equal(CaseAssignmentService.ProposalReviewCapabilities, state.Active!.Capabilities);
        var principal = Identity(reviewer); var http = new DefaultHttpContext(); http.Request.RouteValues["caseId"] = caseId.ToString();
        var claims = new WorkflowAssignmentClaimsTransformation(new ConfigurationBuilder().Build(), new HttpContextAccessor { HttpContext = http }, store);
        await claims.TransformAsync(principal); Assert.True(principal.HasClaim("capability", "FactVerifier"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ChangeAsync(coordinator, caseId, 0, reviewer, "DS", docId, "outdated", DateTimeOffset.UtcNow.AddDays(1), default));
        state = await service.ChangeAsync(coordinator, caseId, 1, null, "DS", docId, "Revoked by coordinator", DateTimeOffset.UtcNow.AddDays(1), default);
        Assert.Null(state.Active); Assert.Single(state.History); Assert.NotNull(state.History[0].RevokedAt);
        await claims.TransformAsync(principal); Assert.False(principal.HasClaim("capability", "FactVerifier"));
    }
    [Fact] public async Task AnotherCaseCannotReceiveGrantAndExpiredAssignmentDoesNotAuthorize()
    {
        var store = Store(); store.State = store.State with { Active = new(Guid.NewGuid(), reviewer, "DS", docId, ["FactVerifier"], coordinator, "fixture", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(-1)) };
        var http = new DefaultHttpContext(); http.Request.RouteValues["caseId"] = caseId.ToString();
        var claims = new WorkflowAssignmentClaimsTransformation(new ConfigurationBuilder().Build(), new HttpContextAccessor { HttpContext = http }, store);
        var p = await claims.TransformAsync(Identity(reviewer)); Assert.False(p.HasClaim("capability", "FactVerifier"));
        http.Request.RouteValues["caseId"] = Guid.NewGuid().ToString();
        p = await claims.TransformAsync(Identity(reviewer)); Assert.False(p.HasClaim("authority_case_id", caseId.ToString()));
    }
    [Fact] public void JurisdictionRequiresExactUniqueEffectiveVersionAndNeverGuessesOffice()
    {
        var now = DateTimeOffset.UtcNow;
        var policy = new IntakeJurisdictionPolicy(Guid.NewGuid(), "TEST-ONLY", "1", "EXACT", "DS", "test source", coordinator, now.AddDays(-1), now.AddDays(1));
        Assert.Equal(policy, CaseAssignmentService.ResolveJurisdiction([policy], "EXACT", now));
        Assert.Null(CaseAssignmentService.ResolveJurisdiction([policy], null, now));
        Assert.Null(CaseAssignmentService.ResolveJurisdiction([policy], "exact", now));
        Assert.Null(CaseAssignmentService.ResolveJurisdiction([policy], "EXACT", now.AddDays(2)));
        Assert.Null(CaseAssignmentService.ResolveJurisdiction([policy, policy with { Id = Guid.NewGuid(), Version = "2" }], "EXACT", now));
    }
    [Fact] public void RoutingReadsActualNestedPersistedIntakeContract()
    {
        Assert.Equal("EXACT", StateLandGovernance.WorkflowGovernance.Infrastructure.Repositories.PostgresCaseAssignmentStore.DeclaredJurisdiction(
            "{\"jurisdiction\":{\"code\":\"EXACT\"},\"purpose\":\"fixture\"}"));
        Assert.Null(StateLandGovernance.WorkflowGovernance.Infrastructure.Repositories.PostgresCaseAssignmentStore.DeclaredJurisdiction("{}"));
        Assert.Null(StateLandGovernance.WorkflowGovernance.Infrastructure.Repositories.PostgresCaseAssignmentStore.DeclaredJurisdiction(null));
    }
    [Fact] public async Task ManagedRevocationCannotFallBackToLegacyWorkspaceGrant()
    {
        var store = Store(); var http = new DefaultHttpContext(); http.Request.RouteValues["caseId"] = caseId.ToString();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["WorkflowGovernance:AuthorityAssignments:0:ActorId"] = reviewer.ToString(),
            ["WorkflowGovernance:AuthorityAssignments:0:CaseId"] = caseId.ToString(),
            ["WorkflowGovernance:AuthorityAssignments:0:SourceReference"] = "Legacy test grant",
            ["WorkflowGovernance:AuthorityAssignments:0:ExpiresAtUtc"] = DateTimeOffset.UtcNow.AddDays(1).ToString("O"),
            ["WorkflowGovernance:AuthorityAssignments:0:Capabilities:0"] = "CaseWorkspaceReviewer" }).Build();
        var result = await new WorkflowAssignmentClaimsTransformation(config, new HttpContextAccessor { HttpContext = http }, store).TransformAsync(Identity(reviewer));
        Assert.False(result.HasClaim("capability", "CaseWorkspaceReviewer"));
        Assert.True(result.HasClaim("authority_managed_case", caseId.ToString()));
    }
    [Fact] public async Task IndependentInstitutionGrantRemainsRestrictedAfterResponsibilityIsManaged()
    {
        var store = Store(); var http = new DefaultHttpContext(); http.Request.RouteValues["caseId"] = caseId.ToString();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["WorkflowGovernance:AuthorityAssignments:0:ActorId"] = reviewer.ToString(),
            ["WorkflowGovernance:AuthorityAssignments:0:CaseId"] = caseId.ToString(),
            ["WorkflowGovernance:AuthorityAssignments:0:SourceReference"] = "Explicit institutional test grant",
            ["WorkflowGovernance:AuthorityAssignments:0:InstitutionCode"] = "TEST_INSTITUTION",
            ["WorkflowGovernance:AuthorityAssignments:0:ExpiresAtUtc"] = DateTimeOffset.UtcNow.AddDays(1).ToString("O"),
            ["WorkflowGovernance:AuthorityAssignments:0:Capabilities:0"] = "InstitutionalReviewer" }).Build();
        var result = await new WorkflowAssignmentClaimsTransformation(config, new HttpContextAccessor { HttpContext = http }, store).TransformAsync(Identity(reviewer));
        Assert.True(result.HasClaim("institution", "TEST_INSTITUTION"));
        Assert.True(result.HasClaim("capability", "InstitutionalReviewer"));
        Assert.False(result.HasClaim("capability", "CaseWorkspaceReviewer"));
    }
    private ClaimsPrincipal Identity(Guid actor) => new(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, actor.ToString()), new("exp", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString()), new(ClaimTypes.Role,"Officer")], "test"));
    private FakeStore Store() => new(new(caseId,0,null,null,null,null,"UnassignedJurisdiction",null,[]),docId);
    private FakeDirectory Directory() => new(new(coordinator,"DS",true,DateTimeOffset.UtcNow.AddMonths(1),"explicit test office"),[new(reviewer,"Test reviewer","DS")]);
    private sealed record FakeDirectory(IntakeCoordinatorScope Scope, IReadOnlyList<EligibleCaseReviewer> Reviewers) : IIntakeAssignmentDirectory
    { public IntakeCoordinatorScope? Coordinator(Guid actor) => actor == Scope.ActorId ? Scope : null; }
    private sealed class FakeStore(CaseAssignmentState state, Guid document) : ICaseAssignmentStore
    {
        public CaseAssignmentState State = state;
        public Task<CaseAssignmentState?> ReadAsync(Guid id,CancellationToken ct=default) => Task.FromResult<CaseAssignmentState?>(id==State.CaseId?State:null);
        public Task<CaseAssignmentState> EnsureAsync(Guid id,CancellationToken ct=default)=>Task.FromResult(State);
        public Task<bool> DocumentBelongsAsync(Guid id,Guid doc,CancellationToken ct=default)=>Task.FromResult(id==State.CaseId&&doc==document);
        public Task<CaseAssignmentState> SaveAsync(CaseAssignmentState next,int revision,CancellationToken ct=default){State=next;return Task.FromResult(next);}
        public Task<IReadOnlyList<IntakeCaseSummary>> ListAsync(Guid? a,Guid? r,string? o,bool u,CancellationToken ct=default)=>Task.FromResult<IReadOnlyList<IntakeCaseSummary>>([]);
        public Task<IReadOnlyList<IntakeJurisdictionPolicy>> PoliciesAsync(CancellationToken ct=default)=>Task.FromResult<IReadOnlyList<IntakeJurisdictionPolicy>>([]);
        public Task PublishPolicyAsync(IntakeJurisdictionPolicy p,CancellationToken ct=default)=>Task.CompletedTask;
    }
}
