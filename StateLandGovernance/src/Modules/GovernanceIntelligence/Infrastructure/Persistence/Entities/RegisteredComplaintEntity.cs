namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
public sealed class RegisteredComplaintEntity
{
    public Guid Id {get;set;}
    public Guid ApplicantId {get;set;}
    public Guid SubmissionKey {get;set;}
    public Guid? CaseId {get;set;}
    public DateTimeOffset SubmittedAtUtc {get;set;}
    public int Revision {get;set;}
    public string PayloadJson {get;set;}="";
}
public sealed class RegisteredComplaintAssessmentLinkEntity
{
    public Guid AssessmentId {get;set;}
    public Guid ComplaintId {get;set;}
}
