namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class InvalidLandExtentException : WorkflowGovernanceDomainException
{
    public InvalidLandExtentException(string message) : base(message)
    {
    }
}
