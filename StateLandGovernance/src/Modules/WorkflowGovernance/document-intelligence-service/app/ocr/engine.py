"""Tesseract OCR Engine wrapper and document orchestration."""

import io
import shutil
import time
from typing import List, Optional, Set, Tuple
import numpy as np
from PIL import Image
import pytesseract
from pytesseract import Output

from app.core.config import settings
from app.core.logging import logger
from app.models.schemas import HealthResponse, OCRPageResult, OCRResponse
from app.ocr.pdf_processor import PDFProcessor
from app.ocr.preprocessor import ImagePreprocessor


class TesseractNotAvailableError(Exception):
    """Raised when Tesseract binary executable cannot be found or executed."""
    pass


class TesseractLanguageUnavailableError(Exception):
    """Raised when a requested OCR language pack is not installed in Tesseract."""
    pass


import os
import unicodedata
from typing import Union

Union_Image_Or_Array = Union[Image.Image, np.ndarray]


def normalize_ocr_text(text: str) -> str:
    """
    Apply non-semantic text normalization to raw OCR output:
    1. Line-ending normalization (CRLF / CR -> LF).
    2. NFC Unicode normalization (preserves Sinhala base glyphs and vowel signs).
    3. Per-line whitespace trimming (removes leading/trailing whitespace per line).
    4. Blank line deduplication (limits consecutive blank lines to at most 2).

    CRITICAL RULES:
    - NO spelling correction
    - NO translation
    - NO semantic rewriting
    - NO Sinhala word replacement
    - NO LLM cleanup
    """
    if not text:
        return ""
    # Normalize line endings
    norm = text.replace("\r\n", "\n").replace("\r", "\n")
    # NFC Unicode normalization
    norm = unicodedata.normalize("NFC", norm)
    # Strip whitespace per line
    lines = [line.strip() for line in norm.split("\n")]
    # Limit consecutive empty lines
    cleaned_lines = []
    consecutive_blank = 0
    for line in lines:
        if not line:
            consecutive_blank += 1
            if consecutive_blank <= 2:
                cleaned_lines.append(line)
        else:
            consecutive_blank = 0
            cleaned_lines.append(line)
    return "\n".join(cleaned_lines).strip()


class OCREngine:
    """Orchestrates document rasterization, preprocessing, and Tesseract text extraction."""

    def __init__(self, tesseract_cmd: Optional[str] = None):
        cmd = tesseract_cmd or settings.tesseract_cmd
        if cmd:
            pytesseract.pytesseract.tesseract_cmd = cmd
        else:
            detected = shutil.which("tesseract")
            if not detected:
                # Portable checks for standard Windows and Linux locations
                local_app_data = os.environ.get("LOCALAPPDATA", "")
                candidates = [
                    os.path.join(local_app_data, "Programs", "Tesseract-OCR", "tesseract.exe") if local_app_data else None,
                    r"C:\Program Files\Tesseract-OCR\tesseract.exe",
                    r"C:\Program Files (x86)\Tesseract-OCR\tesseract.exe",
                    "/usr/bin/tesseract",
                    "/usr/local/bin/tesseract",
                ]
                for candidate in candidates:
                    if candidate and os.path.exists(candidate):
                        detected = candidate
                        break
            if detected:
                pytesseract.pytesseract.tesseract_cmd = detected

    def is_available(self) -> bool:
        """Verify whether Tesseract executable is present and callable."""
        try:
            pytesseract.get_tesseract_version()
            return True
        except Exception:
            return False

    def get_available_languages(self) -> List[str]:
        """Query Tesseract for installed trained language data packs."""
        try:
            return pytesseract.get_languages()
        except Exception as ex:
            logger.warning(f"Unable to retrieve Tesseract language packs: {ex}")
            return []

    def check_health(self) -> HealthResponse:
        """Produce system health diagnostics for Tesseract availability and language packs."""
        try:
            version = str(pytesseract.get_tesseract_version())
            languages = pytesseract.get_languages()
            return HealthResponse(
                status="healthy",
                tesseract_available=True,
                tesseract_version=version,
                available_languages=languages,
            )
        except Exception as ex:
            logger.error(f"Health check failed: Tesseract engine unavailable ({ex})")
            return HealthResponse(
                status="degraded",
                tesseract_available=False,
                tesseract_version=None,
                available_languages=[],
            )

    def validate_language_availability(self, language_mode: str) -> None:
        """
        Verify that requested language mode is supported and its language packs exist.
        CRITICAL: Never silently fall back to English if Sinhala ('sin') is unavailable.
        """
        if language_mode not in settings.supported_language_modes:
            raise ValueError(
                f"Unsupported language mode '{language_mode}'. "
                f"Valid modes are: {sorted(settings.supported_language_modes)}"
            )

        installed_langs: Set[str] = set(self.get_available_languages())

        # If language query failed (e.g. mock or restricted permissions), skip pack check
        if not installed_langs:
            return

        needed_langs = language_mode.split("+")
        missing = [l for l in needed_langs if l not in installed_langs]
        if missing:
            raise TesseractLanguageUnavailableError(
                f"Requested OCR language pack(s) {missing} are not installed in Tesseract. "
                f"Installed language packs: {sorted(installed_langs)}. "
                f"Automatic fallback is disabled to prevent silent transcription corruption."
            )

    def extract_page_ocr(
        self,
        image: Union_Image_Or_Array,
        page_number: int,
        language_mode: str,
        preprocess: bool = True,
    ) -> OCRPageResult:
        """
        Run OCR extraction on a single page image with optional preprocessing.
        Computes text, character count, and token-level engine confidence.
        """
        processed_input = (
            ImagePreprocessor.preprocess(image) if preprocess else image
        )

        try:
            # PSM 3: Fully automatic page segmentation with OSD
            text = pytesseract.image_to_string(
                processed_input,
                lang=language_mode,
                config="--psm 3",
            )
        except pytesseract.TesseractNotFoundError as ex:
            raise TesseractNotAvailableError("Tesseract OCR executable not found on system.") from ex
        except Exception as ex:
            raise RuntimeError(f"Tesseract OCR execution failed on page {page_number}: {str(ex)}") from ex

        # Extract word-level confidence metrics
        confidence: Optional[float] = None
        try:
            data = pytesseract.image_to_data(
                processed_input,
                lang=language_mode,
                config="--psm 3",
                output_type=Output.DICT,
            )
            confidences = [
                float(c)
                for c, w in zip(data.get("conf", []), data.get("text", []))
                if str(c).strip() not in ("-1", "") and str(w).strip()
            ]
            if confidences:
                confidence = round(float(np.mean(confidences)), 2)
        except Exception as ex:
            logger.debug(f"Could not compute word confidence for page {page_number}: {ex}")

        raw_text = text
        clean_text = normalize_ocr_text(raw_text)

        return OCRPageResult(
            page_number=page_number,
            raw_text=raw_text,
            clean_text=clean_text,
            text=clean_text,
            character_count=len(clean_text),
            confidence=confidence,
        )

    def process_document(
        self,
        file_bytes: bytes,
        media_type: str,
        language_mode: str = "sin+eng",
        preprocess: bool = True,
        request_id: str = "req-default",
    ) -> OCRResponse:
        """Process document bytes (PDF or Image) and produce page-level OCR response."""
        start_time = time.perf_counter()

        self.validate_language_availability(language_mode)

        page_results: List[OCRPageResult] = []

        if media_type == "application/pdf":
            pages: List[Tuple[int, Image.Image]] = PDFProcessor.render_pages(
                file_bytes, dpi=settings.ocr_pdf_dpi
            )
            for page_num, page_img in pages:
                page_res = self.extract_page_ocr(
                    image=page_img,
                    page_number=page_num,
                    language_mode=language_mode,
                    preprocess=preprocess,
                )
                page_results.append(page_res)
        elif media_type in ("image/png", "image/jpeg", "image/jpg"):
            try:
                img = Image.open(io.BytesIO(file_bytes))
                img.load()
            except Exception as ex:
                raise ValueError(f"Corrupt or unreadable image file: {str(ex)}") from ex

            page_res = self.extract_page_ocr(
                image=img,
                page_number=1,
                language_mode=language_mode,
                preprocess=preprocess,
            )
            page_results.append(page_res)
        else:
            raise ValueError(f"Unsupported media type: '{media_type}'")

        duration_ms = round((time.perf_counter() - start_time) * 1000, 2)
        total_chars = sum(p.character_count for p in page_results)

        logger.info(
            f"[{request_id}] OCR completed: pages={len(page_results)}, "
            f"chars={total_chars}, lang={language_mode}, prep={preprocess}, "
            f"duration={duration_ms}ms"
        )

        return OCRResponse(
            request_id=request_id,
            language_mode=language_mode,
            preprocessed=preprocess,
            page_count=len(page_results),
            pages=page_results,
            total_character_count=total_chars,
            duration_ms=duration_ms,
        )
