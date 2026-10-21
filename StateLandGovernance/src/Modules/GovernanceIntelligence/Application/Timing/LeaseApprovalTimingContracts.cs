namespace StateLandGovernance.GovernanceIntelligence.Application.Timing;

public enum TimingBoundaryEvidenceStatus
{
    Confirmed = 1,
    Unconfirmed = 2,
    Unavailable = 3
}

public enum LeaseApprovalWorkflowState
{
    Ongoing = 1,
    Completed = 2
}

public enum LeaseApprovalTimingAvailability
{
    Available = 1,
    Unavailable = 2
}

public enum LeaseApprovalTimingExpectationState
{
    Unknown = 1,
    WithinExpectation = 2,
    ExceededExpectation = 3
}

public enum LeaseApprovalTimingUnavailabilityReason
{
    None = 0,
    RoadmapStartUnavailable = 1,
    RoadmapStartUnconfirmed = 2,
    ApprovalHandoverUnavailable = 3,
    ApprovalHandoverUnconfirmed = 4
}

/// <summary>
/// Caller-supplied evidence for one timing boundary. OccurredAt must carry an
/// explicit offset; this contract never accepts or assigns a timezone to a
/// naive DateTime value.
/// </summary>
public sealed record TimingBoundaryEvidence(
    DateTimeOffset? OccurredAt,
    TimingBoundaryEvidenceStatus Status,
    string Source,
    string SourceReference,
    string CaseId,
    Guid WorkflowRunId);

public sealed record LeaseApprovalTimingAssessmentInput(
    string CaseId,
    Guid WorkflowRunId,
    LeaseApprovalWorkflowState WorkflowState,
    TimingBoundaryEvidence? CompleteProposalRoadmapStart,
    TimingBoundaryEvidence? ApprovalHandover,
    DateTimeOffset AssessmentAt);

public sealed record LeaseApprovalTimingAssessmentResult(
    string CaseId,
    Guid WorkflowRunId,
    DateOnly? CompleteProposalRoadmapStartDate,
    DateOnly? ExpectedApprovalHandoverDate,
    DateOnly? ActualApprovalHandoverDate,
    DateOnly AssessmentDate,
    LeaseApprovalWorkflowState WorkflowState,
    LeaseApprovalTimingAvailability Availability,
    LeaseApprovalTimingExpectationState ExpectationState,
    int? CalendarDaysBeyondExpectedDate,
    LeaseApprovalTimingUnavailabilityReason UnavailabilityReason,
    string TimingPolicyVersion,
    string ExpertReportedProvenanceNote,
    TimingBoundaryEvidence? CompleteProposalRoadmapStartEvidence,
    TimingBoundaryEvidence? ApprovalHandoverEvidence);

public interface ILeaseApprovalTimingCalculator
{
    LeaseApprovalTimingAssessmentResult Assess(LeaseApprovalTimingAssessmentInput input);
}
