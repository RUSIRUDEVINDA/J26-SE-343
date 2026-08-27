namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public class DuplicateAnalysisRunException : WorkflowGovernanceDomainException
{
    public DuplicateAnalysisRunException(string message) : base(message) { }
}
