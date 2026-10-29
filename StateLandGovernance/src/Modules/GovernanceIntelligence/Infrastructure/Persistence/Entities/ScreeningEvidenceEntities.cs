using System.Text.Json;
namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
public sealed class ScreeningEvidenceAttachmentEntity {
 public Guid Id{get;set;}public Guid RequestId{get;set;}public Guid CreatorActorId{get;set;}public Guid SubmissionKey{get;set;}public string Concern{get;set;}="";public string FileName{get;set;}="";public string ContentType{get;set;}="";public byte[] Content{get;set;}=[];public string ContentSha256{get;set;}="";public string InputSha256{get;set;}="";public DateTimeOffset CreatedAtUtc{get;set;}
}
public sealed class ScreeningEvidenceVerificationEntity {
 public Guid Id{get;set;}public Guid RequestId{get;set;}public Guid CreatorActorId{get;set;}public Guid SubmissionKey{get;set;}public string Concern{get;set;}="";public int Revision{get;set;}public Guid? PreviousEventId{get;set;}public string PreviousStatus{get;set;}="Unverified";public string Status{get;set;}="";public string Remarks{get;set;}="";public string AttachmentIdsJson{get;set;}="[]";public string ReferencesJson{get;set;}="[]";public string InputSha256{get;set;}="";public DateTimeOffset CreatedAtUtc{get;set;}public string CanonicalJson{get;set;}="";public string EventSha256{get;set;}="";
 public string Canonical()=>JsonSerializer.Serialize(new{Id,RequestId,CreatorActorId,SubmissionKey,Concern,Revision,PreviousEventId,PreviousStatus,Status,Remarks,AttachmentIds=JsonSerializer.Deserialize<Guid[]>(AttachmentIdsJson),EvidenceReferences=JsonSerializer.Deserialize<string[]>(ReferencesJson),InputSha256,CreatedAtUtc});
}
