namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Transport DTO representing the response schema returned by FastAPI POST /v1/ocr.
/// Internal to Infrastructure; never exposed to Application or Domain.
/// Exact 1-to-1 match with FastAPI OCRResponse Pydantic schema in schemas.py.
/// </summary>
internal sealed record FastApiOcrResponseDto(
    [property: JsonPropertyName("request_id")] string RequestId,
    [property: JsonPropertyName("language_mode")] string LanguageMode,
    [property: JsonPropertyName("preprocessed")] bool Preprocessed,
    [property: JsonPropertyName("page_count")] int PageCount,
    [property: JsonPropertyName("pages")] IReadOnlyList<FastApiOcrPageDto> Pages,
    [property: JsonPropertyName("total_character_count")] int TotalCharacterCount,
    [property: JsonPropertyName("duration_ms")] double DurationMs
);

/// <summary>
/// Transport DTO representing a single page OCR result from FastAPI.
/// Exact 1-to-1 match with FastAPI OCRPageResult Pydantic schema in schemas.py.
/// </summary>
internal sealed record FastApiOcrPageDto(
    [property: JsonPropertyName("page_number")] int PageNumber,
    [property: JsonPropertyName("raw_text")] string RawText,
    [property: JsonPropertyName("clean_text")] string CleanText,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("character_count")] int CharacterCount,
    [property: JsonPropertyName("confidence")] double? Confidence
);

/// <summary>
/// Transport DTO representing standardized error responses from FastAPI.
/// Exact 1-to-1 match with FastAPI ErrorResponse Pydantic schema in schemas.py.
/// </summary>
internal sealed record FastApiErrorResponseDto(
    [property: JsonPropertyName("detail")] string? Detail,
    [property: JsonPropertyName("error_code")] string? ErrorCode,
    [property: JsonPropertyName("request_id")] string? RequestId
);

/// <summary>
/// Transport DTO representing GET /health response from FastAPI.
/// Exact 1-to-1 match with FastAPI HealthResponse Pydantic schema in schemas.py.
/// </summary>
internal sealed record FastApiHealthResponseDto(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("tesseract_available")] bool TesseractAvailable,
    [property: JsonPropertyName("tesseract_version")] string? TesseractVersion,
    [property: JsonPropertyName("available_languages")] IReadOnlyList<string> AvailableLanguages
);

/// <summary>
/// Standard schema for page-level OCR transcript artifacts.
/// Preserves raw and clean text, 1-based page number, and language mode with strict provenance.
/// Serialized to UTF-8 JSON bytes when representing OcrPageTranscript artifacts.
/// </summary>
public sealed record FastApiPageTranscriptArtifactDto(
    [property: JsonPropertyName("page_number")] int PageNumber,
    [property: JsonPropertyName("language_mode")] string LanguageMode,
    [property: JsonPropertyName("raw_text")] string RawText,
    [property: JsonPropertyName("clean_text")] string CleanText
);

/// <summary>
/// Transport DTO representing the response schema returned by FastAPI POST /v1/analyze.
/// </summary>
internal sealed record FastApiAnalyzeResponseDto(
    [property: JsonPropertyName("request_id")] string RequestId,
    [property: JsonPropertyName("language_mode")] string LanguageMode,
    [property: JsonPropertyName("preprocessed")] bool Preprocessed,
    [property: JsonPropertyName("page_count")] int PageCount,
    [property: JsonPropertyName("pages")] IReadOnlyList<FastApiOcrPageDto> Pages,
    [property: JsonPropertyName("classification")] FastApiClassificationDto Classification,
    [property: JsonPropertyName("facts")] IReadOnlyList<FastApiCandidateFactDto> Facts,
    [property: JsonPropertyName("all_candidates")] IReadOnlyList<FastApiCandidateFactDto>? AllCandidates,
    [property: JsonPropertyName("field_inventory")] IReadOnlyList<FastApiGovernedFieldInventoryItemDto>? FieldInventory,
    [property: JsonPropertyName("candidate_count")] int CandidateCount,
    [property: JsonPropertyName("accepted_count")] int AcceptedCount,
    [property: JsonPropertyName("rejected_count")] int RejectedCount,
    [property: JsonPropertyName("conflict_count")] int ConflictCount,
    [property: JsonPropertyName("ungrounded_count")] int UngroundedCount,
    [property: JsonPropertyName("model_identifier")] string ModelIdentifier,
    [property: JsonPropertyName("prompt_version")] string PromptVersion,
    [property: JsonPropertyName("duration_breakdown")] FastApiDurationBreakdownDto DurationBreakdown,
    [property: JsonPropertyName("warnings")] IReadOnlyList<string>? Warnings,
    [property: JsonPropertyName("governance_notice")] string GovernanceNotice,
    [property: JsonPropertyName("semantic_provider")] string? SemanticProvider = null
);

internal sealed record FastApiClassificationDto(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("confidence")] double? Confidence
);

internal sealed record FastApiCandidateFactDto(
    [property: JsonPropertyName("field_code")] string FieldCode,
    [property: JsonPropertyName("value_kind")] string ValueKind,
    [property: JsonPropertyName("canonical_value")] string CanonicalValue,
    [property: JsonPropertyName("raw_value")] string? RawValue,
    [property: JsonPropertyName("normalized_value")] string? NormalizedValue,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("evidence")] FastApiEvidenceSpanDto? Evidence,
    [property: JsonPropertyName("is_evidence_verified")] bool IsEvidenceVerified,
    [property: JsonPropertyName("is_semantic_valid")] bool IsSemanticValid,
    [property: JsonPropertyName("validation_status")] string? ValidationStatus,
    [property: JsonPropertyName("candidate_status")] string? CandidateStatus,
    [property: JsonPropertyName("validation_warning")] string? ValidationWarning,
    [property: JsonPropertyName("validation_reasons")] IReadOnlyList<string>? ValidationReasons,
    [property: JsonPropertyName("rank")] int Rank,
    [property: JsonPropertyName("is_preferred")] bool IsPreferred,
    [property: JsonPropertyName("source_type")] string? SourceType = null,
    [property: JsonPropertyName("normalized_english_value")] string? NormalizedEnglishValue = null
);

internal sealed record FastApiGovernedFieldInventoryItemDto(
    [property: JsonPropertyName("field_code")] string FieldCode,
    [property: JsonPropertyName("display_name")] string DisplayName,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("preferred_value")] string? PreferredValue,
    [property: JsonPropertyName("raw_value")] string? RawValue,
    [property: JsonPropertyName("page_number")] int? PageNumber,
    [property: JsonPropertyName("confidence")] double? Confidence,
    [property: JsonPropertyName("is_grounded")] bool IsGrounded,
    [property: JsonPropertyName("candidate_count")] int CandidateCount,
    [property: JsonPropertyName("source_type")] string? SourceType = null,
    [property: JsonPropertyName("normalized_english_value")] string? NormalizedEnglishValue = null
);

internal sealed record FastApiEvidenceSpanDto(
    [property: JsonPropertyName("page_number")] int PageNumber,
    [property: JsonPropertyName("quote")] string Quote,
    [property: JsonPropertyName("start_offset")] int? StartOffset,
    [property: JsonPropertyName("end_offset")] int? EndOffset
);

internal sealed record FastApiDurationBreakdownDto(
    [property: JsonPropertyName("ocr_duration_ms")] double OcrDurationMs,
    [property: JsonPropertyName("semantic_duration_ms")] double SemanticDurationMs,
    [property: JsonPropertyName("total_duration_ms")] double TotalDurationMs
);
