namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class WorkflowStageExecutionNotFoundException : WorkflowGovernanceDomainException
{
    public WorkflowStageExecutionNotFoundException(string message) : base(message)
    {
    }
}
