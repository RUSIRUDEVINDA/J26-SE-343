"""Deterministic evidence validation and grounding against source OCR text."""

import re
import unicodedata
from dataclasses import dataclass
from typing import Dict, List, Optional, Tuple


@dataclass(frozen=True)
class GroundedEvidence:
    """Validated evidence span grounded in source OCR text."""

    page_number: int
    quote: str
    start_offset: Optional[int]
    end_offset: Optional[int]
    is_verified: bool
    warning: Optional[str] = None


def normalize_whitespace_for_matching(text: str) -> str:
    """Normalize whitespace and Unicode NFC for resilient quote matching."""
    if not text:
        return ""
    normalized = unicodedata.normalize("NFC", text)
    return " ".join(normalized.split())


def _build_zero_width_resilient_pattern(text: str) -> Optional[str]:
    """
    Builds a regex pattern allowing optional zero-width formatting codepoints
    (ZWNJ \\u200c, ZWSP \\u200b, BOM \\ufeff) between characters and whitespace.
    Crucially preserves meaningful Sinhala characters such as ZWJ (\\u200d)
    which forms conjunct consonants (e.g. repaya, yansaya, rakaransaya).
    """
    if not text:
        return None
    tokens = text.split()
    token_pats = []
    for t in tokens:
        # Strip only invisible formatting codepoints, NEVER strip \\u200d (meaningful Sinhala ZWJ)
        cleaned = re.sub(r"[\u200b\u200c\ufeff]", "", t)
        if cleaned:
            token_pats.append(r"[\u200b\u200c\ufeff]*".join(re.escape(c) for c in cleaned))
    if not token_pats:
        return None
    return r"[\s\u200b\u200c\ufeff]+".join(token_pats)


def find_quote_offsets(
    page_text: str,
    quote: str,
) -> Tuple[Optional[int], Optional[int], bool]:
    """
    Find exact start and end character offsets of a quote within the page text.
    First attempts exact literal match, then normalized whitespace match.
    
    Returns:
        (start_offset, end_offset, is_match)
    """
    if not page_text or not quote:
        return None, None, False

    norm_page = unicodedata.normalize("NFC", page_text)
    norm_quote = unicodedata.normalize("NFC", quote.strip())

    if not norm_quote:
        return None, None, False

    # 1. Exact substring match
    idx = norm_page.find(norm_quote)
    if idx != -1:
        return idx, idx + len(norm_quote), True

    # 2. Case-insensitive substring match
    idx_lower = norm_page.lower().find(norm_quote.lower())
    if idx_lower != -1:
        return idx_lower, idx_lower + len(norm_quote), True

    # 3. Flexible whitespace regex match
    escaped_tokens = [re.escape(t) for t in norm_quote.split()]
    if escaped_tokens:
        pattern = r"\s+".join(escaped_tokens)
        match = re.search(pattern, norm_page, flags=re.IGNORECASE)
        if match:
            return match.start(), match.end(), True

    # 4. Zero-width character resilient matching directly in norm_page
    # Ensures offsets match original text exactly without index shift,
    # and strictly preserves meaningful Sinhala characters like ZWJ (\u200d)
    zw_pattern = _build_zero_width_resilient_pattern(norm_quote)
    if zw_pattern:
        zw_match = re.search(zw_pattern, norm_page, flags=re.IGNORECASE)
        if zw_match:
            return zw_match.start(), zw_match.end(), True

    # 5. Quote-trimmed matching (strips surrounding quotation marks or trailing punctuation from cited quote)
    stripped_q = re.sub(r"^[\"\'“‘\(\[\s]+|[\"\'”’\)\]\.\,\:\;\s]+$", "", norm_quote)
    if stripped_q and stripped_q != norm_quote:
        idx_sq = norm_page.find(stripped_q)
        if idx_sq != -1:
            return idx_sq, idx_sq + len(stripped_q), True
        sq_tokens = [re.escape(t) for t in stripped_q.split()]
        if sq_tokens:
            sq_pattern = r"\s+".join(sq_tokens)
            sq_match = re.search(sq_pattern, norm_page, flags=re.IGNORECASE)
            if sq_match:
                return sq_match.start(), sq_match.end(), True
        zw_sq_pattern = _build_zero_width_resilient_pattern(stripped_q)
        if zw_sq_pattern:
            zw_sq_match = re.search(zw_sq_pattern, norm_page, flags=re.IGNORECASE)
            if zw_sq_match:
                return zw_sq_match.start(), zw_sq_match.end(), True

    return None, None, False


def ground_evidence(
    page_texts_by_number: Dict[int, str],
    page_number: int,
    quote: Optional[str],
) -> GroundedEvidence:
    """
    Validate whether a model-produced evidence quote actually exists on the cited OCR page.
    Prevents fabricated model evidence from polluting the governance record.
    """
    if page_number not in page_texts_by_number:
        return GroundedEvidence(
            page_number=page_number,
            quote=quote or "",
            start_offset=None,
            end_offset=None,
            is_verified=False,
            warning=f"Page {page_number} does not exist in the document (document has pages {sorted(page_texts_by_number.keys())}).",
        )

    if not quote or not quote.strip():
        return GroundedEvidence(
            page_number=page_number,
            quote="",
            start_offset=None,
            end_offset=None,
            is_verified=False,
            warning=f"Fact on page {page_number} was produced without any supporting quote.",
        )

    clean_page_text = page_texts_by_number[page_number]
    start, end, matched = find_quote_offsets(clean_page_text, quote)

    if matched:
        return GroundedEvidence(
            page_number=page_number,
            quote=quote.strip(),
            start_offset=start,
            end_offset=end,
            is_verified=True,
            warning=None,
        )

    # Check if quote exists on a DIFFERENT page
    other_pages = []
    for p_num, text in page_texts_by_number.items():
        if p_num != page_number:
            _, _, other_matched = find_quote_offsets(text, quote)
            if other_matched:
                other_pages.append(p_num)

    if other_pages:
        warning = (
            f"Quote was not found on cited page {page_number}, but was found on page(s) {other_pages}. "
            "Evidence provenance citation is inaccurate."
        )
    else:
        warning = (
            f"Quote '{quote[:60]}...' was NOT found in the OCR text of page {page_number}. "
            "Evidence citation could not be grounded in source text."
        )

    return GroundedEvidence(
        page_number=page_number,
        quote=quote.strip(),
        start_offset=None,
        end_offset=None,
        is_verified=False,
        warning=warning,
    )

