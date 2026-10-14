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

    code: str = Field(..., description="Classification category code from the allowed taxonomy or 'Unavailable'.")
    confidence: Optional[float] = Field(default=None, ge=0.0, le=1.0, description="Heuristic model confidence score (0.0 to 1.0) or None if unavailable.")


class ExtractedCandidateFact(BaseModel):
    """Unverified machine candidate fact extracted from document text."""

    field_code: str = Field(..., description="Canonical field identifier from the catalog.")
    value_kind: str = Field(..., description="Data type of the value (Text, Integer, Decimal, etc.).")
    canonical_value: str = Field(..., description="Extracted canonical string value.")
    raw_value: Optional[str] = Field(default=None, description="Raw unnormalized value produced by the model.")
    normalized_value: Optional[str] = Field(default=None, description="Deterministically normalized value if available.")
    confidence: float = Field(..., ge=0.0, le=1.0, description="Heuristic candidate confidence score (0.0 to 1.0).")
    evidence: Optional[EvidenceSpan] = Field(default=None, description="Grounded source evidence span.")
    is_evidence_verified: bool = Field(default=False, description="Whether evidence was deterministically grounded in source OCR text.")
    is_semantic_valid: bool = Field(default=True, description="Whether candidate passed deterministic field semantic validation.")
    validation_status: str = Field(default="Valid", description="'Valid', 'Invalid', or 'NeedsReview'.")
    candidate_status: str = Field(default="Accepted", description="'Accepted', 'Rejected', 'Conflict', 'NeedsReview', or 'Ungrounded'.")
    validation_warning: Optional[str] = Field(default=None, description="Warning if quote could not be strictly grounded.")
    validation_reasons: List[str] = Field(default_factory=list, description="Detailed deterministic validation and governance reasons.")
    rank: int = Field(default=1, description="Relative deterministic rank among candidates for this field (1 = preferred).")
    is_preferred: bool = Field(default=False, description="True if chosen as the preferred candidate for this field.")
    source_type: Optional[str] = Field(default="Explicit", description="'Explicit', 'Derived', or 'SemanticSummary'.")
    normalized_english_value: Optional[str] = Field(default=None, description="Concise English semantic normalization if available.")


class GovernedFieldInventoryItem(BaseModel):
    """Entry in the comprehensive 17-field inventory representing extraction state."""

    field_code: str = Field(..., description="Canonical field code from the 17-field catalog.")
    display_name: str = Field(..., description="Human-readable display name.")
    status: str = Field(..., description="Field governance status: Extracted, Missing, Rejected, Conflict, Needs Review, Ungrounded.")
    preferred_value: Optional[str] = Field(default=None, description="Preferred governed value or None if missing/rejected.")
    raw_value: Optional[str] = Field(default=None, description="Raw model candidate value if produced.")
    page_number: Optional[int] = Field(default=None, description="Source page number of preferred candidate.")
    confidence: Optional[float] = Field(default=None, description="Confidence of preferred candidate.")
    is_grounded: bool = Field(default=False, description="Whether preferred candidate has verified evidence grounding.")
    candidate_count: int = Field(default=0, description="Total candidates produced for this field.")
    source_type: Optional[str] = Field(default=None, description="'Explicit', 'Derived', or 'SemanticSummary'.")
    normalized_english_value: Optional[str] = Field(default=None, description="Concise English semantic normalization if available.")


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
    facts: List[ExtractedCandidateFact] = Field(default_factory=list, description="List of unverified candidate facts (preferred and accepted).")
    all_candidates: List[ExtractedCandidateFact] = Field(default_factory=list, description="Complete set of all candidates produced (including rejected, conflicting, and ungrounded).")
    field_inventory: List[GovernedFieldInventoryItem] = Field(default_factory=list, description="Complete 17-field catalog inventory showing status and preferred values.")
    candidate_count: int = Field(default=0, description="Total candidate facts produced by the model.")
    accepted_count: int = Field(default=0, description="Number of accepted candidates.")
    rejected_count: int = Field(default=0, description="Number of rejected candidates.")
    conflict_count: int = Field(default=0, description="Number of conflicting candidates.")
    ungrounded_count: int = Field(default=0, description="Number of ungrounded candidates.")
    model_identifier: str = Field(..., description="Identifier of the semantic extraction model.")
    semantic_provider: Optional[str] = Field(default=None, description="Identifier of the semantic provider ('gemini', 'qwen', 'stub').")
    prompt_version: str = Field(..., description="Version of the extraction prompt used.")
    duration_breakdown: DurationBreakdown = Field(..., description="Latency metrics per pipeline stage.")
    warnings: List[str] = Field(default_factory=list, description="Operational and evidence grounding warnings.")
    governance_notice: str = Field(
        default="Machine-extracted candidate information. Not human verified. Not authoritative.",
        description="Mandatory governance disclaimer.",
    )
