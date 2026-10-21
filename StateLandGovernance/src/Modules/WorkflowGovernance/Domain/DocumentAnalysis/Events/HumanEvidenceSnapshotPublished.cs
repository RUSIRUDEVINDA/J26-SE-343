namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis.Events;

using StateLandGovernance.BuildingBlocks.Events;

public sealed record HumanEvidenceSnapshotPublished(Guid EventId, DateTime OccurredOn,
    DocumentAnalysisId AnalysisId, VerifiedFactSnapshotId BaseSnapshotId,
    VerifiedFactSnapshotId SnapshotId, Guid ActorId, int HumanObservationCount, int AnalysisRevision) : IDomainEvent;
