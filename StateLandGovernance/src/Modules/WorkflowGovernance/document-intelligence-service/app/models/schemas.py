"""Data models and API schemas."""

from typing import List, Optional
from pydantic import BaseModel, Field


class OCRPageResult(BaseModel):
    """Page-level OCR result preserving strict provenance."""

    page_number: int = Field(
        ...,
        description="1-based page number within the original document.",
        ge=1,
    )
    raw_text: str = Field(
        ...,
        description="Unaltered raw extracted text directly produced by Tesseract OCR.",
    )
    clean_text: str = Field(
        ...,
        description=(
            "Cleaned text produced by non-semantic normalization (line-ending "
            "normalization, whitespace cleanup, and NFC Unicode normalization). "
            "Contains NO spelling correction, translation, semantic rewriting, or LLM alteration."
        ),
    )
    text: str = Field(
        ...,
        description="Standard extracted text for backward compatibility (matches clean_text).",
    )
    character_count: int = Field(
        ...,
        description="Total character count of clean_text.",
        ge=0,
    )
    confidence: Optional[float] = Field(
        default=None,
        description=(
            "Estimated OCR engine character/token confidence score (0.0 to 100.0). "
            "CRITICAL: This is an unverified machine observation metric. "
            "It does NOT represent legal validity or human fact verification confidence."
        ),
    )


class OCRResponse(BaseModel):
    """Complete response returned for an OCR extraction request."""

    request_id: str = Field(
        ...,
        description="Correlation ID for request tracing.",
    )
    language_mode: str = Field(
        ...,
        description="Tesseract language configuration used (e.g. 'eng', 'sin', 'sin+eng').",
    )
    preprocessed: bool = Field(
        ...,
        description="Whether deterministic OpenCV image preprocessing was applied.",
    )
    page_count: int = Field(
        ...,
        description="Total number of processed pages.",
        ge=1,
    )
    pages: List[OCRPageResult] = Field(
        ...,
        description="Sequential list of page-level OCR results.",
    )
    total_character_count: int = Field(
        ...,
        description="Total characters extracted across all pages.",
        ge=0,
    )
    duration_ms: float = Field(
        ...,
        description="Total processing time in milliseconds.",
        ge=0.0,
    )


class HealthResponse(BaseModel):
    """Health check response for monitoring and readiness diagnostics."""

    status: str = Field(
        ...,
        description="'healthy' if OCR engine is operational; 'degraded' otherwise.",
    )
    tesseract_available: bool = Field(
        ...,
        description="True if Tesseract OCR binary was located and can be invoked.",
    )
    tesseract_version: Optional[str] = Field(
        default=None,
        description="Reported Tesseract binary version.",
    )
    available_languages: List[str] = Field(
        default_factory=list,
        description="List of detected Tesseract language models (e.g. ['eng', 'sin', 'osd']).",
    )


class ErrorResponse(BaseModel):
    """Standardized error envelope preventing information leakage."""

    detail: str = Field(
        ...,
        description="Sanitized human-readable error explanation.",
    )
    error_code: str = Field(
        ...,
        description="Machine-readable error classification code.",
    )
    request_id: Optional[str] = Field(
        default=None,
        description="Correlation ID associated with the failed request.",
    )


class EvidenceSpan(BaseModel):
    """Grounded evidence citation linking a candidate fact to an OCR page."""

    page_number: int = Field(..., ge=1, description="1-based page number where evidence was located.")
    quote: str = Field(..., description="Verbatim quote from the page text.")
    start_offset: Optional[int] = Field(default=None, description="Character start offset on the page.")
    end_offset: Optional[int] = Field(default=None, description="Character end offset on the page.")


class DocumentClassificationResult(BaseModel):
    """Non-authoritative document classification suggestion."""

    code: str = Field(..., description="Classification category code from the allowed taxonomy.")
    confidence: float = Field(..., ge=0.0, le=1.0, description="Heuristic model confidence score (0.0 to 1.0).")


class ExtractedCandidateFact(BaseModel):
    """Unverified machine candidate fact extracted from document text."""

    field_code: str = Field(..., description="Canonical field identifier from the catalog.")
    value_kind: str = Field(..., description="Data type of the value (Text, Integer, Decimal, etc.).")
    canonical_value: str = Field(..., description="Extracted canonical string value.")
    confidence: float = Field(..., ge=0.0, le=1.0, description="Heuristic candidate confidence score (0.0 to 1.0).")
    evidence: Optional[EvidenceSpan] = Field(default=None, description="Grounded source evidence span.")
    is_evidence_verified: bool = Field(default=False, description="Whether evidence was deterministically grounded in source OCR text.")
    validation_warning: Optional[str] = Field(default=None, description="Warning if quote could not be strictly grounded.")


class DurationBreakdown(BaseModel):
    """Operational latency breakdown across processing stages."""

    ocr_duration_ms: float = Field(..., ge=0.0, description="Time spent in OCR text extraction.")
    semantic_duration_ms: float = Field(..., ge=0.0, description="Time spent in semantic extraction.")
    total_duration_ms: float = Field(..., ge=0.0, description="Total pipeline execution duration.")


class AnalyzeResponse(BaseModel):
    """Complete structured response for governed semantic document intelligence."""

    request_id: str = Field(..., description="Correlation ID for request tracing.")
    language_mode: str = Field(..., description="OCR language configuration used.")
    preprocessed: bool = Field(..., description="Whether OpenCV preprocessing was applied.")
    page_count: int = Field(..., ge=1, description="Total number of processed pages.")
    pages: List[OCRPageResult] = Field(..., description="Sequential list of page OCR results.")
    classification: DocumentClassificationResult = Field(..., description="Suggested non-authoritative document classification.")
    facts: List[ExtractedCandidateFact] = Field(default_factory=list, description="List of unverified candidate facts.")
    model_identifier: str = Field(..., description="Identifier of the semantic extraction model.")
    prompt_version: str = Field(..., description="Version of the extraction prompt used.")
    duration_breakdown: DurationBreakdown = Field(..., description="Latency metrics per pipeline stage.")
    warnings: List[str] = Field(default_factory=list, description="Operational and evidence grounding warnings.")
    governance_notice: str = Field(
        default="Machine-extracted candidate information. Not human verified. Not authoritative.",
        description="Mandatory governance disclaimer.",
    )
