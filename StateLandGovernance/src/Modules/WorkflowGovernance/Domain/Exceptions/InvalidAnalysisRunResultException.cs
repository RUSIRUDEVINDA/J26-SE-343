namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class InvalidAnalysisRunResultException : WorkflowGovernanceDomainException
{
    public InvalidAnalysisRunResultException(string message) : base(message) { }
}

