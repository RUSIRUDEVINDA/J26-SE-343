namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class ConsensusParticipantNotFoundException : WorkflowGovernanceDomainException
{
    public ConsensusParticipantNotFoundException(string message) : base(message) { }
}
