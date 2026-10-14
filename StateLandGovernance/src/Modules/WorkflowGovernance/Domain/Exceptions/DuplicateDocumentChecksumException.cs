namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public class DuplicateDocumentChecksumException : WorkflowGovernanceDomainException
{
    public DuplicateDocumentChecksumException(string message) : base(message) { }
}
