namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;

using System;

/// <summary>
/// EF Core persistence model for VerifiedFactSnapshot.
/// Maps to workflow_governance.verified_fact_snapshots.
/// Fact snapshots are strictly immutable audit records once created.
/// </summary>
public sealed class VerifiedFactSnapshotEntity
{
    public Guid Id { get; set; }
    public Guid DocumentAnalysisId { get; set; }
    public Guid GovernedDocumentId { get; set; }
    public Guid DocumentVersionId { get; set; }
    public string ChecksumAlgorithm { get; set; } = string.Empty;
    public string ChecksumValue { get; set; } = string.Empty;
    public Guid AnalysisRunId { get; set; }
    public Guid AnalysisRunResultId { get; set; }
    public int RunNumber { get; set; }
    public string ModelProvider { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string ModelVersion { get; set; } = string.Empty;
    public string RequestedCapabilitiesJson { get; set; } = "[]";
    public string ResultOutcome { get; set; } = string.Empty;
    public string SnapshotOutcome { get; set; } = string.Empty;
    public string EntriesJson { get; set; } = "[]";
    public int SourceFactCount { get; set; }
    public int PublishedFactCount { get; set; }
    public int ConfirmedFactCount { get; set; }
    public int CorrectedFactCount { get; set; }
    public int UnsupportedFactCount { get; set; }
    public DateTime PublishedAt { get; set; }
    public Guid PublishingActorId { get; set; }
    public string VerifiedCapability { get; set; } = string.Empty;
    public string GrantedAuthorityScopeKind { get; set; } = string.Empty;
    public string? GrantedAuthorityScopeIdentifier { get; set; }
    public string RequiredAuthorityScopeKind { get; set; } = string.Empty;
    public string RequiredAuthorityScopeIdentifier { get; set; } = string.Empty;
    public DateTime AuthorityValidFrom { get; set; }
    public DateTime AuthorityVerificationTime { get; set; }
    public DateTime AuthorityValidUntil { get; set; }

    // Navigation property
    public DocumentAnalysisEntity? DocumentAnalysis { get; set; }
}
