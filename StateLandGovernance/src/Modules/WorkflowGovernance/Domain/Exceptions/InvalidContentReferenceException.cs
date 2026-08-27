namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public class InvalidContentReferenceException : WorkflowGovernanceDomainException
{
    public InvalidContentReferenceException(string message) : base(message) { }
}
