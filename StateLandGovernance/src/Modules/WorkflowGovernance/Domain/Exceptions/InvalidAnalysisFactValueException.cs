namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class InvalidAnalysisFactValueException : WorkflowGovernanceDomainException
{
    public InvalidAnalysisFactValueException(string message) : base(message) { }
}

