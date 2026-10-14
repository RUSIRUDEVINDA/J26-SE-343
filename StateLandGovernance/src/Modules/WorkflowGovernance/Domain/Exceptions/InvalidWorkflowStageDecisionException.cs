namespace StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class InvalidWorkflowStageDecisionException : WorkflowGovernanceDomainException
{
    public InvalidWorkflowStageDecisionException(string message) : base(message)
    {
    }
}
