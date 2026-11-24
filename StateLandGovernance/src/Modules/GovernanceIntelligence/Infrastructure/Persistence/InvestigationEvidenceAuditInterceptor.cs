using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
using StateLandGovernance.GovernanceIntelligence.Application.OfficerFlows;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
public sealed class InvestigationEvidenceAuditInterceptor(IComplaintActorContext actor,TimeProvider clock):SaveChangesInterceptor {
 public static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
 public static string Canonical(OfficerActionEventEntity row,JsonElement data)=>JsonSerializer.Serialize(new{id=row.Id,actor=row.CreatorActorId,investigation=row.InvestigationId,request=row.RequestId,operation=row.Operation,at=row.CreatedAtUtc,data},Json);
 void Stamp(DbContext? db){if(db==null)return;var at=clock.GetUtcNow().ToUniversalTime();at=new(at.Ticks-at.Ticks%10,TimeSpan.Zero);
  foreach(var e in db.ChangeTracker.Entries<OfficerInvestigationAttachmentEntity>()){
   if(e.State is EntityState.Modified or EntityState.Deleted)throw new InvalidOperationException("Investigation evidence is immutable.");
   if(e.State!=EntityState.Added)continue;if(actor.ActorId==Guid.Empty||e.Entity.CreatorActorId!=actor.ActorId)throw new UnauthorizedAccessException();e.Entity.CreatedAtUtc=at;e.Entity.CreatorActorId=actor.ActorId;
  }
  foreach(var e in db.ChangeTracker.Entries<OfficerActionEventEntity>().Where(x=>x.Entity.Operation=="InvestigationFinalized")){
   if(e.State is EntityState.Modified or EntityState.Deleted)throw new InvalidOperationException("Finalization is immutable.");
   if(e.State!=EntityState.Added)continue;if(actor.ActorId==Guid.Empty||e.Entity.CreatorActorId!=actor.ActorId)throw new UnauthorizedAccessException();
   e.Entity.CreatedAtUtc=at;e.Entity.CreatorActorId=actor.ActorId;using var data=JsonDocument.Parse(e.Entity.DetailsJson);var canonical=Canonical(e.Entity,data.RootElement);e.Entity.DetailsJson=JsonSerializer.Serialize(new{canonical},Json);e.Entity.EventSha256=OfficerFlowInput.Hash(canonical);
  }
 }
 public override InterceptionResult<int> SavingChanges(DbContextEventData d,InterceptionResult<int> r){Stamp(d.Context);return r;}
 public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData d,InterceptionResult<int> r,CancellationToken ct=default){Stamp(d.Context);return ValueTask.FromResult(r);}
}
