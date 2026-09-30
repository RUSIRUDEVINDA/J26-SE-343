namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class InvalidProposalTemplateException : WorkflowGovernanceDomainException
{
    public InvalidProposalTemplateException(string message) : base(message)
    {
    }
}
