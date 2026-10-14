namespace StateLandGovernance.WorkflowGovernance.Domain.ProposalContent.Events;

using System;
using System.Collections.Generic;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed record ProposalContentHumanReviewRequired(
    Guid EventId,
    DateTime OccurredOn,
    LeaseCaseId LeaseCaseId,
    ProposalContentAssessmentResultId ResultId,
    ProposalTemplateId TemplateId,
    string TemplateVersion,
    IReadOnlyCollection<string> Reasons
) : IDomainEvent;
