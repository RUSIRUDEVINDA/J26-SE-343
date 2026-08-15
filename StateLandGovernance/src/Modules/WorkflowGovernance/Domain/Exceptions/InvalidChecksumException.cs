namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public class InvalidChecksumException : WorkflowGovernanceDomainException
{
    public InvalidChecksumException(string message) : base(message) { }
}
