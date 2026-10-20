namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;

using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;

/// <summary>Explicit outcome gates from the approved, versioned project routing policy.</summary>
public sealed class WorkflowPrerequisiteGatePolicy
{
    public string Identifier { get; }
    public string Version { get; }
    private readonly WorkflowStageId[] _approvalRequiredStages;
    public IReadOnlyCollection<WorkflowStageId> ApprovalRequiredStages => Array.AsReadOnly(_approvalRequiredStages);

    public WorkflowPrerequisiteGatePolicy(string identifier, string version, IEnumerable<WorkflowStageId> approvalRequiredStages)
    {
        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(version))
            throw new InvalidWorkflowExecutionException("Gate policy identifier/version are required.");
        _approvalRequiredStages = approvalRequiredStages?.ToArray()
            ?? throw new InvalidWorkflowExecutionException("Gate stages are required.");
        if (_approvalRequiredStages.Any(x => x.Value == Guid.Empty) || _approvalRequiredStages.Distinct().Count() != _approvalRequiredStages.Length)
            throw new InvalidWorkflowExecutionException("Gate stage IDs must be nonempty and unique.");
        Identifier = identifier.Trim();
        Version = version.Trim();
    }

    internal bool Permits(WorkflowStageId dependentStage, WorkflowStageDecisionOutcome outcome) =>
        !_approvalRequiredStages.Contains(dependentStage) ||
        outcome is WorkflowStageDecisionOutcome.Approved or WorkflowStageDecisionOutcome.ApprovedWithConditions;
}
