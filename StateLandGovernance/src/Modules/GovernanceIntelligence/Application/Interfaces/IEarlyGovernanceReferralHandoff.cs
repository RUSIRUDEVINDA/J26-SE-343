namespace StateLandGovernance.GovernanceIntelligence.Application.Interfaces;

public enum EarlyGovernanceReferralHandoffOutcome
{
    Acknowledged = 1,
    AlreadyProcessed = 2,
    NotConfigured = 3,
    TemporarilyUnavailable = 4,
    CaseWorkflowRunMismatch = 5,
    WorkflowRunNotFound = 6,
    WorkflowRunAlreadyEnded = 7,
    Rejected = 8
}

public sealed record EarlyGovernanceReferralHandoffRequest(
    Guid ReferralId,
    Guid CorrelationId,
    Guid AssessmentId,
    string CaseId,
    Guid WorkflowRunId,
    string ReasonCode,
    string Reason,
    IReadOnlyList<string> EvidenceReferences,
    DateTimeOffset RequestedAtUtc);

public sealed record EarlyGovernanceReferralHandoffResult(
    EarlyGovernanceReferralHandoffOutcome Outcome,
    string? CommissionerReviewProcessReference = null,
    string? FailureCode = null)
{
    public bool ConfirmsTransition =>
        Outcome is EarlyGovernanceReferralHandoffOutcome.Acknowledged
            or EarlyGovernanceReferralHandoffOutcome.AlreadyProcessed;
}

/// <summary>
/// Unagreed Component 3 handoff boundary. A conforming receiver must correlate the exact
/// case and workflow run, process CorrelationId idempotently, atomically prevent further
/// normal-run progression and start exactly one separate Commissioner review process.
/// Acknowledged/AlreadyProcessed must include that review process reference. Stale,
/// already-ended, missing, or mismatched runs are failures unless the same correlation was
/// previously completed. Retries use the same CorrelationId.
/// </summary>
public interface IEarlyGovernanceReferralHandoff
{
    Task<EarlyGovernanceReferralHandoffResult> SendAsync(
        EarlyGovernanceReferralHandoffRequest request,
        CancellationToken cancellationToken = default);
}
