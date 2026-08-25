namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class WorkflowStageAlreadyDecidedException : WorkflowGovernanceDomainException
{
    public WorkflowStageAlreadyDecidedException(string message) : base(message)
    {
    }
}
