using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
using StateLandGovernance.GovernanceIntelligence.Application.OfficerFlows;
using StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
public sealed class ManualEvidenceStore(GovernanceIntelligenceDbContext db,IComplaintActorContext actor,IComplaintOfficerDirectory directory,ICommissionerReportReadAccess commissioner,IConfiguration config):IManualEvidenceStore {
 DbSet<ScreeningEvidenceAttachmentEntity> Attachments=>db.Set<ScreeningEvidenceAttachmentEntity>();DbSet<ScreeningEvidenceVerificationEntity> Events=>db.Set<ScreeningEvidenceVerificationEntity>();
 async Task<bool> Scope(Guid request,bool write,CancellationToken ct){
  if(actor.ActorId==Guid.Empty)throw new UnauthorizedAccessException();
  // No existence disclosure to actors without current DS eligibility or explicit report authority.
  bool ds=(await directory.ReadAsync(ct)).Officers.Any(x=>x.ActorId==actor.ActorId);bool central=!write&&await commissioner.CanReadAllAsync(actor.ActorId,ct);
  if(!ds&&!central)throw new UnauthorizedAccessException();
  var r=await db.Set<OfficerAssessmentRequestEntity>().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==request&&x.Kind=="early"&&x.State=="Saved",ct)??throw new KeyNotFoundException();
  bool owner=ds&&r.CreatorActorId==actor.ActorId;if(!owner&&!central)throw new UnauthorizedAccessException();
  if(write&&!config.GetValue<bool>("GovernanceIntelligence:KiwulaWorkspace:WritesEnabled"))throw new OfficerUnavailableException();return owner;
 }
 Task Lock(Guid request,CancellationToken ct)=>db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"c4-manual-evidence:"+request},0))",ct);
 static ScreeningEvidence Metadata(ScreeningEvidenceAttachmentEntity r)=>new(r.Id,r.RequestId,r.Concern,r.CreatorActorId,r.CreatedAtUtc,r.FileName,r.ContentType,r.Content.Length,r.ContentSha256);
 static ScreeningVerification View(ScreeningEvidenceVerificationEntity r)=>new(r.Id,r.RequestId,r.Concern,r.Revision,r.PreviousStatus,r.Status,r.CreatorActorId,r.CreatedAtUtc,r.Remarks,JsonSerializer.Deserialize<Guid[]>(r.AttachmentIdsJson)!,JsonSerializer.Deserialize<string[]>(r.ReferencesJson)!,r.EventSha256);
 public async Task<PreviousScreeningEvidencePage> PreviousAsync(Guid request,int limit,string? cursor,CancellationToken ct){
  await Scope(request,false,ct);
  if(limit is <1 or >10)throw new ArgumentException("History page size must be between 1 and 10.");
  var requests=db.Set<OfficerAssessmentRequestEntity>().AsNoTracking();
  var investigations=db.Set<OfficerInvestigationEntity>().AsNoTracking();
  var anchor=await requests.SingleAsync(x=>x.Id==request,ct);
  var source=await investigations.SingleOrDefaultAsync(x=>x.Id==anchor.InvestigationId&&x.CreatorActorId==anchor.CreatorActorId,ct)??throw new KeyNotFoundException();
  if(string.IsNullOrWhiteSpace(source.ProposalId)||anchor.ScopeKey!=source.ProposalId)throw new OfficerUnavailableException();
  var central=await commissioner.CanReadAllAsync(actor.ActorId,ct);
  // Stable persisted proposal identity, never a display label or browser-supplied proposal key.
  var query=from r in requests join i in investigations on r.InvestigationId equals (Guid?)i.Id
   where r.Kind=="early"&&r.State=="Saved"&&r.EarlyAssessmentId!=null&&r.Id!=request
    &&i.ProposalId==source.ProposalId&&r.ScopeKey==i.ProposalId&&i.CreatorActorId==r.CreatorActorId
    &&(central||r.CreatorActorId==actor.ActorId)
    &&(r.CreatedAtUtc<anchor.CreatedAtUtc||(r.CreatedAtUtc==anchor.CreatedAtUtc&&r.Id.CompareTo(anchor.Id)<0))
    &&(Attachments.Any(a=>a.RequestId==r.Id)||Events.Any(e=>e.RequestId==r.Id)) select r;
  if(cursor!=null){
   if(cursor.Length>36||!Guid.TryParse(cursor,out var lastId))throw new ArgumentException("Invalid history cursor.");
   var last=await query.SingleOrDefaultAsync(x=>x.Id==lastId,ct)??throw new ArgumentException("History cursor no longer available; refresh history.");
   await Scope(last.Id,false,ct);
   query=query.Where(x=>x.CreatedAtUtc<last.CreatedAtUtc||(x.CreatedAtUtc==last.CreatedAtUtc&&x.Id.CompareTo(last.Id)<0));
  }
  var rows=await query.OrderByDescending(x=>x.CreatedAtUtc).ThenByDescending(x=>x.Id).Take(limit+1).ToArrayAsync(ct);
  var items=new List<PreviousScreeningEvidence>();
  foreach(var row in rows.Take(limit)){var evidence=await ReadAsync(row.Id,ct);items.Add(new(row.Id,row.EarlyAssessmentId!.Value,row.CreatedAtUtc,evidence with{CanVerify=false}));}
  foreach(var item in items)await Scope(item.RequestId,false,ct);
  await Scope(request,false,ct);
  return new(items,rows.Length>limit?items[^1].RequestId.ToString():null);
 }
 public async Task<ManualEvidenceView> ReadAsync(Guid request,CancellationToken ct){var can=await Scope(request,false,ct);var attachments=await Attachments.AsNoTracking().Where(x=>x.RequestId==request).OrderBy(x=>x.CreatedAtUtc).Select(x=>new ScreeningEvidence(x.Id,x.RequestId,x.Concern,x.CreatorActorId,x.CreatedAtUtc,x.FileName,x.ContentType,x.Content.Length,x.ContentSha256)).Take(100).ToArrayAsync(ct);var history=await Events.AsNoTracking().Where(x=>x.RequestId==request).OrderBy(x=>x.Concern).ThenBy(x=>x.Revision).Take(1000).ToArrayAsync(ct);await Scope(request,false,ct);return new(can,attachments,history.Select(View).ToArray());}
 public async Task<ScreeningEvidence> AttachAsync(Guid request,AttachScreeningEvidence input,CancellationToken ct){await Scope(request,true,ct);input=ManualEvidenceInput.Validate(input);var contentHash=Convert.ToHexString(SHA256.HashData(input.Content)).ToLowerInvariant();var hash=OfficerFlowInput.Hash(JsonSerializer.Serialize(new{request,input.Concern,input.FileName,input.ContentType,contentHash}));
  await using var tx=await db.Database.BeginTransactionAsync(ct);await Lock(request,ct);await Scope(request,true,ct);
  var previous=await Attachments.AsNoTracking().SingleOrDefaultAsync(x=>x.CreatorActorId==actor.ActorId&&x.SubmissionKey==input.SubmissionKey,ct);if(previous!=null){if(previous.InputSha256!=hash)throw new OfficerConflictException();return Metadata(previous);}
  if(await Attachments.CountAsync(x=>x.RequestId==request,ct)>=100)throw new ArgumentException("This screening already has the maximum100attachments.");
  var row=new ScreeningEvidenceAttachmentEntity{Id=Guid.NewGuid(),RequestId=request,CreatorActorId=actor.ActorId,SubmissionKey=input.SubmissionKey,Concern=input.Concern,FileName=input.FileName,ContentType=input.ContentType,Content=input.Content,ContentSha256=contentHash,InputSha256=hash};Attachments.Add(row);await Scope(request,true,ct);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Metadata(row);
 }
 public async Task<ScreeningVerification> VerifyAsync(Guid request,VerifyScreeningEvidence input,CancellationToken ct){await Scope(request,true,ct);input=ManualEvidenceInput.Validate(input);var hash=OfficerFlowInput.Hash(JsonSerializer.Serialize(new{request,input}));
  await using var tx=await db.Database.BeginTransactionAsync(ct);await Lock(request,ct);await Scope(request,true,ct);
  var replay=await Events.AsNoTracking().SingleOrDefaultAsync(x=>x.CreatorActorId==actor.ActorId&&x.SubmissionKey==input.SubmissionKey,ct);if(replay!=null){if(replay.InputSha256!=hash)throw new OfficerConflictException();return View(replay);}
  var previous=await Events.AsNoTracking().Where(x=>x.RequestId==request&&x.Concern==input.Concern).OrderByDescending(x=>x.Revision).FirstOrDefaultAsync(ct);if((previous?.Revision??0)!=input.ExpectedRevision)throw new OfficerConflictException();
  if(await Events.CountAsync(x=>x.RequestId==request,ct)>=1000)throw new ArgumentException("History limit reached; retain existing events and request operator review.");
  if(await Attachments.CountAsync(x=>input.AttachmentIds.Contains(x.Id)&&x.RequestId==request&&x.Concern==input.Concern&&x.CreatorActorId==actor.ActorId,ct)!=input.AttachmentIds.Length)throw new ArgumentException("Attachments must belong to this screening and concern.");
  var row=new ScreeningEvidenceVerificationEntity{Id=Guid.NewGuid(),RequestId=request,CreatorActorId=actor.ActorId,SubmissionKey=input.SubmissionKey,Concern=input.Concern,Revision=input.ExpectedRevision+1,PreviousEventId=previous?.Id,PreviousStatus=previous?.Status??"Unverified",Status=input.Decision,Remarks=input.Remarks,AttachmentIdsJson=JsonSerializer.Serialize(input.AttachmentIds),ReferencesJson=JsonSerializer.Serialize(input.EvidenceReferences),InputSha256=hash};Events.Add(row);await Scope(request,true,ct);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return View(row);
 }
 public async Task<(ScreeningEvidence Metadata,byte[] Content)> DownloadAsync(Guid request,Guid attachment,CancellationToken ct){await Scope(request,false,ct);var row=await Attachments.AsNoTracking().SingleOrDefaultAsync(x=>x.RequestId==request&&x.Id==attachment,ct)??throw new KeyNotFoundException();if(Convert.ToHexString(SHA256.HashData(row.Content)).ToLowerInvariant()!=row.ContentSha256)throw new OfficerUnavailableException();await Scope(request,false,ct);return(Metadata(row),row.Content);}
}
