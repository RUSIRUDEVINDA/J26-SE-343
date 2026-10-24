namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

/// <summary>
/// Persistence entity for a workflow-anomaly assessment stored in the
/// <c>governance_intelligence.workflow_anomaly_assessments</c> table.
/// </summary>
public sealed class WorkflowAnomalyAssessmentEntity
{
    public Guid AssessmentId { get; set; }
    public Guid WorkflowRunId { get; set; }
    public string CaseId { get; set; } = string.Empty;
    public Guid? WorkflowProjectionId { get; set; }
    public string ModelVersion { get; set; } = string.Empty;
    public string? ModelArtifactSha256 { get; set; }
    public double AnomalyScore { get; set; }
    public double Threshold { get; set; }
    public bool Flagged { get; set; }
    public int EventCount { get; set; }
    public double ElapsedDays { get; set; }
    public double MaxGapHours { get; set; }
    public double MeanGapHours { get; set; }
    public int UniqueActivities { get; set; }
    public int RepeatedActivityCount { get; set; }
    public int ResourceHandoffs { get; set; }
    public int InstitutionSwitches { get; set; }
    public int DistinctResources { get; set; }
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
