using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
using Microsoft.Extensions.Configuration;
namespace StateLandGovernance.UnitTests.GovernanceIntelligence;
public sealed class CentralComplaintPolicyTests
{
    [Fact] public async Task ReviewedConfiguration_BindsExactTimedGrant()
    {
        var actor=Guid.NewGuid();var grant=Guid.NewGuid();var clock=new Clock();
        var values=new Dictionary<string,string?> { ["Enabled"]="true",["WritesEnabled"]="true",
            ["Grants:0:ActorId"]=actor.ToString(),["Grants:0:GrantId"]=grant.ToString(),["Grants:0:Institution"]="COMMISSIONER",
            ["Grants:0:Permissions:0"]=ComplaintRoutingPolicy.Read,["Grants:0:Permissions:1"]=ComplaintRoutingPolicy.Assign,
            ["Grants:0:ValidFromUtc"]=clock.Now.AddHours(-1).ToString("O"),["Grants:0:ExpiresAtUtc"]=clock.Now.AddHours(1).ToString("O"),
            ["Grants:0:SourceReference"]="isolated configuration binding proof" };
        var config=new ConfigurationBuilder().AddInMemoryCollection(values).Build().Get<CentralComplaintOptions>()!;
        Assert.Equal(2,config.Grants.Single().Permissions.Length);
        var policy=new ComplaintRoutingPolicy(config,new Directory(),clock);
        Assert.Equal(grant,(await policy.RequireCentralAsync(actor,true,default)).GrantId);
    }
    sealed class Clock : TimeProvider { public DateTimeOffset Now = new(2026,10,23,0,0,0,TimeSpan.Zero); public override DateTimeOffset GetUtcNow()=>Now; }
    sealed class Directory : IComplaintOfficerDirectory
    {
        public bool Commissioner=true;
        public Task<bool> IsCommissionerAsync(Guid actor,CancellationToken ct)=>Task.FromResult(Commissioner);
        public Task<ComplaintDirectorySnapshot> ReadAsync(CancellationToken ct)=>Task.FromResult(new ComplaintDirectorySnapshot([],new string('a',64)));
    }
    [Theory]
    [InlineData("missing")][InlineData("expired")][InlineData("future")][InlineData("duplicate")]
    [InlineData("institution")][InlineData("read")][InlineData("assign")][InlineData("profile")][InlineData("grant-id")]
    public async Task CentralPermission_InvalidGrant_Denied(string fault)
    {
        var actor=Guid.NewGuid();var clock=new Clock();var directory=new Directory();
        var grant=new CentralComplaintGrant(actor,Guid.NewGuid(),"COMMISSIONER",[ComplaintRoutingPolicy.Read,ComplaintRoutingPolicy.Assign],clock.Now.AddHours(-1),clock.Now.AddHours(1),"isolated approval");
        if(fault=="expired")grant=grant with{ExpiresAtUtc=clock.Now};
        if(fault=="future")grant=grant with{ValidFromUtc=clock.Now.AddMinutes(1)};
        if(fault=="institution")grant=grant with{Institution="DS"};
        if(fault=="read")grant=grant with{Permissions=[ComplaintRoutingPolicy.Assign]};
        if(fault=="assign")grant=grant with{Permissions=[ComplaintRoutingPolicy.Read]};
        if(fault=="profile")directory.Commissioner=false;
        if(fault=="grant-id")grant=grant with{GrantId=Guid.Empty};
        var options=new CentralComplaintOptions{Enabled=true,WritesEnabled=true,Grants=fault=="missing"?[]:fault=="duplicate"?[grant,grant]:[grant]};
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>new ComplaintRoutingPolicy(options,directory,clock).RequireCentralAsync(actor,true,default));
    }
    [Fact] public async Task DisabledCentralAccess_FailsClosed()
    {
        var policy=new ComplaintRoutingPolicy(new(),new Directory(),new Clock());
        await Assert.ThrowsAsync<CentralComplaintUnavailableException>(()=>policy.RequireCentralAsync(Guid.NewGuid(),false,default));
        Assert.False(await policy.EligibleAsync(Guid.NewGuid(),default));
    }
    [Fact] public void CanonicalEvent_BindsEveryTypedField()
    {
        var e=new ComplaintAssignment(Guid.NewGuid(),Guid.NewGuid(),1,Guid.NewGuid(),"OFFICE",Guid.NewGuid(),DateTimeOffset.UnixEpoch,"reason",Guid.NewGuid(),new string('a',64),null,new string('b',64),Guid.NewGuid(),"approval","","");
        var canonical=ComplaintRoutingPolicy.Canonical(e);var hash=ComplaintRoutingPolicy.Hash(canonical);
        Assert.Equal(64,hash.Length);Assert.NotEqual(hash,ComplaintRoutingPolicy.Hash(ComplaintRoutingPolicy.Canonical(e with{TargetOfficeCode="OTHER"})));
        Assert.NotEqual(hash,ComplaintRoutingPolicy.Hash(ComplaintRoutingPolicy.Canonical(e with{AssignedByActorId=Guid.NewGuid()})));
        Assert.Equal(canonical,ComplaintRoutingPolicy.Canonical(e with{EventSha256=hash,EventCanonicalJson=canonical}));
    }
}
