namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class InvalidHandoffException : WorkflowGovernanceDomainException
{
    public InvalidHandoffException(string message) : base(message)
    {
    }
}
