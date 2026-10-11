"""PDF page extraction and rasterization using PyMuPDF."""

from typing import List, Tuple
import pymupdf
from PIL import Image
from app.core.config import settings


class PDFProcessor:
    """Rasterizes multi-page PDF documents to sequential page images."""

    @staticmethod
    def render_pages(pdf_bytes: bytes, dpi: int = 300) -> List[Tuple[int, Image.Image]]:
        """
        Render all pages of a PDF into sequential PIL Images.
        Returns a list of tuples: (page_number_1_based, PIL.Image).
        Preserves strict page ordering and provenance.
        """
        if not pdf_bytes:
            raise ValueError("PDF content is empty.")

        try:
            doc = pymupdf.open(stream=pdf_bytes, filetype="pdf")
        except Exception as ex:
            raise ValueError(f"Failed to parse PDF document: {str(ex)}") from ex

        if len(doc) == 0:
            doc.close()
            raise ValueError("PDF document contains no pages.")

        page_images: List[Tuple[int, Image.Image]] = []

        try:
            for page_idx in range(len(doc)):
                page = doc.load_page(page_idx)
                # Rasterize with specified DPI (300 DPI is recommended standard for OCR)
                pixmap = page.get_pixmap(dpi=dpi)
                
                # Convert PyMuPDF pixmap to PIL Image
                img = Image.frombytes(
                    "RGB",
                    [pixmap.width, pixmap.height],
                    pixmap.samples,
                )
                page_number = page_idx + 1
                page_images.append((page_number, img))
        except Exception as ex:
            raise ValueError(f"Failed to render PDF page: {str(ex)}") from ex
        finally:
            doc.close()

        return page_images
