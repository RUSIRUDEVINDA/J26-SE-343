namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;

public enum CompletenessAssessmentOutcome
{
    Complete,
    MissingRequiredDocuments,
    RequiresHumanReview,
    InsufficientInformation
}
