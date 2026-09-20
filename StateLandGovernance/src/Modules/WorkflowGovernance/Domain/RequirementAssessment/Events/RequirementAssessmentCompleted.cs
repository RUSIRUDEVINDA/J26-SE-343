namespace StateLandGovernance.WorkflowGovernance.Domain.RequirementAssessment.Events;

using System;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed record RequirementAssessmentCompleted(
    Guid EventId,
    DateTime OccurredOn,
    LeaseCaseId LeaseCaseId,
    AssessmentSubject Subject,
    RequirementAssessmentOutcome Outcome,
    string? PolicyId,
    string? PolicyVersion,
    bool RequiresHumanConfirmation
) : IDomainEvent;
