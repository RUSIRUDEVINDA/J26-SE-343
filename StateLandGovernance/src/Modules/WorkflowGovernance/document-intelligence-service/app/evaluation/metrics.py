"""OCR Evaluation Metrics: Character Error Rate (CER) and Word Error Rate (WER)."""

import re
import unicodedata
from typing import Any, Dict, List, Optional, Sequence


def levenshtein_distance(seq1: Sequence[Any], seq2: Sequence[Any]) -> int:
    """
    Compute Levenshtein distance between two sequences (characters or words)
    using dynamic programming with O(min(len1, len2)) space complexity.
    """
    if seq1 == seq2:
        return 0
    if not seq1:
        return len(seq2)
    if not seq2:
        return len(seq1)

    # Ensure seq2 is the shorter sequence for O(min(M, N)) space
    if len(seq1) < len(seq2):
        seq1, seq2 = seq2, seq1

    previous_row = list(range(len(seq2) + 1))

    for idx1, elem1 in enumerate(seq1):
        current_row = [idx1 + 1] + [0] * len(seq2)
        for idx2, elem2 in enumerate(seq2):
            insertions = previous_row[idx2 + 1] + 1
            deletions = current_row[idx2] + 1
            substitutions = previous_row[idx2] + (0 if elem1 == elem2 else 1)
            current_row[idx2 + 1] = min(insertions, deletions, substitutions)
        previous_row = current_row

    return previous_row[-1]


def normalize_text(text: str) -> str:
    """
    Deterministic, conservative text normalization for OCR evaluation.
    Preserves all Sinhala characters, combining diacritics, and English text.
    Operations:
    1. Unicode Normalization Form C (NFC) for canonical composition.
    2. Newline normalization (\\r\\n and \\r -> \\n).
    3. Whitespace collapsing (multiple horizontal spaces -> single space).
    4. Trimming leading and trailing whitespace.
    """
    if not text:
        return ""

    # Canonical decomposition followed by canonical composition
    normalized = unicodedata.normalize("NFC", text)

    # Normalize line breaks
    normalized = re.sub(r"\r\n|\r", "\n", normalized)

    # Collapse consecutive horizontal spaces/tabs
    normalized = re.sub(r"[ \t]+", " ", normalized)

    # Trim spaces adjacent to line breaks
    normalized = re.sub(r" ?\n ?", "\n", normalized)

    # Clean multiple blank lines
    normalized = re.sub(r"\n\s*\n+", "\n\n", normalized)

    return normalized.strip()


def calculate_cer(reference: str, hypothesis: str, normalize: bool = False) -> float:
    """
    Calculate Character Error Rate (CER):
    CER = LevenshteinDistance(ref, hyp) / len(ref)
    """
    ref = normalize_text(reference) if normalize else reference
    hyp = normalize_text(hypothesis) if normalize else hypothesis

    if not ref:
        return 0.0 if not hyp else 1.0

    edit_dist = levenshtein_distance(list(ref), list(hyp))
    return round(float(edit_dist) / float(len(ref)), 4)


def calculate_wer(reference: str, hypothesis: str, normalize: bool = False) -> float:
    """
    Calculate Word Error Rate (WER):
    WER = LevenshteinDistance(ref_words, hyp_words) / len(ref_words)
    """
    ref = normalize_text(reference) if normalize else reference
    hyp = normalize_text(hypothesis) if normalize else hypothesis

    ref_words = ref.split()
    hyp_words = hyp.split()

    if not ref_words:
        return 0.0 if not hyp_words else 1.0

    edit_dist = levenshtein_distance(ref_words, hyp_words)
    return round(float(edit_dist) / float(len(ref_words)), 4)


def evaluate_ocr_pair(reference: str, hypothesis: str) -> Dict[str, Any]:
    """
    Compute comprehensive CER and WER reporting both raw and normalized metrics.
    """
    return {
        "raw": {
            "cer": calculate_cer(reference, hypothesis, normalize=False),
            "wer": calculate_wer(reference, hypothesis, normalize=False),
        },
        "normalized": {
            "cer": calculate_cer(reference, hypothesis, normalize=True),
            "wer": calculate_wer(reference, hypothesis, normalize=True),
        },
        "counts": {
            "reference_characters": len(reference),
            "hypothesis_characters": len(hypothesis),
            "reference_words": len(reference.split()),
            "hypothesis_words": len(hypothesis.split()),
        },
    }


def main(argv: Optional[List[str]] = None) -> int:
    """CLI runner supporting both positional paths and explicit flags."""
    import argparse
    import json
    import sys

    parser = argparse.ArgumentParser(
        description="Evaluate OCR Character Error Rate (CER) and Word Error Rate (WER)."
    )
    # Positional arguments
    parser.add_argument("pos_ref_file", nargs="?", default=None, help="Reference ground truth file path")
    parser.add_argument("pos_hyp_file", nargs="?", default=None, help="Hypothesis OCR output file path")
    # File options
    parser.add_argument("--reference-file", "--ref-file", dest="ref_file", type=str, help="Path to ground truth text file (UTF-8)")
    parser.add_argument("--hypothesis-file", "--hyp-file", dest="hyp_file", type=str, help="Path to OCR hypothesis text file (UTF-8)")
    # Direct string options
    parser.add_argument("--reference-text", "--ref-text", dest="ref_text", type=str, help="Direct reference ground truth string")
    parser.add_argument("--hypothesis-text", "--hyp-text", dest="hyp_text", type=str, help="Direct hypothesis OCR text string")

    args = parser.parse_args(argv)

    ref = ""
    hyp = ""

    # Resolve reference source
    ref_file_path = args.ref_file or args.pos_ref_file
    if ref_file_path:
        with open(ref_file_path, "r", encoding="utf-8") as f:
            ref = f.read()
    elif args.ref_text is not None:
        ref = args.ref_text

    # Resolve hypothesis source
    hyp_file_path = args.hyp_file or args.pos_hyp_file
    if hyp_file_path:
        with open(hyp_file_path, "r", encoding="utf-8") as f:
            hyp = f.read()
    elif args.hyp_text is not None:
        hyp = args.hyp_text

    if not ref and not hyp:
        print("Usage: python -m app.evaluation.metrics [--reference-file PATH --hypothesis-file PATH | PATH_REF PATH_HYP | --reference-text STR --hypothesis-text STR]")
        return 1

    result = evaluate_ocr_pair(ref, hyp)
    print(json.dumps(result, indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    import sys
    sys.exit(main())
