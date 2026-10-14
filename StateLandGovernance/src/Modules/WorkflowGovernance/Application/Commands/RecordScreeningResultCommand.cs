namespace StateLandGovernance.WorkflowGovernance.Application.Commands;

using System;
using StateLandGovernance.BuildingBlocks.CQRS;

/// <summary>
/// Internal machine-to-machine integration command to record a trusted screening result assessed by Component 4.
/// Bound immutably to the exact ScreeningRequestId (Pending ScreeningResult.Id), LeaseCaseId, and VerifiedFactSnapshotId.
/// Presentation endpoints must not expose this command to arbitrary users/applicants.
/// </summary>
public sealed record RecordScreeningResultCommand(
    Guid LeaseCaseId,
    Guid ScreeningRequestId,
    Guid VerifiedFactSnapshotId,
    string Outcome,
    int ExpectedLeaseCaseRevision,
    Guid? ScreeningResultId = null,
    string? Remarks = null,
    DateTime? AssessedAtUtc = null
) : ICommand;
