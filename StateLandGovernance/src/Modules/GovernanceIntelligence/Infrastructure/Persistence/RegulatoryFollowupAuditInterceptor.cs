using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
using StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;

public sealed class RegulatoryFollowupAuditInterceptor(IComplaintActorContext actor, TimeProvider clock) : SaveChangesInterceptor
{
    void Stamp(DbContext? db)
    {
        if (db == null) return;
        foreach (var entry in db.ChangeTracker.Entries<RegulatoryFollowupEntity>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted) throw new InvalidOperationException("Regulatory follow-up history is append-only.");
            if (entry.State != EntityState.Added) continue;
            if (actor.ActorId == Guid.Empty || entry.Entity.ActorId != actor.ActorId || entry.Entity.OriginalFindingSha256.Length != 64)
                throw new UnauthorizedAccessException();
            var now = clock.GetUtcNow().ToUniversalTime();
            entry.Entity.ActorId = actor.ActorId;
            entry.Entity.CreatedAtUtc = new(now.Ticks - now.Ticks % 10, TimeSpan.Zero);
            entry.Entity.CanonicalJson = entry.Entity.Canonical(entry.Entity.OriginalFindingSha256);
            entry.Entity.EventSha256 = RegulatoryJson.Hash(entry.Entity.CanonicalJson);
        }
    }
    public override InterceptionResult<int> SavingChanges(DbContextEventData data, InterceptionResult<int> result) { Stamp(data.Context); return result; }
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
    { Stamp(data.Context); return ValueTask.FromResult(result); }
}
