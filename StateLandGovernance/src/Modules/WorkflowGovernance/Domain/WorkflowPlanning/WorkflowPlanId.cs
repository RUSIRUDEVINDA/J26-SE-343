namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public readonly record struct WorkflowPlanId
{
    public Guid Value { get; }

    public WorkflowPlanId(Guid value)
    {
        if (value == Guid.Empty)
            throw new InvalidWorkflowPlanException("WorkflowPlanId cannot be empty.");
        Value = value;
    }
}
