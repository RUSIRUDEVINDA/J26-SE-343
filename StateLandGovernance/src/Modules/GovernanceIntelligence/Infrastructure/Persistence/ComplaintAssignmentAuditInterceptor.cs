using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;

public sealed class ComplaintAssignmentAuditInterceptor(IComplaintActorContext actor, TimeProvider clock) : SaveChangesInterceptor
{
    private void Stamp(DbContext? db)
    {
        if (db == null) return;
        foreach (var entry in db.ChangeTracker.Entries<ComplaintAssignmentEntity>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted) throw new InvalidOperationException("Assignment history is append-only.");
            if (entry.State != EntityState.Added) continue;
            if (actor.ActorId == Guid.Empty) throw new UnauthorizedAccessException();
            var now = clock.GetUtcNow().ToUniversalTime();
            entry.Entity.AssignedByActorId = actor.ActorId;
            entry.Entity.AssignedAtUtc = new(now.Ticks - now.Ticks % 10, TimeSpan.Zero);
            entry.Entity.EventCanonicalJson = ComplaintRoutingPolicy.Canonical(entry.Entity.ToRecord());
            entry.Entity.EventSha256 = ComplaintRoutingPolicy.Hash(entry.Entity.EventCanonicalJson);
        }
        foreach(var entry in db.ChangeTracker.Entries<ComplaintHandlingEntity>())
        {
            if(entry.State is EntityState.Modified or EntityState.Deleted)throw new InvalidOperationException("Complaint handling history is append-only.");
            if(entry.State!=EntityState.Added)continue;
            if(actor.ActorId==Guid.Empty)throw new UnauthorizedAccessException();
            var now=clock.GetUtcNow().ToUniversalTime();
            entry.Entity.ActorId=actor.ActorId;entry.Entity.ActedAtUtc=new(now.Ticks-now.Ticks%10,TimeSpan.Zero);
            entry.Entity.EventCanonicalJson=entry.Entity.Canonical();
            entry.Entity.EventSha256=ComplaintRoutingPolicy.Hash(entry.Entity.EventCanonicalJson);
        }

    }
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    { Stamp(eventData.Context); return result; }
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    { Stamp(eventData.Context); return ValueTask.FromResult(result); }
}
