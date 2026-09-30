namespace StateLandGovernance.WorkflowGovernance.Domain.RequirementAssessment.Events;

using System;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed record RequirementAssessmentHumanReviewRequired(
    Guid EventId,
    DateTime OccurredOn,
    LeaseCaseId LeaseCaseId,
    AssessmentSubject Subject,
    string Reason,
    string? PolicyId,
    string? PolicyVersion
) : IDomainEvent;
