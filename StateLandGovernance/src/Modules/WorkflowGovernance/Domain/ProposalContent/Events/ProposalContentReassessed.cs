namespace StateLandGovernance.WorkflowGovernance.Domain.ProposalContent.Events;

using System;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed record ProposalContentReassessed(
    Guid EventId,
    DateTime OccurredOn,
    LeaseCaseId LeaseCaseId,
    ProposalContentAssessmentResultId PreviousResultId,
    ProposalContentAssessmentResultId NewResultId,
    ProposalContentCompletenessOutcome PreviousOutcome,
    ProposalContentCompletenessOutcome NewOutcome,
    string PreviousTemplateVersion,
    string NewTemplateVersion
) : IDomainEvent;
