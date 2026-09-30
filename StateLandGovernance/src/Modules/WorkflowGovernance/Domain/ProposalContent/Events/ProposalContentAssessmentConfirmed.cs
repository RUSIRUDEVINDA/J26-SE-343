namespace StateLandGovernance.WorkflowGovernance.Domain.ProposalContent.Events;

using System;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed record ProposalContentAssessmentConfirmed(
    Guid EventId,
    DateTime OccurredOn,
    LeaseCaseId LeaseCaseId,
    ProposalContentAssessmentResultId ResultId,
    Guid ConfirmedByActorId,
    DateTime ConfirmedAtUtc,
    string? Notes
) : IDomainEvent;
