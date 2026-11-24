using Microsoft.EntityFrameworkCore;
using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public sealed class ComplaintAssignmentAuditTests
{
    sealed class Actor : IComplaintActorContext { public Guid ActorId { get; } = Guid.NewGuid(); }
    sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(1234567); }
    [Fact]
    public async Task AuditInterceptor_OverridesSuppliedActorAndTime_AndBindsCanonicalBytes()
    {
        var actor = new Actor();
        var options = new DbContextOptionsBuilder<GovernanceIntelligenceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new ComplaintAssignmentAuditInterceptor(actor, new Clock())).Options;
        await using var db = new GovernanceIntelligenceDbContext(options);
        var row = new ComplaintAssignmentEntity { Id=Guid.NewGuid(),ComplaintId=Guid.NewGuid(),Revision=1,
            TargetActorId=Guid.NewGuid(),TargetOfficeCode="TEST",Reason="Fictional routing",SubmissionKey=Guid.NewGuid(),
            InputSha256=new string('a',64),DirectorySnapshotSha256=new string('b',64),AuthorizationGrantId=Guid.NewGuid(),
            AuthorizationSourceReference="isolated test",AssignedByActorId=Guid.NewGuid(),AssignedAtUtc=DateTimeOffset.MaxValue,
            EventCanonicalJson="forged",EventSha256="forged" };
        db.Add(row);await db.SaveChangesAsync();
        Assert.Equal(actor.ActorId,row.AssignedByActorId);
        Assert.Equal(0,row.AssignedAtUtc.Ticks%10);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddTicks(1234560),row.AssignedAtUtc);
        Assert.Equal(ComplaintRoutingPolicy.Canonical(row.ToRecord()),row.EventCanonicalJson);
        Assert.Equal(ComplaintRoutingPolicy.Hash(row.EventCanonicalJson),row.EventSha256);
        row.Reason="changed";
        await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync());
    }
}
