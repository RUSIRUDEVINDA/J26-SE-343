namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class ConsensusPolicyMismatchException : WorkflowGovernanceDomainException
{
    public ConsensusPolicyMismatchException(string message) : base(message) { }
}
