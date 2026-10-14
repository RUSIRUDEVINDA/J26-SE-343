namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class InvalidProposalIntakeException : WorkflowGovernanceDomainException
{
    public InvalidProposalIntakeException(string message) : base(message)
    {
    }
}
