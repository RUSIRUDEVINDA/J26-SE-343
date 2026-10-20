namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution.Events;

using System;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;

public sealed record WorkflowStageDecisionRevised(Guid EventId, DateTime OccurredOn,
    WorkflowExecutionId WorkflowExecutionId, WorkflowStageId WorkflowStageId,
    WorkflowStageDecisionId PreviousDecisionId, WorkflowStageDecisionId ReplacementDecisionId,
    Guid ActorId, string Reason, int ExecutionRevision) : IDomainEvent;

public sealed record WorkflowExecutionPlanAdopted(Guid EventId, DateTime OccurredOn,
    WorkflowExecutionId WorkflowExecutionId, WorkflowPlanId PreviousPlanId, WorkflowPlanId ReplacementPlanId,
    Guid ActorId, string Reason, int ExecutionRevision) : IDomainEvent;
