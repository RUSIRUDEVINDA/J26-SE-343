namespace StateLandGovernance.WorkflowGovernance.Application.Commands;

using System;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;

/// <summary>
/// Command to record an authorized human verification action against an extracted candidate fact.
/// Preserves machine vs human provenance by recording decision and human corrected value separately from the original candidate.
/// </summary>
public sealed record VerifyCandidateFactCommand(
    Guid DocumentAnalysisId,
    Guid AnalysisRunResultId,
    Guid ExtractedFactId,
    string Decision,
    FactValueDto? CorrectedValue,
    string? Reason,
    Guid VerifyingActorId,
    VerifiedAuthorityContext AuthorityContext,
    int ExpectedAnalysisRevision,
    Guid? VerificationId = null
) : ICommand;
