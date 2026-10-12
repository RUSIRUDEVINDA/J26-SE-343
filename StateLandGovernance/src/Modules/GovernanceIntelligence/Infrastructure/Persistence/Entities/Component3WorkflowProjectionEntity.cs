namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

/// <summary>
/// Persistence entity for a Component 3 workflow governance projection stored in the
/// <c>governance_intelligence.component3_workflow_projections</c> table.
/// </summary>
public sealed class Component3WorkflowProjectionEntity
{
    public Guid ProjectionId { get; set; }
    public Guid WorkflowRunId { get; set; }
    public string CaseId { get; set; } = string.Empty;
    public string WorkflowType { get; set; } = string.Empty;
    public string TraceStatus { get; set; } = string.Empty;
    public string TraceCompleteness { get; set; } = string.Empty;
    public string CurrentOrTerminalStageCode { get; set; } = string.Empty;
    public string EventTimestampSemantics { get; set; } = string.Empty;
    public string EventTimeZone { get; set; } = string.Empty;

    // Scalar summary columns for SQL-level check constraints + validated ordered JSONB snapshot
    public int EventCount { get; set; }
    public DateTimeOffset FirstEventTimestampUtc { get; set; }
    public DateTimeOffset LastEventTimestampUtc { get; set; }
    public string FinalEventActivity { get; set; } = string.Empty;
    public string EventsSnapshotJson { get; set; } = "[]";

    // Timing boundary evidence: CompleteProposalRoadmapStart
    public string CompleteProposalRoadmapStartStatus { get; set; } = string.Empty;
    public DateTimeOffset? CompleteProposalRoadmapStartOccurredAt { get; set; }
    public string CompleteProposalRoadmapStartSource { get; set; } = string.Empty;
    public string CompleteProposalRoadmapStartSourceReference { get; set; } = string.Empty;

    // Timing boundary evidence: ApprovalHandover
    public string ApprovalHandoverStatus { get; set; } = string.Empty;
    public DateTimeOffset? ApprovalHandoverOccurredAt { get; set; }
    public string ApprovalHandoverSource { get; set; } = string.Empty;
    public string ApprovalHandoverSourceReference { get; set; } = string.Empty;

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

    public DateTimeOffset IngestedAtUtc { get; set; }
}
