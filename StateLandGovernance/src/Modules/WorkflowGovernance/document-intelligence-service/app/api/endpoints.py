"""FastAPI HTTP API route endpoints."""

import uuid
from typing import Optional
from fastapi import APIRouter, File, Form, Header, HTTPException, Query, UploadFile, status
from fastapi.responses import JSONResponse

from app.core.config import settings
from app.core.logging import logger
from app.models.schemas import (
    AnalyzeResponse,
    DocumentClassificationResult,
    DurationBreakdown,
    ErrorResponse,
    EvidenceSpan,
    ExtractedCandidateFact,
    HealthResponse,
    OCRResponse,
)
from app.ocr.engine import (
    OCREngine,
    TesseractLanguageUnavailableError,
    TesseractNotAvailableError,
)
from app.semantic.extractor import get_semantic_extractor
from app.semantic.ollama_client import (
    OllamaConnectionError,
    OllamaError,
    OllamaModelNotFoundError,
    OllamaTimeoutError,
)

router = APIRouter()
ocr_engine = OCREngine()


import io
import pymupdf
from PIL import Image


def detect_and_validate_media_type(content: bytes, declared_type: Optional[str]) -> str:
    """
    Validate file content magic bytes and minimal content decode to prevent spoofing or corrupted files.
    Returns normalized canonical MIME type or raises HTTPException.
    """
    if not content:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="Uploaded file is empty (0 bytes).",
        )

    # 1. PDF inspection: signature + PyMuPDF parse
    if content.startswith(b"%PDF"):
        try:
            doc = pymupdf.open(stream=content, filetype="pdf")
            page_count = doc.page_count
            doc.close()
            if page_count < 1:
                raise HTTPException(
                    status_code=status.HTTP_400_BAD_REQUEST,
                    detail="Invalid PDF document: document contains no pages.",
                )
            return "application/pdf"
        except HTTPException:
            raise
        except Exception as ex:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail=f"Invalid or corrupted PDF file: {str(ex)}",
            )

    # 2. PNG inspection: signature + PIL decode verification
    if content.startswith(b"\x89PNG\r\n\x1a\n"):
        try:
            with Image.open(io.BytesIO(content)) as img:
                img.verify()
            return "image/png"
        except Exception as ex:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail=f"Invalid or corrupted PNG image: {str(ex)}",
            )

    # 3. JPEG inspection: signature + PIL decode verification
    if content.startswith(b"\xff\xd8\xff") or (
        declared_type in ("image/jpeg", "image/jpg") and content[:2] == b"\xff\xd8"
    ):
        try:
            with Image.open(io.BytesIO(content)) as img:
                img.verify()
            return "image/jpeg"
        except Exception as ex:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail=f"Invalid or corrupted JPEG image: {str(ex)}",
            )

    # Neither valid PDF, PNG, nor JPEG signature
    raise HTTPException(
        status_code=status.HTTP_415_UNSUPPORTED_MEDIA_TYPE,
        detail=(
            f"Unsupported media type or invalid file signature. Only PDF, PNG, and JPEG documents are supported. "
            f"Declared Content-Type: '{declared_type}'."
        ),
    )


@router.get(
    "/health",
    response_model=HealthResponse,
    summary="Health check and OCR engine readiness diagnostic",
)
def get_health() -> HealthResponse:
    """Check service health and report Tesseract executable and language pack availability."""
    return ocr_engine.check_health()


@router.post(
    "/v1/ocr",
    response_model=OCRResponse,
    responses={
        400: {"model": ErrorResponse},
        413: {"model": ErrorResponse},
        415: {"model": ErrorResponse},
        422: {"model": ErrorResponse},
        503: {"model": ErrorResponse},
    },
    summary="Execute OCR extraction on an uploaded document (PDF/Image)",
)
async def process_ocr(
    file: UploadFile = File(..., description="Document file to process (PDF, PNG, JPEG)"),
    language_mode: str = Query(
        default=settings.ocr_default_language,
        description="OCR language configuration: 'eng', 'sin', or 'sin+eng'",
        pattern="^(eng|sin|sin\\+eng)$",
    ),
    preprocess: bool = Query(
        default=True,
        description="Whether to execute deterministic OpenCV contrast/denoise/deskew preprocessing.",
    ),
    x_request_id: Optional[str] = Header(
        default=None,
        alias="X-Request-ID",
        description="Optional caller request correlation ID.",
    ),
) -> OCRResponse:
    """
    Extract text across document pages with strict provenance preservation.
    Does NOT fabricate legal confidence or perform semantic classification.
    """
    request_id = x_request_id or f"req-{uuid.uuid4().hex[:12]}"

    if language_mode not in settings.supported_language_modes:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=f"Unsupported language mode '{language_mode}'. Must be one of: {sorted(settings.supported_language_modes)}",
        )

    # 1. Read and enforce upload size limits
    max_bytes = settings.max_upload_bytes
    file_bytes = await file.read(max_bytes + 1)
    if len(file_bytes) > max_bytes:
        logger.warning(f"[{request_id}] Rejected upload exceeding {settings.ocr_max_upload_mb} MB limit.")
        raise HTTPException(
            status_code=413,
            detail=f"File exceeds maximum allowed upload size of {settings.ocr_max_upload_mb} MB.",
        )

    # 2. Content signature and MIME validation
    media_type = detect_and_validate_media_type(file_bytes, file.content_type)

    # 3. Process document through OCR engine
    try:
        response = ocr_engine.process_document(
            file_bytes=file_bytes,
            media_type=media_type,
            language_mode=language_mode,
            preprocess=preprocess,
            request_id=request_id,
        )
        return response
    except TesseractNotAvailableError as ex:
        logger.error(f"[{request_id}] Tesseract engine unavailable: {ex}")
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="OCR engine is currently unavailable on this host.",
        )
    except TesseractLanguageUnavailableError as ex:
        logger.error(f"[{request_id}] Missing language pack: {ex}")
        raise HTTPException(
            status_code=422,
            detail=str(ex),
        )
    except ValueError as ex:
        logger.warning(f"[{request_id}] Invalid input: {ex}")
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=f"Invalid or corrupt document: {str(ex)}",
        )
    except Exception as ex:
        logger.error(f"[{request_id}] Unexpected OCR processing failure: {ex}", exc_info=True)
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="An unexpected internal error occurred during OCR text extraction.",
        )


@router.post(
    "/v1/analyze",
    response_model=AnalyzeResponse,
    responses={
        400: {"model": ErrorResponse},
        413: {"model": ErrorResponse},
        415: {"model": ErrorResponse},
        422: {"model": ErrorResponse},
        503: {"model": ErrorResponse},
    },
    summary="Execute end-to-end OCR and governed semantic extraction on an uploaded document",
)
async def process_analyze(
    file: UploadFile = File(..., description="Document file to process (PDF, PNG, JPEG)"),
    language_mode: str = Query(
        default=settings.ocr_default_language,
        description="OCR language configuration: 'eng', 'sin', or 'sin+eng'",
        pattern="^(eng|sin|sin\\+eng)$",
    ),
    preprocess: bool = Query(
        default=True,
        description="Whether to execute deterministic OpenCV contrast/denoise/deskew preprocessing.",
    ),
    category_hint: Optional[str] = Query(
        default=None,
        description="Optional intake document category hint (e.g. 'LeaseProposal', 'Deed').",
    ),
    x_request_id: Optional[str] = Header(
        default=None,
        alias="X-Request-ID",
        description="Optional caller request correlation ID.",
    ),
) -> AnalyzeResponse:
    """
    Executes composite pipeline: OCR text extraction followed by governed semantic extraction.
    Reuses the OCR pipeline without code duplication.
    Extracts machine candidate facts with strict evidence grounding.
    Candidate output is strictly non-authoritative.
    """
    import time
    start_total = time.perf_counter()
    request_id = x_request_id or f"req-{uuid.uuid4().hex[:12]}"

    if language_mode not in settings.supported_language_modes:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=f"Unsupported language mode '{language_mode}'. Must be one of: {sorted(settings.supported_language_modes)}",
        )

    # 1. Read and enforce upload size limits
    max_bytes = settings.max_upload_bytes
    file_bytes = await file.read(max_bytes + 1)
    if len(file_bytes) > max_bytes:
        logger.warning(f"[{request_id}] Rejected upload exceeding {settings.ocr_max_upload_mb} MB limit.")
        raise HTTPException(
            status_code=413,
            detail=f"File exceeds maximum allowed upload size of {settings.ocr_max_upload_mb} MB.",
        )

    # 2. Content signature and MIME validation
    media_type = detect_and_validate_media_type(file_bytes, file.content_type)

    # 3. Stage 1: OCR Text Extraction
    try:
        ocr_response = ocr_engine.process_document(
            file_bytes=file_bytes,
            media_type=media_type,
            language_mode=language_mode,
            preprocess=preprocess,
            request_id=request_id,
        )
    except TesseractNotAvailableError as ex:
        logger.error(f"[{request_id}] Tesseract engine unavailable: {ex}")
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="OCR engine is currently unavailable on this host.",
        )
    except TesseractLanguageUnavailableError as ex:
        logger.error(f"[{request_id}] Missing language pack: {ex}")
        raise HTTPException(
            status_code=422,
            detail=str(ex),
        )
    except ValueError as ex:
        logger.warning(f"[{request_id}] Invalid input: {ex}")
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=f"Invalid or corrupt document: {str(ex)}",
        )
    except Exception as ex:
        logger.error(f"[{request_id}] Unexpected OCR processing failure: {ex}", exc_info=True)
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="An unexpected internal error occurred during OCR text extraction.",
        )

    # 4. Stage 2: Governed Semantic Extraction
    semantic_start = time.perf_counter()
    pages_for_extraction = [
        {
            "page_number": p.page_number,
            "text": p.clean_text if p.clean_text else p.text,
        }
        for p in ocr_response.pages
    ]

    try:
        extractor = get_semantic_extractor()
        semantic_result = extractor.extract(
            pages=pages_for_extraction,
            category_hint=category_hint,
        )
    except OllamaConnectionError as ex:
        logger.error(f"[{request_id}] Ollama connection failed: {ex}")
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="Semantic extraction engine is currently unreachable. Ensure local Ollama daemon is running.",
        )
    except OllamaModelNotFoundError as ex:
        logger.error(f"[{request_id}] Configured Ollama model not found: {ex}")
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail=str(ex),
        )
    except OllamaTimeoutError as ex:
        logger.error(f"[{request_id}] Ollama extraction timed out: {ex}")
        raise HTTPException(
            status_code=status.HTTP_504_GATEWAY_TIMEOUT,
            detail="Semantic extraction timed out.",
        )
    except OllamaError as ex:
        logger.error(f"[{request_id}] Ollama extraction failure: {ex}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="An unexpected failure occurred during semantic extraction.",
        )
    except Exception as ex:
        logger.error(f"[{request_id}] Unexpected semantic extraction error: {ex}", exc_info=True)
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="An internal error occurred during document semantic extraction.",
        )

    semantic_duration = (time.perf_counter() - semantic_start) * 1000.0
    total_duration = (time.perf_counter() - start_total) * 1000.0

    # 5. Assemble mapped schema
    candidate_facts = [
        ExtractedCandidateFact(
            field_code=f.field_code,
            value_kind=f.value_kind,
            canonical_value=f.canonical_value,
            confidence=f.confidence,
            evidence=EvidenceSpan(
                page_number=f.page_number,
                quote=f.quote,
                start_offset=f.start_offset,
                end_offset=f.end_offset,
            ),
            is_evidence_verified=f.is_evidence_verified,
            validation_warning=f.validation_warning,
        )
        for f in semantic_result.facts
    ]

    return AnalyzeResponse(
        request_id=request_id,
        language_mode=language_mode,
        preprocessed=preprocess,
        page_count=ocr_response.page_count,
        pages=ocr_response.pages,
        classification=DocumentClassificationResult(
            code=semantic_result.classification.code,
            confidence=semantic_result.classification.confidence,
        ),
        facts=candidate_facts,
        model_identifier=semantic_result.model_identifier,
        prompt_version=semantic_result.prompt_version,
        duration_breakdown=DurationBreakdown(
            ocr_duration_ms=round(ocr_response.duration_ms, 2),
            semantic_duration_ms=round(semantic_duration, 2),
            total_duration_ms=round(total_duration, 2),
        ),
        warnings=semantic_result.warnings,
        governance_notice="Machine-extracted candidate information. Not human verified. Not authoritative.",
    )
