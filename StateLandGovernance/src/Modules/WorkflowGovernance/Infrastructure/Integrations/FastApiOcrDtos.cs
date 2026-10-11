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
