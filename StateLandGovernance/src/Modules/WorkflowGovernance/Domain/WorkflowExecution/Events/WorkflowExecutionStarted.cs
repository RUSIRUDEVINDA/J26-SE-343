namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution.Events;

using System;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;

public sealed class WorkflowExecutionStarted : IDomainEvent
{
    public Guid EventId { get; }
    public DateTime OccurredOn { get; }
    public WorkflowExecutionId WorkflowExecutionId { get; }
    public WorkflowPlanId WorkflowPlanId { get; }
    public int WorkflowPlanRevision { get; }
    public LeaseCaseId LeaseCaseId { get; }
    public int InitialReadyStageCount { get; }
    public int WorkflowExecutionRevision { get; }

    public WorkflowExecutionStarted(
        Guid eventId,
        DateTime occurredOn,
        WorkflowExecutionId workflowExecutionId,
        WorkflowPlanId workflowPlanId,
        int workflowPlanRevision,
        LeaseCaseId leaseCaseId,
        int initialReadyStageCount,
        int workflowExecutionRevision)
    {
        EventId = eventId;
        OccurredOn = occurredOn;
        WorkflowExecutionId = workflowExecutionId;
        WorkflowPlanId = workflowPlanId;
        WorkflowPlanRevision = workflowPlanRevision;
        LeaseCaseId = leaseCaseId;
        InitialReadyStageCount = initialReadyStageCount;
        WorkflowExecutionRevision = workflowExecutionRevision;
    }
}
