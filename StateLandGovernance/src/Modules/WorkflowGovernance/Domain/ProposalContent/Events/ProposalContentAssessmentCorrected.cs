namespace StateLandGovernance.WorkflowGovernance.Domain.ProposalContent.Events;

using System;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed record ProposalContentAssessmentCorrected(
    Guid EventId,
    DateTime OccurredOn,
    LeaseCaseId LeaseCaseId,
    ProposalContentAssessmentResultId OriginalResultId,
    ProposalContentAssessmentResultId CorrectedResultId,
    Guid CorrectedByActorId,
    DateTime CorrectedAtUtc,
    string Reason
) : IDomainEvent;
