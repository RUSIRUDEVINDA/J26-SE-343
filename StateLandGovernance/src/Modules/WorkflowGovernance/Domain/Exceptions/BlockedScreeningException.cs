namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class BlockedScreeningException : WorkflowGovernanceDomainException
{
    public BlockedScreeningException(string message) : base(message)
    {
    }
}
