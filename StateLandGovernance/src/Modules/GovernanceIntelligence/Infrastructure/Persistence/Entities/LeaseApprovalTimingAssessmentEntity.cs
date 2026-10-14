namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

/// <summary>
/// Persistence entity for a lease-approval timing assessment stored in the
/// <c>governance_intelligence.lease_approval_timing_assessments</c> table.
/// </summary>
public sealed class LeaseApprovalTimingAssessmentEntity
{
    public Guid AssessmentId { get; set; }
    public Guid WorkflowRunId { get; set; }
    public string CaseId { get; set; } = string.Empty;
    public Guid? WorkflowProjectionId { get; set; }
    public string PolicyVersion { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = string.Empty;
    public DateOnly AssessmentDateColombo { get; set; }
    public DateOnly? CompleteProposalRoadmapStartDate { get; set; }
    public DateOnly? ExpectedApprovalHandoverDate { get; set; }
    public DateOnly? ApprovalHandoverDate { get; set; }
    public int? DaysElapsed { get; set; }
    public int? DaysBeyondExpected { get; set; }
    public string TimingState { get; set; } = string.Empty;
    public string EvidenceState { get; set; } = string.Empty;
    public string AdvisoryNote { get; set; } = string.Empty;
    public string InputSha256 { get; set; } = string.Empty;

    // Shared Provenance columns
    public string SourceSystem { get; set; } = string.Empty;
    public string SourceRecordId { get; set; } = string.Empty;
    public string SourceRecordVersion { get; set; } = string.Empty;
    public DateTimeOffset SourceObservedAtUtc { get; set; }
    public bool IsSynthetic { get; set; }
    public string ProvenanceKind { get; set; } = string.Empty;
    public string? SyntheticSeedId { get; set; }
    public string? SyntheticSeedVersion { get; set; }
    public DateTimeOffset GeneratedAtUtc { get; set; }
    public string Purpose { get; set; } = string.Empty;

    public DateTimeOffset AssessedAtUtc { get; set; }
}
