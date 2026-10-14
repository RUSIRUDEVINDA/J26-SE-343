namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness.Events;

using System;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed record DocumentClassificationHumanReviewed(
    Guid EventId,
    DateTime OccurredOn,
    DocumentCompletenessAssessmentId DocumentCompletenessAssessmentId,
    LeaseCaseId LeaseCaseId,
    ClassificationReviewId ClassificationReviewId,
    ClassifiedDocumentId ClassifiedDocumentId,
    GovernedDocumentId GovernedDocumentId,
    DocumentVersionId DocumentVersionId,
    ClassificationReviewDecision ClassificationReviewDecision,
    string? OriginalClassificationCode,
    string? CorrectedClassificationCode,
    string? EffectiveClassificationCode,
    Guid ReviewingActorId,
    int DocumentCompletenessAssessmentRevision
) : IDomainEvent;
