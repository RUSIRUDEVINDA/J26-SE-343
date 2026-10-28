namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
public sealed class OfficerInvestigationAttachmentEntity {
 public Guid Id{get;set;} public Guid InvestigationId{get;set;} public Guid CreatorActorId{get;set;} public Guid SubmissionKey{get;set;}
 public string Concern{get;set;}="";public string FileName{get;set;}="";public string ContentType{get;set;}="";public byte[] Content{get;set;}=[];
 public string ContentSha256{get;set;}="";public string InputSha256{get;set;}="";public DateTimeOffset CreatedAtUtc{get;set;}
}
