namespace StateLandGovernance.WorkflowGovernance.Application.DTOs;

using System;
using System.Collections.Generic;

/// <summary>
/// Read-only DTO exposing an authoritative verified fact snapshot published on a DocumentAnalysis aggregate.
/// </summary>
public sealed record VerifiedFactSnapshotDto(
    Guid Id,
    Guid DocumentAnalysisId,
    Guid GovernedDocumentId,
    Guid DocumentVersionId,
    string ChecksumAlgorithm,
    string ChecksumValue,
    Guid AnalysisRunId,
    Guid AnalysisRunResultId,
    int RunNumber,
    string ResultOutcome,
    string SnapshotOutcome,
    IReadOnlyList<VerifiedFactEntryDto> Entries,
    int SourceFactCount,
    int PublishedFactCount,
    int ConfirmedFactCount,
    int CorrectedFactCount,
    int UnsupportedFactCount,
    DateTime PublishedAt,
    Guid PublishingActorId
);

/// <summary>
/// Read-only DTO exposing an individual authoritative verified fact entry in a snapshot.
/// </summary>
public sealed record VerifiedFactEntryDto(
    Guid? SourceExtractedFactId,
    Guid VerificationId,
    string FactCode,
    FactValueDto EffectiveValue,
    string Decision,
    Guid VerifyingActorId,
    DateTime VerifiedAt,
    string SourceKind = "MachineCandidateReview",
    HumanEvidenceFactDto? HumanEvidence = null
);

public sealed record HumanEvidenceFactDto(Guid ObservationId, Guid DocumentId, Guid VersionId, string Checksum,
    int PageNumber, string Quote, string ReviewReason, Guid ActorId, DateTime RecordedAtUtc,
    string? VerifiedCapability = null, string? AuthorityScopeIdentifier = null,
    DateTime? AuthorityValidFromUtc = null, DateTime? AuthorityVerificationTimeUtc = null, DateTime? AuthorityValidUntilUtc = null);
