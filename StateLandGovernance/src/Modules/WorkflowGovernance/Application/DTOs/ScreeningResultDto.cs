namespace StateLandGovernance.WorkflowGovernance.Application.DTOs;

using System;

/// <summary>
/// Application DTO representing the state of screening for a lease case.
/// Exposes factual state: snapshot binding, staleness, outcome, remarks, and assessment time.
/// Does not duplicate or infer authoritative workflow progression rules.
/// </summary>
public sealed record ScreeningResultDto(
    Guid ScreeningResultId,
    Guid LeaseCaseId,
    Guid BoundVerifiedFactSnapshotId,
    Guid CurrentVerifiedFactSnapshotId,
    string Outcome,
    string? Remarks,
    DateTime AssessedAtUtc,
    bool IsStale
);
