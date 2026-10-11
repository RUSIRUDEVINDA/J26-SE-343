namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;

using System;

/// <summary>
/// EF Core persistence model for AnalysisRun child entity of DocumentAnalysis.
/// Maps to workflow_governance.analysis_runs.
/// </summary>
public sealed class AnalysisRunEntity
{
    public Guid Id { get; set; }
    public Guid DocumentAnalysisId { get; set; }
    public int RunNumber { get; set; }
    public Guid DocumentVersionId { get; set; }
    public string ChecksumAlgorithm { get; set; } = string.Empty;
    public string ChecksumValue { get; set; } = string.Empty;
    public string ModelProvider { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string ModelVersion { get; set; } = string.Empty;
    public string RequestedCapabilitiesJson { get; set; } = "[]";
    public DateTime RequestedAt { get; set; }
    public string State { get; set; } = string.Empty;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public DateTime? SupersededAt { get; set; }
    public string? SupersessionReason { get; set; }
    public string? FailureJson { get; set; }
    public string? ResultJson { get; set; }

    // Navigation property
    public DocumentAnalysisEntity? DocumentAnalysis { get; set; }
}
