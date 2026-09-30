namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class InvalidWorkflowStageTransitionException : WorkflowGovernanceDomainException
{
    public InvalidWorkflowStageTransitionException(string message) : base(message)
    {
    }
}
