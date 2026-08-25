namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class ConflictingConsensusAssessmentException : WorkflowGovernanceDomainException
{
    public ConflictingConsensusAssessmentException(string message) : base(message) { }
}
