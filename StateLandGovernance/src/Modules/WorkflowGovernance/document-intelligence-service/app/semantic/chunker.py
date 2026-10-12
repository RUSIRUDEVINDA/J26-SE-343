"""Deterministic page chunking and context window grouping for long documents."""

from dataclasses import dataclass
from typing import Dict, List


@dataclass(frozen=True)
class DocumentChunk:
    """Group of consecutive pages within the context budget."""

    chunk_index: int
    page_numbers: List[int]
    pages: List[Dict[str, any]]
    total_characters: int


def chunk_document_pages(
    pages: List[Dict[str, any]],
    max_characters_per_chunk: int = 12000,
) -> List[DocumentChunk]:
    """
    Deterministically group document pages into sequential chunks that respect
    the model context budget while preserving exact page boundaries and text.
    
    Args:
        pages: List of dicts, each containing 'page_number' and 'text'.
        max_characters_per_chunk: Character threshold per chunk (approx ~3000-4000 tokens).
        
    Returns:
        List of DocumentChunk instances.
    """
    if not pages:
        return []

    sorted_pages = sorted(pages, key=lambda p: p.get("page_number", 0))
    chunks: List[DocumentChunk] = []

    current_pages: List[Dict[str, any]] = []
    current_char_count = 0
    chunk_idx = 0

    for page in sorted_pages:
        page_text = page.get("text", "")
        page_len = len(page_text)

        # If adding this page exceeds the chunk size and we already have pages in this chunk,
        # close current chunk and start a new one.
        if current_pages and (current_char_count + page_len > max_characters_per_chunk):
            chunks.append(
                DocumentChunk(
                    chunk_index=chunk_idx,
                    page_numbers=[p["page_number"] for p in current_pages],
                    pages=list(current_pages),
                    total_characters=current_char_count,
                )
            )
            chunk_idx += 1
            current_pages = []
            current_char_count = 0

        current_pages.append(page)
        current_char_count += page_len

    if current_pages:
        chunks.append(
            DocumentChunk(
                chunk_index=chunk_idx,
                page_numbers=[p["page_number"] for p in current_pages],
                pages=list(current_pages),
                total_characters=current_char_count,
            )
        )

    return chunks

