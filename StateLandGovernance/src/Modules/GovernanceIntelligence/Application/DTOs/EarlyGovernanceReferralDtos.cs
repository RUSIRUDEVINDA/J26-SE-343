namespace StateLandGovernance.GovernanceIntelligence.Application.DTOs;

public enum EarlyGovernanceReferralDeliveryState
{
    Pending = 1,
    Delivering = 2,
    DeliveryFailed = 3,
    Acknowledged = 4
}

public sealed record EarlyGovernanceReferralIntentDto(
    Guid ReferralId,
    Guid CorrelationId,
    Guid AssessmentId,
    string CaseId,
    Guid WorkflowRunId,
    string ReasonCode,
    string Reason,
    IReadOnlyList<string> EvidenceReferences,
    DateTimeOffset RequestedAtUtc);

public sealed record StoredEarlyGovernanceReferralDto(
    Guid ReferralId,
    Guid CorrelationId,
    Guid AssessmentId,
    string CaseId,
    Guid WorkflowRunId,
    string ReasonCode,
    string Reason,
    IReadOnlyList<string> EvidenceReferences,
    DateTimeOffset RequestedAtUtc,
    EarlyGovernanceReferralDeliveryState DeliveryState,
    int DeliveryAttemptCount,
    DateTimeOffset? LastAttemptAtUtc,
    DateTimeOffset? AcknowledgedAtUtc,
    string? CommissionerReviewProcessReference,
    string? LastFailureCode);

public sealed record EarlyGovernanceReferralDeliveryResultDto(
    StoredEarlyGovernanceReferralDto Referral,
    bool WorkflowTransitionConfirmed,
    string OutcomeCode);
