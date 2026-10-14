namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public class DocumentAnalysisRunNumberOverflowException : WorkflowGovernanceDomainException
{
    public DocumentAnalysisRunNumberOverflowException(string message) : base(message) { }
}
