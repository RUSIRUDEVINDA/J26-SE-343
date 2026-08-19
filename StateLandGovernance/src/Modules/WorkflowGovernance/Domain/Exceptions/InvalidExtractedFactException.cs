namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class InvalidExtractedFactException : WorkflowGovernanceDomainException
{
    public InvalidExtractedFactException(string message) : base(message) { }
}

