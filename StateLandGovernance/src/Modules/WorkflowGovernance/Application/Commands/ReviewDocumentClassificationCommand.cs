namespace StateLandGovernance.WorkflowGovernance.Application.Commands;

using System;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;

/// <summary>
/// Command for an authorized officer to review/confirm/correct an uncertain or unclassified document classification.
/// </summary>
public sealed record ReviewDocumentClassificationCommand(
    Guid AssessmentId,
    Guid ClassifiedDocumentId,
    string Decision,
    string? CorrectedClassificationCode,
    string? Reason,
    Guid ReviewingActorId,
    VerifiedAuthorityContext AuthorityContext,
    int ExpectedRevision,
    Guid? ReviewId = null
) : ICommand;
