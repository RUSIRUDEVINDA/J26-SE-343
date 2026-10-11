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
