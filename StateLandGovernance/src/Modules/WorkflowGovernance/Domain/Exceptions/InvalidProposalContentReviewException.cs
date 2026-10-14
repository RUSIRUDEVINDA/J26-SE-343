namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class InvalidProposalContentReviewException : WorkflowGovernanceDomainException
{
    public InvalidProposalContentReviewException(string message) : base(message)
    {
    }
}
