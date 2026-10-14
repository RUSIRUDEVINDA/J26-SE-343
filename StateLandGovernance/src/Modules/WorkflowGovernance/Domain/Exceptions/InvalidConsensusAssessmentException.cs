namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class InvalidConsensusAssessmentException : WorkflowGovernanceDomainException
{
    public InvalidConsensusAssessmentException(string message) : base(message) { }
}
