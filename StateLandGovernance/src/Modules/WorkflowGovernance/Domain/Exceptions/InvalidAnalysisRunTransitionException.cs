namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public class InvalidAnalysisRunTransitionException : WorkflowGovernanceDomainException
{
    public InvalidAnalysisRunTransitionException(string message) : base(message) { }
}
