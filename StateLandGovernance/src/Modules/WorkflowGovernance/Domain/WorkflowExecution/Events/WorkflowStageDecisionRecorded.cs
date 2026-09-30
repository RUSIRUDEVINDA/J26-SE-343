namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution.Events;

using System;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;

public sealed class WorkflowStageDecisionRecorded : IDomainEvent
{
    public Guid EventId { get; }
    public DateTime OccurredOn { get; }
    public WorkflowExecutionId WorkflowExecutionId { get; }
    public WorkflowPlanId WorkflowPlanId { get; }
    public WorkflowStageId WorkflowStageId { get; }
    public WorkflowStageDecisionId WorkflowStageDecisionId { get; }
    public InstitutionCode InstitutionCode { get; }
    public WorkflowStageDecisionOutcome Outcome { get; }
    public Guid DecidingOfficerId { get; }
    public int NewlyReadyStageCount { get; }
    public WorkflowExecutionStatus WorkflowExecutionStatus { get; }
    public int WorkflowExecutionRevision { get; }

    public WorkflowStageDecisionRecorded(
        Guid eventId,
        DateTime occurredOn,
        WorkflowExecutionId workflowExecutionId,
        WorkflowPlanId workflowPlanId,
        WorkflowStageId workflowStageId,
        WorkflowStageDecisionId workflowStageDecisionId,
        InstitutionCode institutionCode,
        WorkflowStageDecisionOutcome outcome,
        Guid decidingOfficerId,
        int newlyReadyStageCount,
        WorkflowExecutionStatus workflowExecutionStatus,
        int workflowExecutionRevision)
    {
        EventId = eventId;
        OccurredOn = occurredOn;
        WorkflowExecutionId = workflowExecutionId;
        WorkflowPlanId = workflowPlanId;
        WorkflowStageId = workflowStageId;
        WorkflowStageDecisionId = workflowStageDecisionId;
        InstitutionCode = institutionCode;
        Outcome = outcome;
        DecidingOfficerId = decidingOfficerId;
        NewlyReadyStageCount = newlyReadyStageCount;
        WorkflowExecutionStatus = workflowExecutionStatus;
        WorkflowExecutionRevision = workflowExecutionRevision;
    }
}
