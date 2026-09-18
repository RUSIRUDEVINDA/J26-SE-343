namespace StateLandGovernance.WorkflowGovernance.Domain.RequirementAssessment.Events;

using System;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed record RequirementReassessed(
    Guid EventId,
    DateTime OccurredOn,
    LeaseCaseId LeaseCaseId,
    AssessmentSubject Subject,
    RequirementAssessmentOutcome NewOutcome,
    string? PreviousPolicyVersion,
    string? NewPolicyVersion
) : IDomainEvent;
