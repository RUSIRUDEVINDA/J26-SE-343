namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class StaleScreeningException : WorkflowGovernanceDomainException
{
    public StaleScreeningException(string message) : base(message)
    {
    }
}
