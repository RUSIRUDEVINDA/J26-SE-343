namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class DuplicateAnalysisRunResultException : WorkflowGovernanceDomainException
{
    public DuplicateAnalysisRunResultException(string message) : base(message) { }
}

