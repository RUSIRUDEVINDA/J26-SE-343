namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;

using System;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public readonly record struct InstitutionCode
{
    public string Value { get; }

    public InstitutionCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidWorkflowPlanException("InstitutionCode cannot be empty.");
        
        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length > 100)
            throw new InvalidWorkflowPlanException("InstitutionCode is too long.");
        
        if (normalized.Any(c => char.IsControl(c) || char.IsWhiteSpace(c)))
            throw new InvalidWorkflowPlanException("InstitutionCode cannot contain spaces or control characters.");

        Value = normalized;
    }
}
