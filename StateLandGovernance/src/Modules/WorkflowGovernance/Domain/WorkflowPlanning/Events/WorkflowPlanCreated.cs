namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning.Events;

using System;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;

public sealed record WorkflowPlanCreated(
    Guid EventId,
    DateTime OccurredOn,
    WorkflowPlanId WorkflowPlanId,
    LeaseCaseId LeaseCaseId,
    WorkflowPlanSource WorkflowPlanSource,
    VerifiedFactSnapshotId VerifiedFactSnapshotId,
    DocumentCompletenessAssessmentId DocumentCompletenessAssessmentId,
    int TotalStages,
    int WorkflowPlanRevision
) : IDomainEvent;
