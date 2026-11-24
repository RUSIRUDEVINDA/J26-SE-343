using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
namespace StateLandGovernance.UnitTests.GovernanceIntelligence;
public sealed class ComplaintHandlingRulesTests
{
    public static IEnumerable<object[]> Transitions()
    {
        string[] states=["Unassigned","UnderCommissionerReview","AssignedToDs","InformationRequested","UnderInvestigation","Resolved","Closed","Reopened"];
        string[] operations=["TakeDirect","AssignDs","ReassignDs","StartInvestigation","RecordNote","RequestInformation","RecordFindings","Resolve","Close","Reopen","DsReviewRecorded"];
        foreach(var state in states)foreach(var op in operations)yield return [state,op];
    }
    [Theory,MemberData(nameof(Transitions))]
    public void Lifecycle_EnforcesExplicitTransitionMatrix(string state,string operation)
    {
        var active=state is not ("Resolved" or "Closed");
        string? expected=operation switch
        {
            "TakeDirect" when active=>"UnderCommissionerReview",
            "AssignDs" or "ReassignDs" when active=>"AssignedToDs",
            "StartInvestigation" when state is "UnderCommissionerReview" or "AssignedToDs" or "InformationRequested" or "Reopened"=>"UnderInvestigation",
            "RecordNote" or "DsReviewRecorded" when active&&state!="Unassigned"=>state,
            "RequestInformation" when state is "UnderCommissionerReview" or "AssignedToDs" or "UnderInvestigation" or "Reopened"=>"InformationRequested",
            "RecordFindings" when state=="UnderInvestigation"=>state,
            "Resolve" when state=="UnderInvestigation"=>"Resolved",
            "Close" when state=="Resolved"=>"Closed",
            "Reopen" when !active=>"Reopened",_=>null
        };
        if(expected==null)Assert.Throws<ComplaintConcurrencyException>(()=>ComplaintHandlingRules.Next(state,operation));
        else Assert.Equal(expected,ComplaintHandlingRules.Next(state,operation));
    }
    static ComplaintHandlingAction Decision()=>new(Guid.NewGuid(),1,"Resolve","Reasoned software-test decision",
        new(){["outcome"]="UnableToDetermine",["findings"]="Evidence is incomplete.",["actionsTaken"]="Requested an actual source reference.",["decisionBasis"]="No verified evidence available."},[],"Unable to determine; no allegation is verified.");
    [Fact]public void Resolution_RequiresActualDetailsAndApplicantSafeSummary()
    {
        ComplaintHandlingRules.Validate(Decision());
        Assert.Throws<ArgumentException>(()=>ComplaintHandlingRules.Validate(Decision() with{PublicSummary=""}));
        var missing=Decision();missing.Details.Remove("findings");Assert.Throws<ArgumentException>(()=>ComplaintHandlingRules.Validate(missing));
        var forged=Decision();forged.Details["verified"]="true";Assert.Throws<ArgumentException>(()=>ComplaintHandlingRules.Validate(forged));
    }
    [Fact]public void JsonCanonicalization_IsStableAcrossPostgresObjectKeyOrder()
    {
        Assert.Equal(ComplaintHandlingRules.CanonicalJson("{\"a\":1,\"b\":{\"c\":2,\"d\":3}}"),ComplaintHandlingRules.CanonicalJson("{\"b\":{\"d\":3,\"c\":2},\"a\":1}"));
    }
    sealed class Directory:IComplaintOfficerDirectory
    {
        public Task<bool> IsCommissionerAsync(Guid actor,CancellationToken ct)=>Task.FromResult(true);
        public Task<ComplaintDirectorySnapshot> ReadAsync(CancellationToken ct)=>Task.FromResult(new ComplaintDirectorySnapshot([],new string('a',64)));
    }
    [Fact]public async Task ReviewAndReopen_RequireExplicitPermissionAndExactOperation()
    {
        var actor=Guid.NewGuid();var grant=new CentralComplaintGrant(actor,Guid.NewGuid(),"COMMISSIONER",[ComplaintRoutingPolicy.Read,ComplaintRoutingPolicy.Assign],DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddHours(1),"isolated approval");
        var options=new CentralComplaintOptions{Enabled=true,WritesEnabled=true,Grants=[grant]};var policy=new ComplaintRoutingPolicy(options,new Directory(),TimeProvider.System);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>policy.RequireReviewAsync(actor,"TakeDirect",default));
        grant.Permissions=[ComplaintRoutingPolicy.Read,ComplaintRoutingPolicy.Review];grant.ReviewOperations=["Resolve"];
        await policy.RequireReviewAsync(actor,"Resolve",default);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>policy.RequireReviewAsync(actor,"Reopen",default));
        grant.ReviewOperations=["Resolve","Reopen"];await policy.RequireReviewAsync(actor,"Reopen",default);
    }
}
