namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class ConflictingWorkflowStageDecisionException : WorkflowGovernanceDomainException
{
    public ConflictingWorkflowStageDecisionException(string message) : base(message)
    {
    }
}
