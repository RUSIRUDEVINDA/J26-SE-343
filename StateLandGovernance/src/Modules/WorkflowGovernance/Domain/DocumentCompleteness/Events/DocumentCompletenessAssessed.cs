namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness.Events;

using System;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed record DocumentCompletenessAssessed(
    Guid EventId,
    DateTime OccurredOn,
    DocumentCompletenessAssessmentId DocumentCompletenessAssessmentId,
    LeaseCaseId LeaseCaseId,
    string RequirementSetIdentifier,
    string RequirementSetVersion,
    CompletenessAssessmentOutcome CompletenessAssessmentOutcome,
    int DocumentCount,
    int ApplicableRequirementCount,
    int MissingRequirementCount,
    int HumanReviewCandidateCount,
    int DocumentCompletenessAssessmentRevision
) : IDomainEvent;
