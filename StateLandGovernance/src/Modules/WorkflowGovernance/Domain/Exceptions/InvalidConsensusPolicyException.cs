namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class InvalidConsensusPolicyException : WorkflowGovernanceDomainException
{
    public InvalidConsensusPolicyException(string message) : base(message) { }
}
