namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;

using System;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public readonly record struct RoutingReasonCode
{
    public string Value { get; }

    public RoutingReasonCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidWorkflowPlanException("RoutingReasonCode cannot be empty.");
        
        var trimmed = value.Trim();
        if (trimmed.Length > 100)
            throw new InvalidWorkflowPlanException("RoutingReasonCode is too long.");
        
        if (trimmed.Any(char.IsControl))
            throw new InvalidWorkflowPlanException("RoutingReasonCode cannot contain control characters.");

        Value = trimmed;
    }
}
