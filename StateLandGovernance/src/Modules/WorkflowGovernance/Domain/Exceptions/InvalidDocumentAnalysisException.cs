namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public class InvalidDocumentAnalysisException : WorkflowGovernanceDomainException
{
    public InvalidDocumentAnalysisException(string message) : base(message) { }
}
