namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class InvalidAssessmentPolicyException : WorkflowGovernanceDomainException
{
    public InvalidAssessmentPolicyException(string message) : base(message)
    {
    }
}
