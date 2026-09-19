namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class InvalidFulfillmentException : WorkflowGovernanceDomainException
{
    public InvalidFulfillmentException(string message) : base(message)
    {
    }
}
