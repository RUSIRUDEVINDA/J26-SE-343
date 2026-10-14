namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public class DocumentVersionNumberOverflowException : WorkflowGovernanceDomainException
{
    public DocumentVersionNumberOverflowException(string message) : base(message) { }
}
