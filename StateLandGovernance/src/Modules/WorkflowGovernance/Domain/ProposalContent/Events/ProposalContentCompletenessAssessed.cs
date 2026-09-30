namespace StateLandGovernance.WorkflowGovernance.Domain.ProposalContent.Events;

using System;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed record ProposalContentCompletenessAssessed(
    Guid EventId,
    DateTime OccurredOn,
    LeaseCaseId LeaseCaseId,
    ProposalContentAssessmentResultId ResultId,
    ProposalContentCompletenessOutcome Outcome,
    ProposalTemplateId TemplateId,
    string TemplateVersion,
    DocumentVersionId DocumentVersionId,
    DocumentChecksum DocumentChecksum
) : IDomainEvent;
