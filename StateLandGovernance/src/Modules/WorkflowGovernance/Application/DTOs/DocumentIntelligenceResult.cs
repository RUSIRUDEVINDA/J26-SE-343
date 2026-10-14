namespace StateLandGovernance.WorkflowGovernance.Application.DTOs;

using System;
using System.Collections.Generic;

/// <summary>
/// Machine-generated output returned by the external intelligence port.
/// Represents unverified candidates and generated artifacts, NOT verified case truth.
/// </summary>
public sealed record DocumentIntelligenceResult(
    string Provider,
    string ModelName,
    string ModelVersion,
    string Outcome,
    IReadOnlyList<AnalysisArtifactOutputDto> Artifacts,
    IReadOnlyList<ExtractedCandidateFactDto> Candidates,
    IReadOnlyList<string>? Warnings = null
);

public sealed record AnalysisArtifactOutputDto(
    Guid? ArtifactId,
    string ArtifactKind,
    string StorageReference,
    string ContentType,
    string ChecksumAlgorithm,
    string ChecksumValue
);

public sealed record ExtractedCandidateFactDto(
    Guid? FactId,
    string FactCode,
    string ValueKind,
    string CanonicalValue,
    decimal? ConfidenceScore,
    Guid? EvidenceArtifactId,
    int? PageNumber,
    string? Excerpt
);
