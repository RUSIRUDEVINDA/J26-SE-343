namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public class InvalidDocumentException : WorkflowGovernanceDomainException
{
    public InvalidDocumentException(string message) : base(message) { }
}
