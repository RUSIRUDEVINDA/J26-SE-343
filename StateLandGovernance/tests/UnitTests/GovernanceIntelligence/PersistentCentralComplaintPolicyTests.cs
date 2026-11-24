using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
namespace StateLandGovernance.UnitTests.GovernanceIntelligence;
public sealed class PersistentCentralComplaintPolicyTests
{
    private readonly Guid actor=Guid.Parse("0ab669fe-d0ab-5695-ad55-10f6b3b5aab3");
    private CentralComplaintGrant Grant()=>new(actor,Guid.NewGuid(),"COMMISSIONER",[ComplaintRoutingPolicy.Read,ComplaintRoutingPolicy.Assign,ComplaintRoutingPolicy.Review],DateTimeOffset.UnixEpoch,null,"Explicit isolated provisioning") {Persistent=true,ReviewOperations=ComplaintHandlingRules.ReviewOperations};
    sealed class Directory : IComplaintOfficerDirectory
    {
        public bool Active=true;
        public Task<bool> IsCommissionerAsync(Guid id,CancellationToken ct)=>Task.FromResult(Active);
        public Task<ComplaintDirectorySnapshot> ReadAsync(CancellationToken ct)=>Task.FromResult(new ComplaintDirectorySnapshot([],new string('a',64)));
    }
    sealed class Source(CentralComplaintGrant grant) : IComplaintCentralGrantSource
    {
        public CentralComplaintGrant[] Grants=[grant];
        public Task<CentralComplaintGrant[]> ReadAsync(CancellationToken ct)=>Task.FromResult(Grants);
    }
    public static IEnumerable<object[]> Operations=>ComplaintHandlingRules.ReviewOperations.Select(x=>new object[]{x});
    [Theory,MemberData(nameof(Operations))]
    public async Task PersistentGrant_EachExplicitOperation_AllowedAndRevoked(string operation)
    {
        var grant=Grant();var source=new Source(grant);var directory=new Directory();
        var policy=new ComplaintRoutingPolicy(new(){Enabled=true,WritesEnabled=true},directory,TimeProvider.System,source);
        Assert.Equal(grant.GrantId,(await policy.RequireCentralAsync(actor,true,default)).GrantId);
        await policy.RequireReviewAsync(actor,operation,default);
        grant.Revoked=true;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>policy.RequireReviewAsync(actor,operation,default));
        grant.Revoked=false;directory.Active=false;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>policy.RequireReviewAsync(actor,operation,default));
        directory.Active=true;source.Grants=[];
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>policy.RequireCentralAsync(actor,false,default));
    }
    [Theory]
    [InlineData("missing-expiry")][InlineData("expired")][InlineData("revoked")][InlineData("other-actor")][InlineData("operation")][InlineData("role-only")]
    public async Task Grant_UnsafeOrUnprovisioned_Denied(string fault)
    {
        var grant=Grant();var options=new CentralComplaintOptions{Enabled=true,WritesEnabled=true,Grants=[grant]};
        if(fault=="missing-expiry")grant.Persistent=false;
        if(fault=="expired")grant.ExpiresAtUtc=DateTimeOffset.UtcNow.AddMinutes(-1);
        if(fault=="revoked")grant.Revoked=true;
        if(fault=="operation")grant.ReviewOperations=["RecordNote"];
        if(fault=="role-only")options.Grants=[];
        var policy=new ComplaintRoutingPolicy(options,new Directory(),TimeProvider.System,new Source(grant){Grants=options.Grants});
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>policy.RequireReviewAsync(fault=="other-actor"?Guid.NewGuid():actor,"Resolve",default));
    }
    [Fact] public async Task PersistentGrant_OptionalFutureExpiry_RetainsExistingSemantics()
    {
        var grant=Grant();grant.ExpiresAtUtc=DateTimeOffset.UtcNow.AddHours(1);
        await new ComplaintRoutingPolicy(new(){Enabled=true},new Directory(),TimeProvider.System,new Source(grant)).RequireCentralAsync(actor,false,default);
    }
    [Fact] public async Task PersistentGrant_WithoutFreshServerSource_Denied()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>new ComplaintRoutingPolicy(new(){Enabled=true,Grants=[Grant()]},new Directory(),TimeProvider.System).RequireCentralAsync(actor,false,default));
    }
}
