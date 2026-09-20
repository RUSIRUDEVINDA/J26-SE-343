namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public readonly record struct WorkflowExecutionId
{
    public Guid Value { get; }

    public WorkflowExecutionId(Guid value)
    {
        if (value == Guid.Empty)
            throw new InvalidWorkflowExecutionException("WorkflowExecutionId cannot be empty.");
        Value = value;
    }
}
