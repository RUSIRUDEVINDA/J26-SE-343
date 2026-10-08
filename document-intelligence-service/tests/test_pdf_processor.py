"""Unit tests for PDF rasterization and page extraction."""

import pytest
from PIL import Image
from app.ocr.pdf_processor import PDFProcessor


def test_render_pages_valid_pdf(synthetic_twopage_pdf_bytes):
    pages = PDFProcessor.render_pages(synthetic_twopage_pdf_bytes, dpi=150)
    assert len(pages) == 2
    
    # Verify strict page numbering and order
    page_1_num, page_1_img = pages[0]
    page_2_num, page_2_img = pages[1]
    
    assert page_1_num == 1
    assert page_2_num == 2
    assert isinstance(page_1_img, Image.Image)
    assert isinstance(page_2_img, Image.Image)
    assert page_1_img.width > 0 and page_1_img.height > 0


def test_render_pages_empty_bytes():
    with pytest.raises(ValueError, match="empty"):
        PDFProcessor.render_pages(b"")


def test_render_pages_corrupt_bytes():
    with pytest.raises(ValueError, match="Failed to parse PDF"):
        PDFProcessor.render_pages(b"NOT_A_VALID_PDF_HEADER_DATA")
