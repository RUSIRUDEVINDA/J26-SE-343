namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;

using System;

/// <summary>
/// EF Core persistence model for WorkflowExecution aggregate root.
/// Maps to workflow_governance.workflow_executions.
/// </summary>
public sealed class WorkflowExecutionEntity
{
    public Guid Id { get; set; }
    public Guid WorkflowPlanId { get; set; }
    public int WorkflowPlanRevision { get; set; }
    public Guid LeaseCaseId { get; set; }
    public string RuleSetReferenceJson { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public int Revision { get; set; }
    public string StagesJson { get; set; } = "[]";
    public string? CompletedWorkflowJson { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation property
    public LeaseCaseEntity? LeaseCase { get; set; }
}
