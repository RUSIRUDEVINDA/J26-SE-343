namespace StateLandGovernance.WorkflowGovernance.Application.Commands;

using System;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;

/// <summary>
/// Command to link an already-created verified fact snapshot to a LeaseCase.
/// Makes the snapshot the authoritative CurrentVerifiedFactSnapshot for the case.
/// Enforces dual-revision optimistic concurrency checks on both aggregates before committing.
/// </summary>
public sealed record LinkVerifiedFactSnapshotToCaseCommand(
    Guid LeaseCaseId,
    Guid DocumentAnalysisId,
    Guid SnapshotId,
    int ExpectedLeaseCaseRevision,
    int ExpectedAnalysisRevision
) : ICommand;
