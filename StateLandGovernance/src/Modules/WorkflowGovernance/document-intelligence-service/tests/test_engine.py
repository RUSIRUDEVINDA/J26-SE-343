"""Unit tests for OCREngine with mocked Tesseract backend."""

from unittest.mock import MagicMock, patch
import pytest
from PIL import Image
import pytesseract
from app.ocr.engine import (
    OCREngine,
    TesseractLanguageUnavailableError,
    TesseractNotAvailableError,
)


@pytest.fixture
def ocr_engine():
    return OCREngine()


def test_is_available_true(ocr_engine):
    with patch("pytesseract.get_tesseract_version", return_value="5.3.4"):
        assert ocr_engine.is_available() is True


def test_is_available_false(ocr_engine):
    with patch("pytesseract.get_tesseract_version", side_effect=Exception("Not found")):
        assert ocr_engine.is_available() is False


def test_check_health_healthy(ocr_engine):
    with patch("pytesseract.get_tesseract_version", return_value="5.3.4"), \
         patch("pytesseract.get_languages", return_value=["eng", "sin", "osd"]):
        health = ocr_engine.check_health()
        assert health.status == "healthy"
        assert health.tesseract_available is True
        assert health.tesseract_version == "5.3.4"
        assert "sin" in health.available_languages
        assert "eng" in health.available_languages


def test_check_health_degraded(ocr_engine):
    with patch("pytesseract.get_tesseract_version", side_effect=Exception("Executable not found")):
        health = ocr_engine.check_health()
        assert health.status == "degraded"
        assert health.tesseract_available is False
        assert health.available_languages == []


def test_validate_language_mode_invalid(ocr_engine):
    with pytest.raises(ValueError, match="Unsupported language mode"):
        ocr_engine.validate_language_availability("fra")


def test_validate_language_mode_missing_sinhala_pack_raises_error(ocr_engine):
    with patch.object(ocr_engine, "get_available_languages", return_value=["eng", "osd"]):
        with pytest.raises(TesseractLanguageUnavailableError, match="sin"):
            ocr_engine.validate_language_availability("sin")


def test_validate_language_mode_missing_pack_in_mixed_mode_raises_error(ocr_engine):
    with patch.object(ocr_engine, "get_available_languages", return_value=["eng", "osd"]):
        with pytest.raises(TesseractLanguageUnavailableError, match="sin"):
            ocr_engine.validate_language_availability("sin+eng")


def test_validate_language_mode_installed_succeeds(ocr_engine):
    with patch.object(ocr_engine, "get_available_languages", return_value=["eng", "sin", "osd"]):
        # All three supported modes must succeed
        ocr_engine.validate_language_availability("eng")
        ocr_engine.validate_language_availability("sin")
        ocr_engine.validate_language_availability("sin+eng")


def test_extract_page_ocr_success(ocr_engine):
    test_img = Image.new("RGB", (100, 100), color=(255, 255, 255))
    mock_data = {
        "text": ["State", "Land", "Lease"],
        "conf": [95, 90, 85],
    }

    with patch("pytesseract.image_to_string", return_value="State Land Lease\n"), \
         patch("pytesseract.image_to_data", return_value=mock_data):
        page_res = ocr_engine.extract_page_ocr(
            image=test_img,
            page_number=1,
            language_mode="eng",
            preprocess=False,
        )

        assert page_res.page_number == 1
        assert "State Land Lease" in page_res.text
        assert page_res.character_count > 0
        assert page_res.confidence == 90.0  # Mean of (95, 90, 85)


def test_process_document_png(ocr_engine, synthetic_english_image_bytes):
    with patch.object(ocr_engine, "validate_language_availability", return_value=None), \
         patch.object(ocr_engine, "extract_page_ocr") as mock_extract:
        from app.models.schemas import OCRPageResult

        mock_extract.return_value = OCRPageResult(
            page_number=1,
            raw_text="Synthetic text\r\n",
            clean_text="Synthetic text",
            text="Synthetic text",
            character_count=14,
            confidence=92.5,
        )

        response = ocr_engine.process_document(
            file_bytes=synthetic_english_image_bytes,
            media_type="image/png",
            language_mode="eng",
            preprocess=True,
            request_id="test-req-1",
        )

        assert response.request_id == "test-req-1"
        assert response.page_count == 1
        assert len(response.pages) == 1
        assert response.pages[0].page_number == 1
        assert response.pages[0].raw_text == "Synthetic text\r\n"
        assert response.pages[0].clean_text == "Synthetic text"
        assert response.total_character_count == 14
        assert response.preprocessed is True


def test_process_document_pdf(ocr_engine, synthetic_twopage_pdf_bytes):
    with patch.object(ocr_engine, "validate_language_availability", return_value=None), \
         patch.object(ocr_engine, "extract_page_ocr") as mock_extract:
        from app.models.schemas import OCRPageResult

        mock_extract.side_effect = [
            OCRPageResult(
                page_number=1,
                raw_text="Page 1 text\n",
                clean_text="Page 1 text",
                text="Page 1 text",
                character_count=11,
                confidence=90.0,
            ),
            OCRPageResult(
                page_number=2,
                raw_text="Page 2 text\n",
                clean_text="Page 2 text",
                text="Page 2 text",
                character_count=11,
                confidence=88.0,
            ),
        ]

        response = ocr_engine.process_document(
            file_bytes=synthetic_twopage_pdf_bytes,
            media_type="application/pdf",
            language_mode="sin+eng",
            preprocess=True,
            request_id="test-req-pdf",
        )

        assert response.page_count == 2
        assert len(response.pages) == 2
        assert response.pages[0].page_number == 1
        assert response.pages[1].page_number == 2
        assert response.total_character_count == 22


def test_process_document_corrupt_image(ocr_engine):
    with patch.object(ocr_engine, "validate_language_availability", return_value=None):
        with pytest.raises(ValueError, match="Corrupt or unreadable"):
            ocr_engine.process_document(
                file_bytes=b"CORRUPT_PNG_BYTES_HERE",
                media_type="image/png",
            )


def test_normalize_ocr_text_semantics():
    from app.ocr.engine import normalize_ocr_text

    raw = "  Line 1 with trailing spaces   \r\n\r\n\r\n\r\n  Line 2 with \t leading/trailing  \r\n\r\nLine 3  "
    clean = normalize_ocr_text(raw)
    assert "Line 1 with trailing spaces" in clean
    assert "Line 2 with \t leading/trailing" in clean
    assert "\r" not in clean  # Line endings converted to \n
    # Consecutive blank lines collapsed to max 2
    assert "\n\n\n\n" not in clean


def test_normalize_ocr_text_preserves_sinhala():
    from app.ocr.engine import normalize_ocr_text

    sinhala_raw = "  ශ්‍රී ලංකා ප්‍රජාතාන්ත්‍රික සමාජවාදී ජනරජය  \r\n"
    sinhala_clean = normalize_ocr_text(sinhala_raw)
    assert sinhala_clean == "ශ්‍රී ලංකා ප්‍රජාතාන්ත්‍රික සමාජවාදී ජනරජය"
    # No characters were dropped or altered
    assert "ශ්‍රී" in sinhala_clean

