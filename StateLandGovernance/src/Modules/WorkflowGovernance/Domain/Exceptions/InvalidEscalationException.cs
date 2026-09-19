namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class InvalidEscalationException : WorkflowGovernanceDomainException
{
    public InvalidEscalationException(string message) : base(message)
    {
    }
}
