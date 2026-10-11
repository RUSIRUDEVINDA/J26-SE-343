namespace StateLandGovernance.WorkflowGovernance.Application.Commands;

using System;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;

/// <summary>
/// Command for an authorized officer to confirm a proposed proposal-content assessment result.
/// </summary>
public sealed record ConfirmProposalContentAssessmentCommand(
    Guid LeaseCaseId,
    Guid AssessmentResultId,
    Guid ActorId,
    VerifiedAuthorityContext AuthorityContext,
    int ExpectedRevision,
    string? Notes = null
) : ICommand;
