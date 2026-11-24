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
public sealed class InvestigationEvidenceStore(GovernanceIntelligenceDbContext db,IComplaintActorContext actor,IComplaintOfficerDirectory directory,ICommissionerReportReadAccess commissioner,IConfiguration config):IInvestigationEvidenceStore {
 static readonly JsonSerializerOptions Json=InvestigationEvidenceAuditInterceptor.Json;
 const string Operation="InvestigationFinalized";
 DbSet<OfficerInvestigationAttachmentEntity> Files=>db.Set<OfficerInvestigationAttachmentEntity>();
 DbSet<OfficerActionEventEntity> Events=>db.Set<OfficerActionEventEntity>();
 sealed record FileBinding(Guid Id,string Concern,string Sha256);
 sealed record Completion(string Version,Guid SubmissionKey,string InputSha256,OfficerFinding[] Findings,FileBinding[] Attachments);
 static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
 async Task<(OfficerInvestigationEntity Row,bool Owner)> Scope(Guid id,bool write,CancellationToken ct){
  if(actor.ActorId==Guid.Empty)throw new UnauthorizedAccessException();var ds=(await directory.ReadAsync(ct)).Officers.Any(x=>x.ActorId==actor.ActorId);var central=!write&&await commissioner.CanReadAllAsync(actor.ActorId,ct);
  if(!ds&&!central)throw new UnauthorizedAccessException();var row=await db.Set<OfficerInvestigationEntity>().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new KeyNotFoundException();
  var owner=ds&&row.CreatorActorId==actor.ActorId;
  // Explicit saved-report permission never exposes unscreened drafts.
  if(!owner&&!(central&&await db.Set<OfficerAssessmentRequestEntity>().AnyAsync(x=>x.InvestigationId==id&&x.CreatorActorId==row.CreatorActorId&&x.Kind=="early"&&x.State=="Saved",ct)))throw new UnauthorizedAccessException();
  if(write&&!config.GetValue<bool>("GovernanceIntelligence:KiwulaWorkspace:WritesEnabled"))throw new OfficerUnavailableException();return(row,owner);
 }
 Task Lock(Guid id,CancellationToken ct)=>db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"c4-investigation-evidence:"+id},0))",ct);
 static InvestigationAttachment Metadata(OfficerInvestigationAttachmentEntity r)=>new(r.Id,r.InvestigationId,r.Concern,r.CreatorActorId,r.CreatedAtUtc,r.FileName,r.ContentType,r.Content.Length,r.ContentSha256);
 async Task<(OfficerActionEventEntity Row,Completion Data)?> Final(Guid id,CancellationToken ct){
  var row=await Events.AsNoTracking().SingleOrDefaultAsync(x=>x.InvestigationId==id&&x.Operation==Operation,ct);if(row==null)return null;
  try{using var wrapper=JsonDocument.Parse(row.DetailsJson);var canonical=wrapper.RootElement.GetProperty("canonical").GetString()!;using var document=JsonDocument.Parse(canonical);var data=document.RootElement.GetProperty("data");
   if(InvestigationEvidenceAuditInterceptor.Canonical(row,data)!=canonical||OfficerFlowInput.Hash(canonical)!=row.EventSha256)throw new OfficerUnavailableException();
   var result=data.Deserialize<Completion>(Json)!;if(result.Version!="investigation-finalization/1"||result.SubmissionKey==Guid.Empty)throw new OfficerUnavailableException();return(row,result);
  }catch(JsonException){throw new OfficerUnavailableException();}
 }
 static InvestigationFinalization View(OfficerActionEventEntity r,Completion c)=>new(r.Id,r.InvestigationId!.Value,r.CreatorActorId,r.CreatedAtUtc,c.SubmissionKey,c.InputSha256,r.EventSha256);
 public async Task<InvestigationEvidenceView> ReadAsync(Guid id,CancellationToken ct){var scope=await Scope(id,false,ct);var files=await Files.AsNoTracking().Where(x=>x.InvestigationId==id).OrderBy(x=>x.CreatedAtUtc).ThenBy(x=>x.Id).Select(x=>new InvestigationAttachment(x.Id,x.InvestigationId,x.Concern,x.CreatorActorId,x.CreatedAtUtc,x.FileName,x.ContentType,x.Content.Length,x.ContentSha256)).Take(100).ToArrayAsync(ct);var final=await Final(id,ct);await Scope(id,false,ct);return new(scope.Owner&&final==null,files,final is {} f?View(f.Row,f.Data):null);}
 public async Task<InvestigationAttachment> AttachAsync(Guid id,AttachScreeningEvidence input,CancellationToken ct){
  await Scope(id,true,ct);input=ManualEvidenceInput.Validate(input);var contentHash=Hash(input.Content);var inputHash=OfficerFlowInput.Hash(JsonSerializer.Serialize(new{id,input.Concern,input.FileName,input.ContentType,contentHash},Json));
  await using var tx=await db.Database.BeginTransactionAsync(ct);
  // Upload-key lock precedes resource lock, consistently for all uploads.
  await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"c4-investigation-upload:"+actor.ActorId+":"+input.SubmissionKey},0))",ct);await Lock(id,ct);await Scope(id,true,ct);
  var old=await Files.AsNoTracking().SingleOrDefaultAsync(x=>x.CreatorActorId==actor.ActorId&&x.SubmissionKey==input.SubmissionKey,ct);if(old!=null){if(old.InputSha256!=inputHash)throw new OfficerConflictException();return Metadata(old);}
  if(await Final(id,ct)!=null)throw new OfficerConflictException();if(await Files.CountAsync(x=>x.InvestigationId==id,ct)>=100)throw new ArgumentException("Investigation evidence limit reached.");
  var row=new OfficerInvestigationAttachmentEntity{Id=Guid.NewGuid(),InvestigationId=id,CreatorActorId=actor.ActorId,SubmissionKey=input.SubmissionKey,Concern=input.Concern,FileName=input.FileName,ContentType=input.ContentType,Content=input.Content,ContentSha256=contentHash,InputSha256=inputHash};Files.Add(row);await Scope(id,true,ct);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Metadata(row);
 }
 public async Task<(InvestigationAttachment Metadata,byte[] Content)> DownloadAsync(Guid id,Guid attachment,CancellationToken ct){await Scope(id,false,ct);var row=await Files.AsNoTracking().SingleOrDefaultAsync(x=>x.InvestigationId==id&&x.Id==attachment,ct)??throw new KeyNotFoundException();if(Hash(row.Content)!=row.ContentSha256)throw new OfficerUnavailableException();await Scope(id,false,ct);return(Metadata(row),row.Content);}
 public async Task<InvestigationFinalization> FinalizeAsync(Guid id,FinalizeInvestigation input,CancellationToken ct){
  await Scope(id,true,ct);if(input.SubmissionKey==Guid.Empty)throw new ArgumentException("A finalization key is required.");await using var tx=await db.Database.BeginTransactionAsync(ct);await Lock(id,ct);var scope=await Scope(id,true,ct);
  if(scope.Row.InputSha256!=input.ExpectedInputSha256)throw new OfficerConflictException();var final=await Final(id,ct);if(final is {} saved){if(saved.Data.SubmissionKey!=input.SubmissionKey||saved.Data.InputSha256!=input.ExpectedInputSha256)throw new OfficerConflictException();return View(saved.Row,saved.Data);}
  var files=await Files.AsNoTracking().Where(x=>x.InvestigationId==id&&x.CreatorActorId==actor.ActorId).OrderBy(x=>x.Id).ToArrayAsync(ct);if(files.Any(x=>Hash(x.Content)!=x.ContentSha256))throw new OfficerUnavailableException();
  var findings=JsonSerializer.Deserialize<OfficerFinding[]>(scope.Row.FindingsJson,Json)!;InvestigationFinalizationInput.Validate(new(scope.Row.Id,scope.Row.CreatorActorId,scope.Row.ProposalId,scope.Row.SubmissionKey,scope.Row.SourceVersion,scope.Row.InputSha256,findings,scope.Row.CreatedAtUtc,scope.Row.SupersedesId),files.Select(x=>x.Concern).ToArray());
  var completion=new Completion("investigation-finalization/1",input.SubmissionKey,scope.Row.InputSha256,findings,files.Select(x=>new FileBinding(x.Id,x.Concern,x.ContentSha256)).ToArray());
  var row=new OfficerActionEventEntity{Id=Guid.NewGuid(),CreatorActorId=actor.ActorId,InvestigationId=id,Operation=Operation,DetailsJson=JsonSerializer.Serialize(completion,Json)};Events.Add(row);await Scope(id,true,ct);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return View(row,completion);
 }
 public async Task RequireFinalizedAsync(Guid expectedActor,Guid id,CancellationToken ct){
  if(actor.ActorId!=expectedActor)throw new UnauthorizedAccessException();var scope=await Scope(id,true,ct);var final=await Final(id,ct)??throw new ArgumentException("Explicitly finalize the investigation with saved evidence before screening.");
  if(final.Row.CreatorActorId!=expectedActor||final.Data.InputSha256!=scope.Row.InputSha256||JsonSerializer.Serialize(final.Data.Findings,Json)!=JsonSerializer.Serialize(JsonSerializer.Deserialize<OfficerFinding[]>(scope.Row.FindingsJson,Json),Json))throw new OfficerUnavailableException();await Scope(id,true,ct);
 }
}
