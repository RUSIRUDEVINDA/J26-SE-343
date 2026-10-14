namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;

using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class MissingRequiredDocument
{
    public DocumentRequirementId RequirementId { get; }
    public DocumentClassificationCode RequiredClassificationCode { get; }
    public int RequiredCount { get; }
    public int SatisfiedCount { get; }
    public int MissingCount { get; }
    public string Description { get; }

    public MissingRequiredDocument(
        DocumentRequirementId requirementId,
        DocumentClassificationCode requiredClassificationCode,
        int requiredCount,
        int satisfiedCount,
        string description)
    {
        if (requirementId.Value == System.Guid.Empty) throw new InvalidDocumentCompletenessAssessmentException("RequirementId empty.");
        if (requiredClassificationCode == null) throw new InvalidDocumentCompletenessAssessmentException("RequiredClassificationCode null.");
        if (requiredCount <= 0) throw new InvalidDocumentCompletenessAssessmentException("RequiredCount must be > 0.");
        if (satisfiedCount < 0) throw new InvalidDocumentCompletenessAssessmentException("SatisfiedCount cannot be negative.");
        
        var missing = requiredCount - satisfiedCount;
        if (missing <= 0) throw new InvalidDocumentCompletenessAssessmentException("MissingCount must be > 0.");

        RequirementId = requirementId;
        RequiredClassificationCode = requiredClassificationCode;
        RequiredCount = requiredCount;
        SatisfiedCount = satisfiedCount;
        MissingCount = missing;
        Description = description ?? throw new InvalidDocumentCompletenessAssessmentException("Description null.");
    }
}
