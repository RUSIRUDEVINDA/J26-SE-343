namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public class InvalidAnalysisRunException : WorkflowGovernanceDomainException
{
    public InvalidAnalysisRunException(string message) : base(message) { }
}
