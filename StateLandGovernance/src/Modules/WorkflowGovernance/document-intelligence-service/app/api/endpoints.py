"""FastAPI HTTP API route endpoints."""

import uuid
from typing import Optional
from fastapi import APIRouter, File, Form, Header, HTTPException, Query, UploadFile, status
from fastapi.responses import JSONResponse

from app.core.config import settings
from app.core.logging import logger
from app.models.schemas import ErrorResponse, HealthResponse, OCRResponse
from app.ocr.engine import (
    OCREngine,
    TesseractLanguageUnavailableError,
    TesseractNotAvailableError,
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
