namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
public sealed class RegulatoryEvaluationProvenanceEntity{
 public Guid EvaluationId{get;set;}
 public Guid CreatorActorId{get;set;}
 public Guid SubmissionKey{get;set;}
 public string SubmissionHash{get;set;}="";
 public string ProvenanceJson{get;set;}="";
}
