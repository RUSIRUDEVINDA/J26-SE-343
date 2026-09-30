namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class InvalidProposalContentAssessmentException : WorkflowGovernanceDomainException
{
    public InvalidProposalContentAssessmentException(string message) : base(message)
    {
    }
}
