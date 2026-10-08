namespace StateLandGovernance.WorkflowGovernance.Application.DTOs;

using System;
using System.Collections.Generic;

/// <summary>
/// Read-only DTO exposing a supporting-document completeness assessment.
/// </summary>
public sealed record DocumentCompletenessAssessmentDto(
    Guid Id,
    Guid LeaseCaseId,
    string RequirementSetIdentifier,
    string RequirementSetVersion,
    DateTime AssessedAt,
    string Outcome,
    int Revision,
    IReadOnlyList<AssessedDocumentBindingDto> AssessedDocuments,
    IReadOnlyList<ClassifiedDocumentDto> ClassifiedDocuments,
    IReadOnlyList<DocumentRequirementSnapshotDto> Requirements,
    IReadOnlyList<ClassificationReviewDto> Reviews,
    IReadOnlyList<MissingRequiredDocumentDto> MissingRequirements
);

/// <summary>
/// Read-only DTO binding an assessed governed document and version.
/// </summary>
public sealed record AssessedDocumentBindingDto(
    Guid GovernedDocumentId,
    Guid DocumentVersionId,
    string ChecksumAlgorithm,
    string ChecksumValue
);

/// <summary>
/// Read-only DTO exposing a classified document and its machine/human classification state.
/// </summary>
public sealed record ClassifiedDocumentDto(
    Guid Id,
    AssessedDocumentBindingDto DocumentBinding,
    string? OriginalClassificationCode,
    decimal? ConfidenceScore,
    string Status,
    DateTime ClassifiedAt
);

/// <summary>
/// Read-only DTO exposing a required document requirement definition snapshot.
/// </summary>
public sealed record DocumentRequirementSnapshotDto(
    Guid Id,
    string RequiredClassificationCode,
    int MinimumRequiredCount,
    string Applicability,
    string Criticality,
    string ReasonCode,
    string Description
);

/// <summary>
/// Read-only DTO exposing an authorized officer's classification review action.
/// </summary>
public sealed record ClassificationReviewDto(
    Guid Id,
    Guid ClassifiedDocumentId,
    string? OriginalClassificationCode,
    string Decision,
    string? CorrectedClassificationCode,
    string? Reason,
    Guid ReviewingActorId,
    DateTime ReviewedAt
);

/// <summary>
/// Read-only DTO exposing an unmet document requirement deficit.
/// </summary>
public sealed record MissingRequiredDocumentDto(
    Guid RequirementId,
    string RequiredClassificationCode,
    int RequiredCount,
    int SatisfiedCount,
    string Description
);
