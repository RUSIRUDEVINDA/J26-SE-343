namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;

using System;

/// <summary>
/// EF Core persistence model for DocumentVersion child entity of GovernedDocument.
/// Maps to workflow_governance.document_versions.
/// Document content is referenced by StoragePath; bytes are stored in external document storage, NOT in PostgreSQL.
/// </summary>
public sealed class DocumentVersionEntity
{
    public Guid Id { get; set; }
    public Guid GovernedDocumentId { get; set; }
    public int VersionNumber { get; set; }
    public Guid? PredecessorVersionId { get; set; }
    public string ChecksumAlgorithm { get; set; } = string.Empty;
    public string ChecksumValue { get; set; } = string.Empty;
    public string StorageKind { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public long FileSizeInBytes { get; set; }
    public Guid SubmittedByActorId { get; set; }
    public DateTime SubmittedAt { get; set; }
    public string State { get; set; } = string.Empty;

    // Navigation property
    public GovernedDocumentEntity? GovernedDocument { get; set; }
}
