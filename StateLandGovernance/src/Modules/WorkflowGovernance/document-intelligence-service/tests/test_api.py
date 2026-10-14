"""Unit and integration tests for FastAPI HTTP endpoints."""

from unittest.mock import patch
import pytest
from fastapi.testclient import TestClient
from app.core.config import settings
from app.main import app
from app.models.schemas import HealthResponse, OCRPageResult, OCRResponse
from app.ocr.engine import (
    TesseractLanguageUnavailableError,
    TesseractNotAvailableError,
)

client = TestClient(app)


def test_health_endpoint_healthy():
    mock_health = HealthResponse(
        status="healthy",
        tesseract_available=True,
        tesseract_version="5.3.4",
        available_languages=["eng", "sin", "osd"],
    )
    with patch("app.api.endpoints.ocr_engine.check_health", return_value=mock_health):
        response = client.get("/health")
        assert response.status_code == 200
        data = response.json()
        assert data["status"] == "healthy"
        assert data["tesseract_available"] is True
        assert data["tesseract_version"] == "5.3.4"
        assert "sin" in data["available_languages"]


def test_health_endpoint_degraded():
    mock_health = HealthResponse(
        status="degraded",
        tesseract_available=False,
        tesseract_version=None,
        available_languages=[],
    )
    with patch("app.api.endpoints.ocr_engine.check_health", return_value=mock_health):
        response = client.get("/health")
        assert response.status_code == 200
        data = response.json()
        assert data["status"] == "degraded"
        assert data["tesseract_available"] is False


def test_ocr_unsupported_media_type():
    files = {"file": ("document.txt", b"plain text is not supported", "text/plain")}
    response = client.post("/v1/ocr", files=files)
    assert response.status_code == 415
    assert "Unsupported media type" in response.json()["detail"]


def test_ocr_empty_upload():
    files = {"file": ("empty.png", b"", "image/png")}
    response = client.post("/v1/ocr", files=files)
    assert response.status_code == 400
    assert "empty" in response.json()["detail"].lower()


def test_ocr_invalid_language_mode(synthetic_english_image_bytes):
    files = {"file": ("test.png", synthetic_english_image_bytes, "image/png")}
    response = client.post("/v1/ocr?language_mode=invalid_lang", files=files)
    assert response.status_code in (400, 422)


def test_ocr_file_too_large(synthetic_english_image_bytes):
    # Temporarily set upload limit to 1 KB to test 413 rejection
    with patch.object(settings, "ocr_max_upload_mb", 0):
        # 0 MB -> max_upload_bytes = 0
        files = {"file": ("large.png", synthetic_english_image_bytes, "image/png")}
        response = client.post("/v1/ocr", files=files)
        assert response.status_code == 413
        assert "exceeds maximum allowed upload size" in response.json()["detail"]


def test_ocr_success_with_correlation_id(synthetic_english_image_bytes):
    mock_ocr_response = OCRResponse(
        request_id="custom-corr-999",
        language_mode="eng",
        preprocessed=True,
        page_count=1,
        pages=[
            OCRPageResult(
                page_number=1,
                raw_text="STATE LAND LEASE APPLICATION\r\n",
                clean_text="STATE LAND LEASE APPLICATION",
                text="STATE LAND LEASE APPLICATION",
                character_count=28,
                confidence=95.0,
            )
        ],
        total_character_count=28,
        duration_ms=45.2,
    )

    with patch("app.api.endpoints.ocr_engine.process_document", return_value=mock_ocr_response):
        files = {"file": ("test.png", synthetic_english_image_bytes, "image/png")}
        headers = {"X-Request-ID": "custom-corr-999"}
        response = client.post("/v1/ocr?language_mode=eng", files=files, headers=headers)

        assert response.status_code == 200
        data = response.json()
        assert data["request_id"] == "custom-corr-999"
        assert data["language_mode"] == "eng"
        assert data["page_count"] == 1
        assert len(data["pages"]) == 1
        assert data["pages"][0]["page_number"] == 1
        assert data["pages"][0]["raw_text"] == "STATE LAND LEASE APPLICATION\r\n"
        assert data["pages"][0]["clean_text"] == "STATE LAND LEASE APPLICATION"
        assert "STATE LAND LEASE" in data["pages"][0]["text"]
        assert data["pages"][0]["confidence"] == 95.0


def test_ocr_tesseract_unavailable_returns_503(synthetic_english_image_bytes):
    with patch("app.api.endpoints.ocr_engine.process_document", side_effect=TesseractNotAvailableError("Missing")):
        files = {"file": ("test.png", synthetic_english_image_bytes, "image/png")}
        response = client.post("/v1/ocr", files=files)
        assert response.status_code == 503
        assert "unavailable" in response.json()["detail"].lower()


def test_ocr_missing_language_pack_returns_422(synthetic_english_image_bytes):
    with patch("app.api.endpoints.ocr_engine.process_document", side_effect=TesseractLanguageUnavailableError("Language 'sin' not installed")):
        files = {"file": ("test.png", synthetic_english_image_bytes, "image/png")}
        response = client.post("/v1/ocr?language_mode=sin", files=files)
        assert response.status_code == 422
        assert "sin" in response.json()["detail"]


def test_ocr_spoofed_pdf_rejected():
    files = {"file": ("fake.pdf", b"random text not a pdf", "application/pdf")}
    response = client.post("/v1/ocr", files=files)
    assert response.status_code == 415


def test_ocr_corrupted_pdf_rejected():
    files = {"file": ("corrupt.pdf", b"%PDF-1.4 but broken and incomplete", "application/pdf")}
    response = client.post("/v1/ocr", files=files)
    assert response.status_code == 400


def test_ocr_spoofed_png_rejected():
    files = {"file": ("fake.png", b"random text not a png", "image/png")}
    response = client.post("/v1/ocr", files=files)
    assert response.status_code == 415


def test_ocr_corrupted_png_rejected():
    files = {"file": ("corrupt.png", b"\x89PNG\r\n\x1a\ncorruptpayload", "image/png")}
    response = client.post("/v1/ocr", files=files)
    assert response.status_code == 400


def test_ocr_spoofed_jpeg_rejected():
    files = {"file": ("fake.jpg", b"random text not a jpeg", "image/jpeg")}
    response = client.post("/v1/ocr", files=files)
    assert response.status_code == 415

