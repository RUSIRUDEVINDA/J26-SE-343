namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class InvalidProposalSourceBindingException : WorkflowGovernanceDomainException
{
    public InvalidProposalSourceBindingException(string message) : base(message)
    {
    }
}
