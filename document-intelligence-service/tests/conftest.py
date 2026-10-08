"""Pytest fixtures and programmatic synthetic document generators."""

import io
import pytest
import pymupdf
from PIL import Image, ImageDraw


@pytest.fixture
def synthetic_english_image_bytes() -> bytes:
    """Generate a clean synthetic English document image in memory."""
    img = Image.new("RGB", (600, 200), color=(255, 255, 255))
    draw = ImageDraw.Draw(img)
    draw.text((30, 80), "STATE LAND LEASE APPLICATION APP-2026-001", fill=(0, 0, 0))
    buffer = io.BytesIO()
    img.save(buffer, format="PNG")
    return buffer.getvalue()


@pytest.fixture
def synthetic_sinhala_image_bytes() -> bytes:
    """Generate a clean synthetic image for Sinhala OCR pipeline testing."""
    img = Image.new("RGB", (600, 200), color=(255, 255, 255))
    draw = ImageDraw.Draw(img)
    # Simple high-contrast text rendering
    draw.text((30, 80), "SL-LAND-GOV-SIN", fill=(0, 0, 0))
    buffer = io.BytesIO()
    img.save(buffer, format="PNG")
    return buffer.getvalue()


@pytest.fixture
def synthetic_twopage_pdf_bytes() -> bytes:
    """Generate a two-page PDF in memory using PyMuPDF."""
    doc = pymupdf.open()
    
    # Page 1
    page1 = doc.new_page(width=595, height=842)
    page1.insert_text((50, 100), "State Land Lease Governance - Page 1", fontsize=14)
    page1.insert_text((50, 150), "Application Reference: APP-2026-9901", fontsize=12)
    
    # Page 2
    page2 = doc.new_page(width=595, height=842)
    page2.insert_text((50, 100), "State Land Lease Governance - Page 2", fontsize=14)
    page2.insert_text((50, 150), "Survey Plan Coordinates and Extent", fontsize=12)
    
    pdf_bytes = doc.tobytes()
    doc.close()
    return pdf_bytes
