using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
using StateLandGovernance.GovernanceIntelligence.Application.OfficerFlows;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
public sealed class ScreeningEvidenceAuditInterceptor(IComplaintActorContext actor,TimeProvider clock):SaveChangesInterceptor {
 void Stamp(DbContext? db){if(db==null)return;var now=clock.GetUtcNow().ToUniversalTime();now=new(now.Ticks-now.Ticks%10,TimeSpan.Zero);
  foreach(var entry in db.ChangeTracker.Entries<ScreeningEvidenceAttachmentEntity>()){if(entry.State is EntityState.Modified or EntityState.Deleted)throw new InvalidOperationException("Evidence attachments are immutable.");if(entry.State!=EntityState.Added)continue;if(actor.ActorId==Guid.Empty||entry.Entity.CreatorActorId!=actor.ActorId)throw new UnauthorizedAccessException();entry.Entity.CreatorActorId=actor.ActorId;entry.Entity.CreatedAtUtc=now;}
  foreach(var entry in db.ChangeTracker.Entries<ScreeningEvidenceVerificationEntity>()){if(entry.State is EntityState.Modified or EntityState.Deleted)throw new InvalidOperationException("Verification history is immutable.");if(entry.State!=EntityState.Added)continue;if(actor.ActorId==Guid.Empty||entry.Entity.CreatorActorId!=actor.ActorId)throw new UnauthorizedAccessException();entry.Entity.CreatorActorId=actor.ActorId;entry.Entity.CreatedAtUtc=now;entry.Entity.CanonicalJson=entry.Entity.Canonical();entry.Entity.EventSha256=OfficerFlowInput.Hash(entry.Entity.CanonicalJson);}
 }
 public override InterceptionResult<int> SavingChanges(DbContextEventData data,InterceptionResult<int> result){Stamp(data.Context);return result;}
 public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default){Stamp(data.Context);return ValueTask.FromResult(result);}
}
