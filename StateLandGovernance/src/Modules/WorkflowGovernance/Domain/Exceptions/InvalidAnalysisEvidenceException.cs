namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class InvalidAnalysisEvidenceException : WorkflowGovernanceDomainException
{
    public InvalidAnalysisEvidenceException(string message) : base(message) { }
}

