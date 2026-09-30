namespace StateLandGovernance.WorkflowGovernance.Domain.Screening;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed record ScreeningResult(
    Guid Id,
    LeaseCaseId LeaseCaseId,
    Guid VerifiedFactSnapshotId,
    ScreeningOutcome Outcome,
    string? Remarks,
    DateTime AssessedAtUtc)
{
    public bool IsStale(Guid currentVerifiedFactSnapshotId) => VerifiedFactSnapshotId != currentVerifiedFactSnapshotId;
}
