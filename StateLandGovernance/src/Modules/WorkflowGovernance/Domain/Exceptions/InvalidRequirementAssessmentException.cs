namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class InvalidRequirementAssessmentException : WorkflowGovernanceDomainException
{
    public InvalidRequirementAssessmentException(string message) : base(message)
    {
    }
}
