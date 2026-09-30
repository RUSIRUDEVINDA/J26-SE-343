namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public readonly record struct WorkflowStageDecisionId
{
    public Guid Value { get; }

    public WorkflowStageDecisionId(Guid value)
    {
        if (value == Guid.Empty)
            throw new InvalidWorkflowStageDecisionException("WorkflowStageDecisionId cannot be empty.");
        Value = value;
    }
}
