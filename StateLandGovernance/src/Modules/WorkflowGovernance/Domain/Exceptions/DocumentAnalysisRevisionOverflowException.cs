namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public class DocumentAnalysisRevisionOverflowException : WorkflowGovernanceDomainException
{
    public DocumentAnalysisRevisionOverflowException(string message) : base(message) { }
}
