namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;

using System;
using System.Collections.Generic;

/// <summary>
/// EF Core persistence model for GovernedDocument aggregate root.
/// Maps to workflow_governance.governed_documents.
/// </summary>
public sealed class GovernedDocumentEntity
{
    public Guid Id { get; set; }
    public Guid LeaseCaseId { get; set; }
    public string LogicalCategory { get; set; } = string.Empty;
    public Guid ActiveVersionId { get; set; }
    public int Revision { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public LeaseCaseEntity? LeaseCase { get; set; }
    public ICollection<DocumentVersionEntity> Versions { get; set; } = new List<DocumentVersionEntity>();
    public ICollection<DocumentAnalysisEntity> DocumentAnalyses { get; set; } = new List<DocumentAnalysisEntity>();
}
