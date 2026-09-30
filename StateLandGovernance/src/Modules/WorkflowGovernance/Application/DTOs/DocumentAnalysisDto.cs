namespace StateLandGovernance.WorkflowGovernance.Application.DTOs;

using System;
using System.Collections.Generic;

/// <summary>
/// Read-only DTO exposing document analysis aggregate metadata, exact version binding, and historical runs.
/// </summary>
public sealed record DocumentAnalysisDto(
    Guid Id,
    Guid GovernedDocumentId,
    Guid DocumentVersionId,
    string ChecksumAlgorithm,
    string ChecksumValue,
    int DocumentVersionNumber,
    int SourceDocumentRevision,
    DateTime CreatedAt,
    int Revision,
    int RunCount,
    AnalysisRunDto? ActiveRun,
    IReadOnlyList<AnalysisRunDto> Runs
);

/// <summary>
/// Read-only DTO exposing an individual machine analysis run and its lifecycle state.
/// </summary>
public sealed record AnalysisRunDto(
    Guid Id,
    int RunNumber,
    string State,
    string ModelProvider,
    string ModelName,
    string ModelVersion,
    IReadOnlyList<string> RequestedCapabilities,
    DateTime RequestedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    DateTime? FailedAt,
    DateTime? SupersededAt,
    string? FailureCode,
    string? FailureDescription,
    string? SupersessionReason,
    AnalysisRunResultDto? Result
);

/// <summary>
/// Read-only DTO exposing machine-generated outputs and unverified fact candidates.
/// </summary>
public sealed record AnalysisRunResultDto(
    Guid Id,
    string Outcome,
    DateTime CompletedAt,
    IReadOnlyList<AnalysisArtifactDto> Artifacts,
    IReadOnlyList<CandidateFactDto> ExtractedCandidateFacts
);

/// <summary>
/// Read-only DTO exposing generated artifact metadata without leaking internal storage locators.
/// </summary>
public sealed record AnalysisArtifactDto(
    Guid Id,
    string ArtifactKind,
    string ContentType,
    string ChecksumAlgorithm,
    string ChecksumValue
);

/// <summary>
/// Read-only DTO explicitly representing machine-extracted candidate facts.
/// Values are candidate values awaiting human verification, NOT verified case facts.
/// </summary>
public sealed record CandidateFactDto(
    Guid Id,
    string FactCode,
    string ValueKind,
    string CandidateValue,
    decimal? ConfidenceScore,
    Guid? EvidenceArtifactId,
    int? PageNumber,
    string? Excerpt
);
