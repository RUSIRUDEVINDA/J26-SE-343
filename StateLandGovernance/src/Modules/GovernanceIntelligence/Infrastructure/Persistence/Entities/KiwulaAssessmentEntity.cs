namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
public sealed class KiwulaAssessmentEntity
{
    public Guid Id { get; set; }
    public Guid CreatorActorId { get; set; }
    public Guid SubmissionKey { get; set; }
    public string Engine { get; set; } = "";
    public Guid? ConflictEvaluationId { get; set; }
    public Guid? EarlyAssessmentId { get; set; }
    public string PayloadJson { get; set; } = "";
}
