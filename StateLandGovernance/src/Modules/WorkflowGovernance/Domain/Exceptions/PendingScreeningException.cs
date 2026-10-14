namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class PendingScreeningException : WorkflowGovernanceDomainException
{
    public PendingScreeningException(string message) : base(message)
    {
    }
}
