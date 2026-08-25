namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;

using System;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public readonly record struct WorkflowStageCode
{
    public string Value { get; }

    public WorkflowStageCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidWorkflowPlanException("WorkflowStageCode cannot be empty.");
        
        var trimmed = value.Trim();
        if (trimmed.Length > 100)
            throw new InvalidWorkflowPlanException("WorkflowStageCode is too long.");
        
        if (trimmed.Any(char.IsControl))
            throw new InvalidWorkflowPlanException("WorkflowStageCode cannot contain control characters.");

        Value = trimmed;
    }
}
