namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class WorkflowExecutionRevisionOverflowException : WorkflowGovernanceDomainException
{
    public WorkflowExecutionRevisionOverflowException(string message) : base(message)
    {
    }
}
