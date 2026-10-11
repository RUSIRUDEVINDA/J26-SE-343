namespace StateLandGovernance.WorkflowGovernance.Application.DTOs;

using System;

/// <summary>
/// Read-only DTO exposing recorded human fact verification provenance, officer action, and decisions.
/// Preserves machine vs human provenance by showing both original candidate value and any corrected human value.
/// </summary>
public sealed record HumanFactVerificationDto(
    Guid Id,
    Guid DocumentAnalysisId,
    Guid AnalysisRunId,
    Guid AnalysisRunResultId,
    Guid ExtractedFactId,
    Guid DocumentVersionId,
    int RunNumber,
    string FactCode,
    FactValueDto OriginalMachineValue,
    string Decision,
    FactValueDto? CorrectedValue,
    string? Reason,
    DateTime VerifiedAt,
    Guid VerifyingActorId,
    string VerifiedCapability,
    string GrantedAuthorityScopeKind,
    string? GrantedAuthorityScopeIdentifier
);
