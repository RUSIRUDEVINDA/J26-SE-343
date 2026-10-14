namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class DuplicateConsensusAssessmentException : WorkflowGovernanceDomainException
{
    public DuplicateConsensusAssessmentException(string message) : base(message) { }
}
