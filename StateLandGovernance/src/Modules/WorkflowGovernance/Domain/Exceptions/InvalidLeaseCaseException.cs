namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public class InvalidLeaseCaseException : WorkflowGovernanceDomainException
{
    public InvalidLeaseCaseException(string message) : base(message) { }
}
