namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public class DuplicateDocumentVersionException : WorkflowGovernanceDomainException
{
    public DuplicateDocumentVersionException(string message) : base(message) { }
}
