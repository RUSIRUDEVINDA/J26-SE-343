namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class ConsensusAlreadyEvaluatedException : WorkflowGovernanceDomainException
{
    public ConsensusAlreadyEvaluatedException(string message) : base(message) { }
}
