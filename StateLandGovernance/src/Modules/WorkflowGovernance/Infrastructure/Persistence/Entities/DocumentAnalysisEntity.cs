namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;

using System;
using System.Collections.Generic;

/// <summary>
/// EF Core persistence model for DocumentAnalysis aggregate root.
/// Maps to workflow_governance.document_analyses.
/// </summary>
public sealed class DocumentAnalysisEntity
{
    public Guid Id { get; set; }
    public Guid GovernedDocumentId { get; set; }
    public Guid DocumentVersionId { get; set; }
    public string ChecksumAlgorithm { get; set; } = string.Empty;
    public string ChecksumValue { get; set; } = string.Empty;
    public int DocumentVersionNumber { get; set; }
    public int SourceDocumentRevision { get; set; }
    public DateTime CreatedAt { get; set; }
    public int Revision { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public GovernedDocumentEntity? GovernedDocument { get; set; }
    public ICollection<AnalysisRunEntity> Runs { get; set; } = new List<AnalysisRunEntity>();
    public ICollection<HumanFactVerificationEntity> Verifications { get; set; } = new List<HumanFactVerificationEntity>();
    public ICollection<VerifiedFactSnapshotEntity> VerifiedFactSnapshots { get; set; } = new List<VerifiedFactSnapshotEntity>();
}
