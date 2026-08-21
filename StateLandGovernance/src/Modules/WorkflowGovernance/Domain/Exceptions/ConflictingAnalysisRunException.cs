namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public class ConflictingAnalysisRunException : WorkflowGovernanceDomainException
{
    public ConflictingAnalysisRunException(string message) : base(message) { }
}
