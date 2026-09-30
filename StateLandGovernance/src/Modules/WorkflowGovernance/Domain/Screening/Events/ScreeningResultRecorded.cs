namespace StateLandGovernance.WorkflowGovernance.Domain.Screening.Events;

using System;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed record ScreeningResultRecorded(
    Guid EventId,
    DateTime OccurredOn,
    LeaseCaseId LeaseCaseId,
    Guid ScreeningResultId,
    Guid VerifiedFactSnapshotId,
    ScreeningOutcome Outcome) : IDomainEvent;
