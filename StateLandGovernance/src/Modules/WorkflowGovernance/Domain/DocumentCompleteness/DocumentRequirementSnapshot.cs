namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;

using System;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class DocumentRequirementSnapshot
{
    public DocumentRequirementId Id { get; }
    public DocumentClassificationCode RequiredClassificationCode { get; }
    public RequirementCriticality Criticality { get; }
    public RequirementApplicability Applicability { get; }
    public int MinimumRequiredCount { get; }
    public string ReasonCode { get; }
    public string Description { get; }

    public DocumentRequirementSnapshot(
        DocumentRequirementId id,
        DocumentClassificationCode requiredClassificationCode,
        RequirementCriticality criticality,
        RequirementApplicability applicability,
        int minimumRequiredCount,
        string reasonCode,
        string description)
    {
        if (id.Value == Guid.Empty) throw new InvalidDocumentCompletenessAssessmentException("Id cannot be empty.");
        if (requiredClassificationCode == null) throw new InvalidDocumentCompletenessAssessmentException("RequiredClassificationCode is required.");
        if (minimumRequiredCount <= 0) throw new InvalidDocumentCompletenessAssessmentException("MinimumRequiredCount must be greater than zero.");
        
        if (string.IsNullOrWhiteSpace(reasonCode)) throw new InvalidDocumentCompletenessAssessmentException("ReasonCode cannot be empty.");
        var trimmedReasonCode = reasonCode.Trim();
        if (trimmedReasonCode.Any(char.IsControl) || trimmedReasonCode.Length > 255)
            throw new InvalidDocumentCompletenessAssessmentException("Invalid ReasonCode.");

        if (string.IsNullOrWhiteSpace(description)) throw new InvalidDocumentCompletenessAssessmentException("Description cannot be empty.");
        var trimmedDescription = description.Trim();
        if (trimmedDescription.Any(char.IsControl) || trimmedDescription.Length > 2000)
            throw new InvalidDocumentCompletenessAssessmentException("Invalid Description.");

        if (criticality == RequirementCriticality.Mandatory && applicability != RequirementApplicability.Required)
        {
            throw new InvalidDocumentCompletenessAssessmentException("Mandatory requirements must have Required applicability.");
        }

        Id = id;
        RequiredClassificationCode = requiredClassificationCode;
        Criticality = criticality;
        Applicability = applicability;
        MinimumRequiredCount = minimumRequiredCount;
        ReasonCode = trimmedReasonCode;
        Description = trimmedDescription;
    }
}
