namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class InvalidWorkflowExecutionException : WorkflowGovernanceDomainException
{
    public InvalidWorkflowExecutionException(string message) : base(message)
    {
    }
}
