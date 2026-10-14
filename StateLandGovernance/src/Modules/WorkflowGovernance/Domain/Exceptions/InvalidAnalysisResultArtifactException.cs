namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class InvalidAnalysisResultArtifactException : WorkflowGovernanceDomainException
{
    public InvalidAnalysisResultArtifactException(string message) : base(message) { }
}

