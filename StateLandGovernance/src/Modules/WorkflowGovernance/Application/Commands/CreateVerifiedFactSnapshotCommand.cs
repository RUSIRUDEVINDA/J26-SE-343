namespace StateLandGovernance.WorkflowGovernance.Application.Commands;

using System;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;

/// <summary>
/// Command to create an immutable verified fact snapshot on a DocumentAnalysis aggregate.
/// Creating a snapshot records historical verified facts on the analysis aggregate,
/// but does NOT make it the current authoritative snapshot for a LeaseCase.
/// To make it authoritative for a case, LinkVerifiedFactSnapshotToCaseCommand must be invoked.
/// </summary>
public sealed record CreateVerifiedFactSnapshotCommand(
    Guid DocumentAnalysisId,
    Guid AnalysisRunResultId,
    Guid PublishingActorId,
    VerifiedAuthorityContext AuthorityContext,
    int ExpectedAnalysisRevision,
    Guid? SnapshotId = null
) : ICommand;
