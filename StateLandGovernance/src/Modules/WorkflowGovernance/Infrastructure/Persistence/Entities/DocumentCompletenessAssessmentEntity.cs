namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;

using System;

/// <summary>
/// EF Core persistence model for DocumentCompletenessAssessment aggregate root.
/// Maps to workflow_governance.document_completeness_assessments.
/// </summary>
public sealed class DocumentCompletenessAssessmentEntity
{
    public Guid Id { get; set; }
    public Guid LeaseCaseId { get; set; }
    public string RequirementSetIdentifier { get; set; } = string.Empty;
    public string RequirementSetVersion { get; set; } = string.Empty;
    public DateTime AssessedAt { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public int Revision { get; set; }

    public string AssessedDocumentsJson { get; set; } = "[]";
    public string ClassifiedDocumentsJson { get; set; } = "[]";
    public string RequirementsJson { get; set; } = "[]";
    public string ReviewsJson { get; set; } = "[]";
    public string MissingRequirementsJson { get; set; } = "[]";
    public DateTime UpdatedAt { get; set; }

    // Navigation property
    public LeaseCaseEntity? LeaseCase { get; set; }
}
