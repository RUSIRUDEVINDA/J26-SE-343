namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

public sealed class EarlyGovernanceReferralEntity
{
    public Guid ReferralId { get; set; }
    public Guid CorrelationId { get; set; }
    public Guid AssessmentId { get; set; }
    public string CaseId { get; set; } = string.Empty;
    public Guid WorkflowRunId { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string EvidenceReferencesJson { get; set; } = "[]";
    public DateTimeOffset RequestedAtUtc { get; set; }
    public string DeliveryState { get; set; } = string.Empty;
    public int DeliveryAttemptCount { get; set; }
    public DateTimeOffset? LastAttemptAtUtc { get; set; }
    public DateTimeOffset? AcknowledgedAtUtc { get; set; }
    public string? CommissionerReviewProcessReference { get; set; }
    public string? LastFailureCode { get; set; }
    public EarlyGovernanceScreeningEvaluationEntity? Assessment { get; set; }
}
