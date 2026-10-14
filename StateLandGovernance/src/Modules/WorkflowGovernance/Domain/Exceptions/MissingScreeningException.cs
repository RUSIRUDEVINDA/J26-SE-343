namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class MissingScreeningException : WorkflowGovernanceDomainException
{
    public MissingScreeningException(string message) : base(message)
    {
    }
}
