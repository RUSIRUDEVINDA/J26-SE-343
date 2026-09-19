namespace StateLandGovernance.WorkflowGovernance.Domain.Tracking.Events;

using System;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed record TaskOverdueEscalated(
    Guid EventId,
    DateTime OccurredOn,
    LeaseCaseId LeaseCaseId,
    Guid TaskId,
    string Reason) : IDomainEvent;
