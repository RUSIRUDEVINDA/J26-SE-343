namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class DuplicateWorkflowStageDecisionException : WorkflowGovernanceDomainException
{
    public DuplicateWorkflowStageDecisionException(string message) : base(message)
    {
    }
}
