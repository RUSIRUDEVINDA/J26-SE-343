namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public readonly record struct WorkflowStageId
{
    public Guid Value { get; }

    public WorkflowStageId(Guid value)
    {
        if (value == Guid.Empty)
            throw new InvalidWorkflowPlanException("WorkflowStageId cannot be empty.");
        Value = value;
    }
}
