namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning.Events;

using System;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed record WorkflowPlanSuperseded(
    Guid EventId,
    DateTime OccurredOn,
    WorkflowPlanId WorkflowPlanId,
    LeaseCaseId LeaseCaseId,
    Guid SupersedingActorId,
    string Reason,
    int WorkflowPlanRevision
) : IDomainEvent;
