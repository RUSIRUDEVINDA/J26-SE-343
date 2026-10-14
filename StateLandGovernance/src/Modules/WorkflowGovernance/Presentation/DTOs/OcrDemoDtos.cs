namespace StateLandGovernance.WorkflowGovernance.Presentation.DTOs;

using System;
using System.Collections.Generic;

/// <summary>
/// Response returned by the temporary OCR and semantic analysis demonstration endpoint.
/// Exposes machine-generated recognition text, classification, and candidate facts for demo display.
/// Output is machine-generated candidate facts only and has NOT been verified or promoted to legal facts.
/// </summary>
public sealed record OcrDemoResponse(
    Guid CaseId,
    Guid GovernedDocumentId,
    Guid DocumentVersionId,
    Guid DocumentAnalysisId,
    Guid AnalysisRunId,
    string Status,
    string LanguageMode,
    string FileName,
    string MediaType,
    int PageCount,
    string FullText,
    IReadOnlyList<OcrDemoPageResponse> Pages,
    IReadOnlyList<string> Warnings,
    string GovernanceNotice,
    string? ExecutionMode = "ocr",
    DocumentClassificationResponse? Classification = null,
    IReadOnlyList<CandidateFactResponse>? CandidateFacts = null,
    ExecutionDurationResponse? DurationBreakdown = null,
    string? SemanticStatus = null,
    IReadOnlyList<CandidateFactResponse>? AllCandidates = null,
    IReadOnlyList<GovernedFieldInventoryItemResponse>? FieldInventory = null,
    int CandidateCount = 0,
    int AcceptedCount = 0,
    int RejectedCount = 0,
    int ConflictCount = 0,
    int UngroundedCount = 0,
    string? ModelIdentifier = null,
    string? PromptVersion = null,
    string? SemanticProvider = null
);

/// <summary>
/// Page-level OCR recognition result for demo display.
/// </summary>
public sealed record OcrDemoPageResponse(
    int PageNumber,
    string RawText,
    string CleanText,
    double? Confidence
);

/// <summary>
/// Document machine classification candidate for demo display.
/// </summary>
public sealed record DocumentClassificationResponse(
    string DocumentType,
    double? Confidence
);

/// <summary>
/// Machine-extracted candidate fact with evidence provenance and deterministic validation details for demo display.
/// </summary>
public sealed record CandidateFactResponse(
    string FactCode,
    string CanonicalValue,
    string ValueKind,
    double? Confidence,
    int? EvidencePage,
    string? EvidenceQuote,
    bool IsGrounded,
    string? ValidationWarning = null,
    string? RawValue = null,
    string? NormalizedValue = null,
    bool IsSemanticValid = true,
    string ValidationStatus = "Valid",
    string CandidateStatus = "Accepted",
    IReadOnlyList<string>? ValidationReasons = null,
    int Rank = 1,
    bool IsPreferred = false,
    string? SourceType = null,
    string? NormalizedEnglishValue = null
);

/// <summary>
/// Governed 17-field catalog inventory item showing extraction and verification status.
/// </summary>
public sealed record GovernedFieldInventoryItemResponse(
    string FieldCode,
    string DisplayName,
    string Status,
    string? PreferredValue,
    string? RawValue,
    int? PageNumber,
    double? Confidence,
    bool IsGrounded,
    int CandidateCount,
    string? SourceType = null,
    string? NormalizedEnglishValue = null
);

/// <summary>
/// Operational pipeline duration breakdown for demo display.
/// </summary>
public sealed record ExecutionDurationResponse(
    double? OcrDurationMs = null,
    double? SemanticDurationMs = null,
    double? TotalDurationMs = null
);
