namespace StateLandGovernance.WorkflowGovernance.Application.DTOs;

using System;
using System.Collections.Generic;

/// <summary>
/// Read-only DTO exposing a proposal-content completeness assessment result.
/// </summary>
public sealed record ProposalContentAssessmentDto(
    Guid Id,
    Guid LeaseCaseId,
    ProposalSourceBindingDto SourceBinding,
    ProposalTemplateSnapshotDto TemplateSnapshot,
    string Outcome,
    IReadOnlyList<ProposalContentRequirementDto> SatisfiedMandatory,
    IReadOnlyList<ProposalContentRequirementDto> MissingMandatory,
    IReadOnlyList<ProposalContentRequirementDto> ReviewRequiredMandatory,
    IReadOnlyList<ProposalContentRequirementDto> MissingOptional,
    IReadOnlyList<ProposalContentRequirementDto> SatisfiedOptional,
    IReadOnlyList<string> Explanations,
    IReadOnlyList<ProposalContentRequirementDto> UnresolvedOptional,
    bool IsConfirmed,
    Guid? ConfirmedByActorId,
    DateTime? ConfirmedAtUtc,
    string? ConfirmationNotes,
    Guid? ConfirmedResultOfId,
    Guid? SupersedesResultId,
    string? CorrectionReason,
    string? CorrectionEvidenceReference,
    int SequenceNumber
);

/// <summary>
/// Read-only DTO exposing exact proposal document version, template, and evaluation binding.
/// </summary>
public sealed record ProposalSourceBindingDto(
    Guid LeaseCaseId,
    Guid ProposalDocumentId,
    Guid DocumentVersionId,
    string ChecksumAlgorithm,
    string ChecksumValue,
    string TemplateId,
    string TemplateVersion,
    DateTime AssessedAt,
    string? ExtractionReference
);

/// <summary>
/// Read-only DTO exposing the immutable template snapshot used to evaluate proposal content.
/// </summary>
public sealed record ProposalTemplateSnapshotDto(
    string TemplateId,
    string TemplateVersion,
    string Name,
    string AuthoritativeSourceReference,
    string Status,
    string DefinitionDigest,
    IReadOnlyList<ProposalContentRequirementDto> Requirements
);

/// <summary>
/// Read-only DTO exposing a single proposal content requirement/section.
/// </summary>
public sealed record ProposalContentRequirementDto(
    string Id,
    string Kind,
    string DisplayName,
    bool IsMandatory,
    string TemplateId,
    string TemplateVersion,
    string AuthoritativeSourceReference,
    int OrderIndex,
    string? ApplicabilityContext
);

/// <summary>
/// DTO representing an observation of a proposal content requirement/section.
/// </summary>
public sealed record ProposalObservationDto(
    string RequirementId,
    string State,
    int? PageNumber = null,
    string? TextSpan = null,
    string? EvidenceReference = null,
    string? ExtractionReference = null,
    string? Explanation = null
);
