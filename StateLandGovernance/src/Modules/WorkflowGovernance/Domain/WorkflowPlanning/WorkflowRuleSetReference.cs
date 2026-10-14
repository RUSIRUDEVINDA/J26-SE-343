namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;

using System;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public readonly record struct WorkflowRuleSetReference
{
    public string Identifier { get; }
    public string Version { get; }

    public WorkflowRuleSetReference(string identifier, string version)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new InvalidWorkflowPlanException("RuleSet identifier cannot be empty.");
        if (string.IsNullOrWhiteSpace(version))
            throw new InvalidWorkflowPlanException("RuleSet version cannot be empty.");

        Identifier = identifier.Trim();
        Version = version.Trim();

        if (Identifier.Any(char.IsControl) || Version.Any(char.IsControl))
            throw new InvalidWorkflowPlanException("RuleSet reference cannot contain control characters.");
    }
}
