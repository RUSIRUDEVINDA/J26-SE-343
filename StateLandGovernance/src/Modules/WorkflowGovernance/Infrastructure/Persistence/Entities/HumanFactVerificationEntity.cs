namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;

using System;

/// <summary>
/// EF Core persistence model for HumanFactVerification child entity of DocumentAnalysis.
/// Maps to workflow_governance.human_fact_verifications.
/// </summary>
public sealed class HumanFactVerificationEntity
{
    public Guid Id { get; set; }
    public Guid DocumentAnalysisId { get; set; }
    public string FactKey { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? CorrectedValue { get; set; }
    public Guid VerifiedByActorId { get; set; }
    public DateTime VerifiedAt { get; set; }
    public string? Note { get; set; }

    // Navigation property
    public DocumentAnalysisEntity? DocumentAnalysis { get; set; }
}
