namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution.Events;

using System;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;

public sealed class WorkflowStageStarted : IDomainEvent
{
    public Guid EventId { get; }
    public DateTime OccurredOn { get; }
    public WorkflowExecutionId WorkflowExecutionId { get; }
    public WorkflowPlanId WorkflowPlanId { get; }
    public WorkflowStageId WorkflowStageId { get; }
    public WorkflowStageCode WorkflowStageCode { get; }
    public InstitutionCode InstitutionCode { get; }
    public Guid ActingOfficerId { get; }
    public int WorkflowExecutionRevision { get; }

    public WorkflowStageStarted(
        Guid eventId,
        DateTime occurredOn,
        WorkflowExecutionId workflowExecutionId,
        WorkflowPlanId workflowPlanId,
        WorkflowStageId workflowStageId,
        WorkflowStageCode workflowStageCode,
        InstitutionCode institutionCode,
        Guid actingOfficerId,
        int workflowExecutionRevision)
    {
        EventId = eventId;
        OccurredOn = occurredOn;
        WorkflowExecutionId = workflowExecutionId;
        WorkflowPlanId = workflowPlanId;
        WorkflowStageId = workflowStageId;
        WorkflowStageCode = workflowStageCode;
        InstitutionCode = institutionCode;
        ActingOfficerId = actingOfficerId;
        WorkflowExecutionRevision = workflowExecutionRevision;
    }
}
