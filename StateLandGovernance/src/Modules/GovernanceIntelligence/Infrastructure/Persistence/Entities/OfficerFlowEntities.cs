namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
public sealed class OfficerInvestigationEntity {
 public Guid Id{get;set;} public Guid CreatorActorId{get;set;} public string ProposalId{get;set;}=""; public Guid SubmissionKey{get;set;} public string SourceVersion{get;set;}=""; public string InputSha256{get;set;}=""; public string FindingsJson{get;set;}="[]"; public DateTimeOffset CreatedAtUtc{get;set;} public Guid? SupersedesId{get;set;}
}
public sealed class OfficerAssessmentRequestEntity {
 public Guid Id{get;set;} public Guid CreatorActorId{get;set;} public Guid SubmissionKey{get;set;} public string Kind{get;set;}=""; public string ScopeKey{get;set;}=""; public string SourceVersion{get;set;}=""; public string InputSha256{get;set;}=""; public Guid? InvestigationId{get;set;} public string? Scenario{get;set;} public Guid? WorkflowRunId{get;set;} public Guid? ProjectionId{get;set;} public string State{get;set;}="Pending"; public DateTimeOffset CreatedAtUtc{get;set;} public DateTimeOffset? FinishedAtUtc{get;set;} public Guid? EarlyAssessmentId{get;set;} public Guid? AnomalyAssessmentId{get;set;} public string? FailureCode{get;set;} public int Revision{get;set;}=1;
}
public sealed class OfficerActionEventEntity {
 public Guid Id{get;set;} public Guid CreatorActorId{get;set;} public Guid? InvestigationId{get;set;} public Guid? RequestId{get;set;} public string Operation{get;set;}=""; public DateTimeOffset CreatedAtUtc{get;set;} public string DetailsJson{get;set;}="{}"; public string EventSha256{get;set;}="";
}
