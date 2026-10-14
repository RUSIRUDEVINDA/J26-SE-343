#!/usr/bin/env python3
"""
OCR Evaluation Harness for Proposal Extraction (Development Benchmark).

Evaluates the existing OpenCV/Tesseract OCR pipeline independently from Qwen/LLM
by measuring Character Error Rate (CER) and Word Error Rate (WER) against
manually transcribed reference ground truth.

Strict research invariants:
1. Ground truth MUST NOT be generated from OCR output.
2. If ground-truth files are empty or contain placeholders, evaluation is refused
   to prevent fabricated accuracy numbers.
3. CER and WER are computed on two normalization views:
   - Strict: Unicode NFC, line endings, leading/trailing spaces.
   - Whitespace-normalized: Tokens collapsed with single spacing to isolate layout errors.
4. Micro-aggregates sum edit operations and reference denominators across all valid pages.
"""

import argparse
import os
import re
import sys
import unicodedata
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple, Union

# Ensure UTF-8 output on Windows consoles
if sys.stdout.encoding and sys.stdout.encoding.lower() != "utf-8":
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass

# Add service root to sys.path so app modules can be imported
SERVICE_ROOT = Path(__file__).resolve().parent.parent.parent
if str(SERVICE_ROOT) not in sys.path:
    sys.path.insert(0, str(SERVICE_ROOT))

DEFAULT_PDF_PATH = Path(r"C:\Users\Ovindi\Downloads\Proposal1.pdf")
DEFAULT_PAGES = [3, 5, 14]
DEFAULT_LANG_MODE = "sin"

SCRIPT_DIR = Path(__file__).resolve().parent
GROUND_TRUTH_DIR = SCRIPT_DIR / "ground_truth"
OUTPUTS_DIR = SCRIPT_DIR / "outputs"


# ============================================================================
# 1. TEXT NORMALIZATION VIEWS
# ============================================================================

def normalize_strict(text: str) -> str:
    """
    Conservative Strict Normalization View:
    - NFC Unicode normalization (preserves base glyphs and combining signs).
    - Line-ending normalization (CRLF / CR -> LF).
    - Trims leading/trailing whitespace per line.
    - Collapses consecutive spaces/tabs on a single line to a single space.
    - Trims leading/trailing blank lines of the entire text.
    
    CRITICAL: Does NOT translate, transliterate, correct spelling, or delete Sinhala glyphs.
    """
    if not text:
        return ""
    # 1. Unicode NFC
    norm = unicodedata.normalize("NFC", text)
    # 2. Line endings
    norm = norm.replace("\r\n", "\n").replace("\r", "\n")
    # 3. Clean line spacing
    lines = [re.sub(r"[ \t]+", " ", line).strip() for line in norm.split("\n")]
    # 4. Remove empty lines at boundaries
    result = "\n".join(lines).strip()
    return result


def normalize_whitespace(text: str) -> str:
    """
    Whitespace-Normalized View:
    - NFC Unicode normalization.
    - Normalizes all whitespace (spaces, tabs, newlines) into single spaces.
    - Strips leading and trailing whitespace.
    
    Helps isolate character misrecognitions from layout/table ordering differences.
    """
    if not text:
        return ""
    norm = unicodedata.normalize("NFC", text)
    return re.sub(r"\s+", " ", norm).strip()


# ============================================================================
# 2. GROUND TRUTH INTEGRITY VALIDATION
# ============================================================================

def validate_ground_truth(file_path: Path) -> Tuple[bool, str, str]:
    """
    Validates whether a ground-truth file contains genuine manually transcribed text.
    Returns: (is_valid, reason, raw_content)
    """
    if not file_path.exists():
        return False, f"Ground-truth file does not exist: {file_path.name}", ""
    
    raw_content = file_path.read_text(encoding="utf-8").strip()
    if not raw_content:
        return False, f"Ground-truth file is empty: {file_path.name}", ""
    
    # Check for placeholder markers
    placeholder_patterns = [
        r"\[PLACEHOLDER.*\]",
        r"^TODO\b",
        r"^UNTRANSCRIBED\b",
        r"Manually transcribe Sinhala reference text",
    ]
    for pattern in placeholder_patterns:
        if re.search(pattern, raw_content, re.IGNORECASE):
            return False, f"File contains placeholder marker ({pattern}): {file_path.name}", raw_content
    
    return True, "Valid human reference text present", raw_content


# ============================================================================
# 3. LEVENSHTEIN ALIGNMENT, CER & WER
# ============================================================================

@dataclass
class EditDistanceResult:
    ref_count: int
    hyp_count: int
    substitutions: int
    deletions: int
    insertions: int
    total_edits: int
    error_rate: float
    alignment_ops: List[Tuple[str, Optional[Any], Optional[Any]]]  # ('match'|'sub'|'del'|'ins', ref_tok, hyp_tok)


def compute_levenshtein(
    ref_tokens: List[Union[str, Any]],
    hyp_tokens: List[Union[str, Any]],
) -> EditDistanceResult:
    """
    Standard dynamic programming Wagner-Fischer algorithm with full backtracking.
    Computes exact counts of Substitutions, Deletions, and Insertions.
    """
    m = len(ref_tokens)
    n = len(hyp_tokens)

    if m == 0 and n == 0:
        return EditDistanceResult(0, 0, 0, 0, 0, 0, 0.0, [])
    if m == 0:
        return EditDistanceResult(0, n, 0, 0, n, n, float("inf"), [("ins", None, t) for t in hyp_tokens])

    # Standard 2D DP matrix
    dp = [[0] * (n + 1) for _ in range(m + 1)]
    for i in range(m + 1):
        dp[i][0] = i
    for j in range(n + 1):
        dp[0][j] = j

    for i in range(1, m + 1):
        r_tok = ref_tokens[i - 1]
        for j in range(1, n + 1):
            h_tok = hyp_tokens[j - 1]
            if r_tok == h_tok:
                dp[i][j] = dp[i - 1][j - 1]
            else:
                dp[i][j] = 1 + min(
                    dp[i - 1][j - 1],  # Substitution
                    dp[i - 1][j],      # Deletion
                    dp[i][j - 1],      # Insertion
                )

    # Backtracking to reconstruct exact edit operations
    i, j = m, n
    subs = 0
    dels = 0
    inss = 0
    ops: List[Tuple[str, Optional[Any], Optional[Any]]] = []

    while i > 0 or j > 0:
        if i > 0 and j > 0 and ref_tokens[i - 1] == hyp_tokens[j - 1]:
            ops.append(("match", ref_tokens[i - 1], hyp_tokens[j - 1]))
            i -= 1
            j -= 1
        elif i > 0 and j > 0 and dp[i][j] == dp[i - 1][j - 1] + 1:
            ops.append(("sub", ref_tokens[i - 1], hyp_tokens[j - 1]))
            subs += 1
            i -= 1
            j -= 1
        elif i > 0 and dp[i][j] == dp[i - 1][j] + 1:
            ops.append(("del", ref_tokens[i - 1], None))
            dels += 1
            i -= 1
        else:
            ops.append(("ins", None, hyp_tokens[j - 1]))
            inss += 1
            j -= 1

    ops.reverse()
    total_edits = subs + dels + inss
    error_rate = total_edits / float(m)

    return EditDistanceResult(
        ref_count=m,
        hyp_count=n,
        substitutions=subs,
        deletions=dels,
        insertions=inss,
        total_edits=total_edits,
        error_rate=error_rate,
        alignment_ops=ops,
    )


def compute_cer(ref_text: str, hyp_text: str) -> EditDistanceResult:
    """Character Error Rate over Unicode code points."""
    ref_chars = list(ref_text)
    hyp_chars = list(hyp_text)
    return compute_levenshtein(ref_chars, hyp_chars)


def compute_wer(ref_text: str, hyp_text: str) -> EditDistanceResult:
    """Word Error Rate over whitespace-split tokens."""
    ref_words = ref_text.split()
    hyp_words = hyp_text.split()
    return compute_levenshtein(ref_words, hyp_words)


# ============================================================================
# 4. DIFF VISIBILITY & MISMATCH ANALYSIS
# ============================================================================

def categorize_mismatch(ref_sub: str, hyp_sub: str) -> str:
    """Diagnoses error source for error-diff reporting."""
    if not ref_sub:
        return "Spurious insertion (OCR artifact / noise)"
    if not hyp_sub:
        return "Missing text (undetected characters / skipped region)"
    
    # Check digits
    if any(c.isdigit() for c in ref_sub) or any(c.isdigit() for c in hyp_sub):
        return "Numeric / date / reference ID confusion"
    # Check ASCII / English tokens
    if any(c.isascii() and c.isalpha() for c in ref_sub) or any(c.isascii() and c.isalpha() for c in hyp_sub):
        return "English / mixed-script confusion"
    # Check Sinhala Unicode block
    has_sin = any("\u0D80" <= c <= "\u0DFF" for c in ref_sub)
    if has_sin:
        return "Sinhala character / modifier glyph confusion"
    return "Symbol / punctuation / spacing confusion"


def extract_diff_snippets(
    ops: List[Tuple[str, Optional[Any], Optional[Any]]],
    is_char_level: bool = True,
    max_snippets: int = 5,
    context_window: int = 15,
) -> List[Dict[str, str]]:
    """
    Extracts representative mismatch windows from alignment operations.
    Shows REFERENCE vs OCR hypothesis around edit regions.
    """
    snippets = []
    idx = 0
    n = len(ops)

    while idx < n and len(snippets) < max_snippets:
        op, r, h = ops[idx]
        if op == "match":
            idx += 1
            continue

        # Found start of a mismatch region; collect contiguous edits
        mismatch_start = idx
        while idx < n and ops[idx][0] != "match":
            idx += 1
        mismatch_end = idx

        # Build window with preceding and succeeding context
        ctx_before_start = max(0, mismatch_start - (context_window if is_char_level else 3))
        ctx_after_end = min(n, mismatch_end + (context_window if is_char_level else 3))

        ref_parts = []
        hyp_parts = []
        ref_diff = []
        hyp_diff = []

        for k in range(ctx_before_start, ctx_after_end):
            o_k, r_k, h_k = ops[k]
            sep = "" if is_char_level else " "
            if r_k is not None:
                ref_parts.append(str(r_k))
                if mismatch_start <= k < mismatch_end:
                    ref_diff.append(str(r_k))
            if h_k is not None:
                hyp_parts.append(str(h_k))
                if mismatch_start <= k < mismatch_end:
                    hyp_diff.append(str(h_k))

        sep = "" if is_char_level else " "
        ref_snippet = sep.join(ref_parts)
        hyp_snippet = sep.join(hyp_parts)
        category = categorize_mismatch(sep.join(ref_diff), sep.join(hyp_diff))

        snippets.append({
            "reference": ref_snippet,
            "ocr": hyp_snippet,
            "category": category,
        })

        # Skip ahead slightly past the context window to find distinct errors
        idx = min(n, ctx_after_end)

    return snippets


# ============================================================================
# 5. OCR RUNNER VIA INTERNAL PIPELINE
# ============================================================================

def run_pipeline_ocr(
    pdf_path: Path,
    pages: List[int],
    language_mode: str = DEFAULT_LANG_MODE,
    preprocess: bool = True,
    output_dir: Path = OUTPUTS_DIR,
) -> Dict[int, Path]:
    """
    Renders specified pages of the PDF and executes the existing OpenCV/Tesseract
    OCR pipeline directly, completely bypassing Qwen and semantic extraction.
    Saves raw OCR outputs to the outputs/ directory.
    """
    import pymupdf
    from PIL import Image
    from app.ocr.engine import OCREngine

    if not pdf_path.exists():
        raise FileNotFoundError(f"Input PDF not found: {pdf_path}")

    output_dir.mkdir(parents=True, exist_ok=True)
    ocr_engine = OCREngine()
    ocr_engine.validate_language_availability(language_mode)

    print(f"Opening PDF document: {pdf_path}")
    doc = pymupdf.open(str(pdf_path))
    total_doc_pages = len(doc)
    print(f"Total pages in document: {total_doc_pages}")

    results: Dict[int, Path] = {}

    for page_num in pages:
        if page_num < 1 or page_num > total_doc_pages:
            print(f"[WARNING] Page {page_num} is out of bounds (1..{total_doc_pages}). Skipping.")
            continue

        print(f"\n--- Extracting OCR for Page {page_num:02d} (lang='{language_mode}', preprocess={preprocess}) ---")
        # 1-based page_num maps to 0-based page_idx
        doc_page = doc.load_page(page_num - 1)
        pix = doc_page.get_pixmap(dpi=300)
        img = Image.frombytes("RGB", [pix.width, pix.height], pix.samples)

        ocr_result = ocr_engine.extract_page_ocr(
            image=img,
            page_number=page_num,
            language_mode=language_mode,
            preprocess=preprocess,
        )

        out_file = output_dir / f"proposal1_page{page_num:02d}_ocr.txt"
        out_file.write_text(ocr_result.raw_text, encoding="utf-8")
        results[page_num] = out_file

        print(f"  Raw OCR length:   {len(ocr_result.raw_text)} chars")
        print(f"  Clean OCR length: {len(ocr_result.clean_text)} chars")
        print(f"  Confidence:       {ocr_result.confidence}%" if ocr_result.confidence is not None else "  Confidence: N/A")
        print(f"  Saved raw output to: {out_file}")

    doc.close()
    return results


# ============================================================================
# 6. EVALUATION RUNNER & METRIC REPORTING
# ============================================================================

@dataclass
class PageEvalReport:
    page_number: int
    status: str  # "Evaluated" | "Refused"
    ref_file: Path
    hyp_file: Path
    reason: Optional[str] = None
    # Strict
    strict_cer: Optional[EditDistanceResult] = None
    strict_wer: Optional[EditDistanceResult] = None
    # Whitespace-normalized
    ws_cer: Optional[EditDistanceResult] = None
    ws_wer: Optional[EditDistanceResult] = None
    diff_snippets: Optional[List[Dict[str, str]]] = None


def run_evaluation(
    pages: List[int],
    ground_truth_dir: Path = GROUND_TRUTH_DIR,
    outputs_dir: Path = OUTPUTS_DIR,
) -> List[PageEvalReport]:
    """
    Evaluates OCR outputs against manually transcribed ground truth.
    Strictly refuses to compute CER/WER if ground truth is empty or placeholder.
    """
    reports: List[PageEvalReport] = []

    for page_num in pages:
        ref_file = ground_truth_dir / f"proposal1_page{page_num:02d}.txt"
        hyp_file = outputs_dir / f"proposal1_page{page_num:02d}_ocr.txt"

        if not hyp_file.exists():
            reports.append(PageEvalReport(
                page_number=page_num,
                status="Refused",
                ref_file=ref_file,
                hyp_file=hyp_file,
                reason=f"OCR output file does not exist: {hyp_file.name}. Run --extract first.",
            ))
            continue

        is_valid, gt_msg, raw_ref = validate_ground_truth(ref_file)
        if not is_valid:
            reports.append(PageEvalReport(
                page_number=page_num,
                status="Refused",
                ref_file=ref_file,
                hyp_file=hyp_file,
                reason=f"Ground truth not ready: {gt_msg}",
            ))
            continue

        raw_hyp = hyp_file.read_text(encoding="utf-8")

        # 1. Strict view
        s_ref = normalize_strict(raw_ref)
        s_hyp = normalize_strict(raw_hyp)
        strict_cer = compute_cer(s_ref, s_hyp)
        strict_wer = compute_wer(s_ref, s_hyp)

        # 2. Whitespace-normalized view
        ws_ref = normalize_whitespace(raw_ref)
        ws_hyp = normalize_whitespace(raw_hyp)
        ws_cer = compute_cer(ws_ref, ws_hyp)
        ws_wer = compute_wer(ws_ref, ws_hyp)

        # 3. Diff snippets
        diffs = extract_diff_snippets(strict_cer.alignment_ops, is_char_level=True, max_snippets=4)

        reports.append(PageEvalReport(
            page_number=page_num,
            status="Evaluated",
            ref_file=ref_file,
            hyp_file=hyp_file,
            strict_cer=strict_cer,
            strict_wer=strict_wer,
            ws_cer=ws_cer,
            ws_wer=ws_wer,
            diff_snippets=diffs,
        ))

    return reports


def print_evaluation_summary(reports: List[PageEvalReport]) -> None:
    """Prints structured metric tables and diff diagnostics."""
    print("\n" + "=" * 90)
    print("OCR BENCHMARK EVALUATION REPORT — PROPOSAL 1 (Tesseract 5 'sin')")
    print("=" * 90)

    evaluated = [r for r in reports if r.status == "Evaluated"]
    refused = [r for r in reports if r.status == "Refused"]

    if refused:
        print("\n[GROUND TRUTH STATUS / REFUSED PAGES]")
        for r in refused:
            print(f"  • Page {r.page_number:02d}: REFUSED — {r.reason}")
        print("  Notice: Evaluator adheres to research invariants and refuses to report fake metrics.")

    if not evaluated:
        print("\nNo pages had valid manually transcribed ground truth.")
        print("To compute CER/WER: Populate the ground_truth/ files with verified human transcriptions.")
        print("=" * 90)
        return

    # Per-page table
    print("\n--- PER-PAGE METRICS (STRICT vs WHITESPACE-NORMALIZED) ---")
    headers = [
        "Page", "Ref Chars", "Hyp Chars", "Strict CER", "WS CER",
        "Ref Words", "Hyp Words", "Strict WER", "WS WER"
    ]
    print(f"{headers[0]:<6} | {headers[1]:<10} | {headers[2]:<10} | {headers[3]:<12} | {headers[4]:<12} | {headers[5]:<10} | {headers[6]:<10} | {headers[7]:<12} | {headers[8]:<12}")
    print("-" * 110)

    for r in evaluated:
        sc = r.strict_cer
        wc = r.ws_cer
        sw = r.strict_wer
        ww = r.ws_wer
        print(
            f"Page {r.page_number:02d} | "
            f"{sc.ref_count:<10} | {sc.hyp_count:<10} | {sc.error_rate * 100:>6.2f}% ({sc.total_edits}) | {wc.error_rate * 100:>6.2f}% ({wc.total_edits}) | "
            f"{sw.ref_count:<10} | {sw.hyp_count:<10} | {sw.error_rate * 100:>6.2f}% ({sw.total_edits}) | {ww.error_rate * 100:>6.2f}% ({ww.total_edits})"
        )

    # Detailed breakdown table
    print("\n--- DETAILED EDIT OPERATION BREAKDOWN (Strict View) ---")
    print(f"{'Page':<6} | {'Level':<6} | {'Substitutions':<14} | {'Deletions':<10} | {'Insertions':<12} | {'Total Edits':<12} | {'Error Rate':<12}")
    print("-" * 85)
    for r in evaluated:
        sc = r.strict_cer
        sw = r.strict_wer
        print(f"Page {r.page_number:02d} | Char   | {sc.substitutions:<14} | {sc.deletions:<10} | {sc.insertions:<12} | {sc.total_edits:<12} | {sc.error_rate * 100:>6.2f}%")
        print(f"Page {r.page_number:02d} | Word   | {sw.substitutions:<14} | {sw.deletions:<10} | {sw.insertions:<12} | {sw.total_edits:<12} | {sw.error_rate * 100:>6.2f}%")

    # Micro-aggregates across all available pages
    sum_ref_chars = sum(r.strict_cer.ref_count for r in evaluated)
    sum_char_subs = sum(r.strict_cer.substitutions for r in evaluated)
    sum_char_dels = sum(r.strict_cer.deletions for r in evaluated)
    sum_char_inss = sum(r.strict_cer.insertions for r in evaluated)
    sum_char_edits = sum_char_subs + sum_char_dels + sum_char_inss
    micro_strict_cer = (sum_char_edits / sum_ref_chars) if sum_ref_chars > 0 else 0.0

    sum_ws_ref_chars = sum(r.ws_cer.ref_count for r in evaluated)
    sum_ws_char_edits = sum(r.ws_cer.total_edits for r in evaluated)
    micro_ws_cer = (sum_ws_char_edits / sum_ws_ref_chars) if sum_ws_ref_chars > 0 else 0.0

    sum_ref_words = sum(r.strict_wer.ref_count for r in evaluated)
    sum_word_subs = sum(r.strict_wer.substitutions for r in evaluated)
    sum_word_dels = sum(r.strict_wer.deletions for r in evaluated)
    sum_word_inss = sum(r.strict_wer.insertions for r in evaluated)
    sum_word_edits = sum_word_subs + sum_word_dels + sum_word_inss
    micro_strict_wer = (sum_word_edits / sum_ref_words) if sum_ref_words > 0 else 0.0

    sum_ws_ref_words = sum(r.ws_wer.ref_count for r in evaluated)
    sum_ws_word_edits = sum(r.ws_wer.total_edits for r in evaluated)
    micro_ws_wer = (sum_ws_word_edits / sum_ws_ref_words) if sum_ws_ref_words > 0 else 0.0

    macro_strict_cer = sum(r.strict_cer.error_rate for r in evaluated) / len(evaluated)
    macro_strict_wer = sum(r.strict_wer.error_rate for r in evaluated) / len(evaluated)

    print("\n--- AGGREGATE METRICS ACROSS EVALUATED PAGES ---")
    print(f"Evaluated Page Count: {len(evaluated)} pages")
    print(f"Micro-Aggregate Strict CER:           {micro_strict_cer * 100:.2f}% (Edits: {sum_char_edits} / Ref Chars: {sum_ref_chars})")
    print(f"Micro-Aggregate Whitespace-Norm CER:  {micro_ws_cer * 100:.2f}% (Edits: {sum_ws_char_edits} / Ref Chars: {sum_ws_ref_chars})")
    print(f"Micro-Aggregate Strict WER:           {micro_strict_wer * 100:.2f}% (Edits: {sum_word_edits} / Ref Words: {sum_ref_words})")
    print(f"Micro-Aggregate Whitespace-Norm WER:  {micro_ws_wer * 100:.2f}% (Edits: {sum_ws_word_edits} / Ref Words: {sum_ws_ref_words})")
    print(f"Macro-Average Strict CER:             {macro_strict_cer * 100:.2f}% (unweighted mean of page CERs)")
    print(f"Macro-Average Strict WER:             {macro_strict_wer * 100:.2f}% (unweighted mean of page WERs)")

    # Diagnostic diff samples
    print("\n--- DIAGNOSTIC ERROR SAMPLES (Mismatch Regions) ---")
    for r in evaluated:
        print(f"\n[Page {r.page_number:02d} Diagnostic Snippets]")
        if not r.diff_snippets:
            print("  (No mismatches detected — perfect alignment)")
        else:
            for s_idx, snip in enumerate(r.diff_snippets, 1):
                print(f"  Snippet {s_idx} ({snip['category']}):")
                print(f"    REFERENCE: {snip['reference']}")
                print(f"    OCR:       {snip['ocr']}")

    print("\n" + "=" * 90)


# ============================================================================
# 7. MAIN CLI
# ============================================================================

def main():
    parser = argparse.ArgumentParser(
        description="Evaluate existing OCR pipeline (CER & WER) against manual ground truth.",
        formatter_class=argparse.ArgumentDefaultsHelpFormatter,
    )
    parser.add_argument(
        "--pdf",
        type=Path,
        default=DEFAULT_PDF_PATH,
        help="Path to input proposal PDF document.",
    )
    parser.add_argument(
        "--pages",
        type=str,
        default="3,5,14",
        help="Comma-separated 1-based page numbers to extract and evaluate.",
    )
    parser.add_argument(
        "--lang",
        type=str,
        default=DEFAULT_LANG_MODE,
        choices=["sin", "eng", "sin+eng"],
        help="Tesseract OCR language mode.",
    )
    parser.add_argument(
        "--no-preprocess",
        action="store_true",
        help="Disable deterministic OpenCV contrast/denoise/deskew preprocessing.",
    )
    parser.add_argument(
        "--extract",
        action="store_true",
        help="Run OCR extraction on the specified pages and save to outputs/.",
    )
    parser.add_argument(
        "--evaluate",
        action="store_true",
        help="Run CER/WER evaluation against ground_truth/ files.",
    )
    parser.add_argument(
        "--ground-truth-dir",
        type=Path,
        default=GROUND_TRUTH_DIR,
        help="Directory containing manual ground-truth text files.",
    )
    parser.add_argument(
        "--outputs-dir",
        type=Path,
        default=OUTPUTS_DIR,
        help="Directory containing OCR extracted text files.",
    )

    args = parser.parse_args()

    # Parse page list
    try:
        pages = [int(p.strip()) for p in args.pages.split(",") if p.strip()]
    except ValueError:
        print(f"Invalid page numbers: {args.pages}. Must be comma-separated integers.")
        sys.exit(1)

    # Default action: if neither --extract nor --evaluate is specified, do both!
    do_extract = args.extract or (not args.extract and not args.evaluate)
    do_evaluate = args.evaluate or (not args.extract and not args.evaluate)

    if do_extract:
        print(f"Executing OCR extraction on pages {pages} using lang='{args.lang}'...")
        run_pipeline_ocr(
            pdf_path=args.pdf,
            pages=pages,
            language_mode=args.lang,
            preprocess=not args.no_preprocess,
            output_dir=args.outputs_dir,
        )

    if do_evaluate:
        print(f"\nEvaluating CER / WER on pages {pages}...")
        reports = run_evaluation(
            pages=pages,
            ground_truth_dir=args.ground_truth_dir,
            outputs_dir=args.outputs_dir,
        )
        print_evaluation_summary(reports)


if __name__ == "__main__":
    main()

