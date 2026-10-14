namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public class DocumentVersionConflictException : WorkflowGovernanceDomainException
{
    public DocumentVersionConflictException(string message) : base(message) { }
}
