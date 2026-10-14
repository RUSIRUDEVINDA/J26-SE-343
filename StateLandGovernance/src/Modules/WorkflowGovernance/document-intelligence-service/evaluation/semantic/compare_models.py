"""
Controlled Semantic-Model Comparison Runner: Qwen3 1.7B vs Gemini 3.8 Flash.

Evaluates semantic extraction precision across all 17 governed fields on the exact
same Tesseract OCR output for Proposal1.pdf against authoritative Human Reference answers.

Invariants:
1. Identical OCR input for both models (=== PAGE X ===).
2. Evaluates against PROPOSAL1_HUMAN_REFERENCE.
3. Classifications: Correct, PartiallyCorrect, Incorrect, CorrectMissing, FalsePositive.
4. Strictly distinguishes 17-field total and restored 9 critical fields:
   (applicant_name, project_title, land_location, district, divisional_secretariat_division,
    land_extent, requested_lease_period, proposed_land_use, legal_statutory_basis).
5. Separates semantic correctness, grounding status, and governance review status.
6. When Gemini API key is missing or call is blocked, marks run as NotExecuted
   (scores and latency are N/A; does NOT score infrastructure failures as predictions).
7. Records OCR input SHA-256 hash, artifact timestamps, and timing provenance.
"""

import argparse
import hashlib
import json
import os
import sys
import time
from datetime import datetime, timezone
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple

# Add parent path for imports
_current_dir = Path(__file__).resolve().parent
_service_root = _current_dir.parent.parent
if str(_service_root) not in sys.path:
    sys.path.insert(0, str(_service_root))

from app.core.config import settings
from app.semantic.catalog import CANONICAL_FIELDS, CRITICAL_FIELD_CODES
from app.semantic.extractor import (
    BaseSemanticExtractor,
    OllamaQwenSemanticExtractor,
    SemanticExtractionResult,
    get_semantic_extractor,
)
from app.semantic.gemini_client import (
    GeminiAuthenticationError,
    GeminiBadRequestError,
    GeminiClient,
    GeminiError,
    GeminiNotFoundError,
)
from app.semantic.gemini_extractor import GeminiSemanticExtractor
from evaluation.semantic.reference_answers import (
    PROPOSAL1_HUMAN_REFERENCE,
    HumanReferenceField,
)


@dataclass
class FieldEvaluationDetail:
    """Evaluation outcome for one field of one model."""

    field_code: str
    display_name: str
    is_critical: bool
    human_reference: Optional[str]
    expected_status: str
    model_status: str  # Extracted, Missing, Rejected, NeedsReview, Ungrounded
    model_value: Optional[str]
    model_raw_value: Optional[str]
    is_grounded: bool
    page_number: Optional[int]
    evidence_quote: Optional[str]
    evaluation_verdict: str  # Correct, PartiallyCorrect, Incorrect, CorrectMissing, FalsePositive
    semantic_correctness: str  # Correct, PartiallyCorrect, Incorrect, CorrectMissing
    governance_status: str  # Accepted, NeedsReview, Rejected, Missing
    verdict_rationale: str


import re


def normalize_text_for_comparison(text: Optional[str]) -> str:
    """
    Normalizes candidate text and human reference values for fair semantic matching:
    1. Removes invisible zero-width Unicode characters (ZWNJ \u200c, ZWJ \u200d, \u200b, \ufeff).
    2. Strips surrounding punctuation, quotes, brackets, and colons.
    3. Collapses multiple whitespace characters.
    """
    if not text:
        return ""
    # Strip zero-width formatting codepoints
    cleaned = re.sub(r"[\u200b\u200c\u200d\ufeff]+", "", str(text))
    # Strip surrounding quotation marks, punctuation, colons, brackets
    cleaned = cleaned.strip()
    cleaned = re.sub(r"^[\s\.\,\:\;\-\_\"\'“‘”’\(\)\[\]\{\}]+|[\s\.\,\:\;\-\_\"\'“‘”’\(\)\[\]\{\}]+$", "", cleaned)
    # Collapse multiple spaces
    cleaned = re.sub(r"\s+", " ", cleaned).strip()
    return cleaned


@dataclass
class ModelEvaluationSummary:
    """Aggregated evaluation metrics for a single model run."""

    model_identifier: str
    provider: str
    execution_status: str  # Executed, NotExecuted, Failed
    duration_ms: Optional[float]
    ocr_duration_ms: float
    total_duration_ms: Optional[float]
    candidate_count: int
    accepted_count: int
    rejected_count: int
    conflict_count: int
    ungrounded_count: int
    # 17-field metrics
    total_fields: int
    correct_count: int
    partially_correct_count: int
    incorrect_count: int
    correct_missing_count: int
    false_positive_count: int
    score_17: Optional[float]  # Strict accuracy: (correct + correct_missing) / 17
    score_17_weighted: Optional[float]  # Weighted: (correct + correct_missing + 0.5 * partial) / 17
    # 9-critical field metrics
    critical_fields_count: int
    critical_correct: int
    critical_partially_correct: int
    critical_correct_missing: int
    critical_score: Optional[float]  # Strict critical: (critical_correct + critical_correct_missing) / 9
    critical_score_weighted: Optional[float]  # Weighted: (critical_correct + critical_correct_missing + 0.5 * critical_partial) / 9
    # Breakdown: Expected-present (13) vs Expected-missing (4)
    expected_present_total: int
    expected_present_correct: int
    present_accuracy: Optional[float]  # correct / 13
    expected_missing_total: int
    expected_missing_correct: int
    missing_accuracy: Optional[float]  # correct_missing / 4
    # Candidate recall (disregarding conservative conflict escalation)
    candidate_semantic_recall: Optional[float]
    field_details: Dict[str, FieldEvaluationDetail]
    request_dispatched: bool = True
    failure_reason: Optional[str] = None
    failed_duration_ms: Optional[float] = None
    successful_attempt_duration_ms: Optional[float] = None
    retry_backoff_duration_ms: Optional[float] = None
    attempts_made: int = 1


def evaluate_field_extraction(
    ref: HumanReferenceField,
    inv_item: Optional[Dict[str, Any]],
    candidate_override: Optional[Dict[str, Any]] = None,
) -> FieldEvaluationDetail:
    """
    Evaluates an extracted inventory item against human reference ground truth.
    Separates semantic correctness, grounding status, and governance review status.
    """
    field_code = ref.field_code
    display_name = ref.display_name
    is_critical = ref.is_critical
    expected_status = ref.expected_status
    human_ref_val = ref.reference_value

    if not inv_item:
        model_status = "Missing"
        model_val = None
        model_raw = None
        is_grounded = False
        page_num = None
        quote = None
    else:
        model_status = inv_item.get("status", "Missing")
        model_val = inv_item.get("preferred_value") or inv_item.get("preferredValue")
        model_raw = inv_item.get("raw_value") or inv_item.get("rawValue")
        is_grounded = inv_item.get("is_grounded") if "is_grounded" in inv_item else inv_item.get("isGrounded", False)
        page_num = inv_item.get("page_number") or inv_item.get("pageNumber")
        quote = inv_item.get("quote") or inv_item.get("evidenceQuote")

    # If inventory item has no preferred value, check raw candidate if available for audit
    audit_val = model_val or model_raw
    if candidate_override:
        if not quote:
            quote = candidate_override.get("quote") or candidate_override.get("evidenceQuote")
        if not page_num:
            page_num = candidate_override.get("page_number") or candidate_override.get("pageNumber") or candidate_override.get("evidencePage")
        if not is_grounded:
            is_grounded = bool(
                candidate_override.get("is_grounded")
                or candidate_override.get("isGrounded")
                or candidate_override.get("is_evidence_verified", False)
            )

    # Case 1: Ground truth is MISSING
    if expected_status == "Missing":
        if model_status in ("Missing", "Rejected", "Ungrounded") or not model_val:
            return FieldEvaluationDetail(
                field_code=field_code,
                display_name=display_name,
                is_critical=is_critical,
                human_reference=human_ref_val,
                expected_status=expected_status,
                model_status=model_status,
                model_value=model_val,
                model_raw_value=model_raw,
                is_grounded=is_grounded,
                page_number=page_num,
                evidence_quote=quote,
                evaluation_verdict="CorrectMissing",
                semantic_correctness="CorrectMissing",
                governance_status=model_status,
                verdict_rationale="Field is absent in proposal; model correctly abstained.",
            )
        else:
            return FieldEvaluationDetail(
                field_code=field_code,
                display_name=display_name,
                is_critical=is_critical,
                human_reference=human_ref_val,
                expected_status=expected_status,
                model_status=model_status,
                model_value=model_val,
                model_raw_value=model_raw,
                is_grounded=is_grounded,
                page_number=page_num,
                evidence_quote=quote,
                evaluation_verdict="FalsePositive",
                semantic_correctness="Incorrect",
                governance_status=model_status,
                verdict_rationale=f"Model incorrectly accepted candidate '{model_val}' for an absent field.",
            )

    # Case 2: Ground truth is PRESENT (expected_status == "Extracted")
    # Determine candidate semantic correctness regardless of governance status
    val_clean = str(audit_val or "").strip()
    val_norm = normalize_text_for_comparison(val_clean).lower()
    ref_norm = normalize_text_for_comparison(ref.reference_value).lower() if ref.reference_value else ""
    variants_norm = [normalize_text_for_comparison(v).lower() for v in ref.acceptable_variants]

    is_sem_match = bool(val_norm and (
        any(val_norm == v for v in variants_norm) or
        (ref_norm and val_norm == ref_norm)
    ))
    is_partial_match = bool(val_norm and not is_sem_match and (
        any(v in val_norm or val_norm in v for v in variants_norm if len(v) > 3) or
        (ref_norm and (ref_norm in val_norm or val_norm in ref_norm if len(ref_norm) > 3 else False))
    ))

    # Evaluate preferred fact governance verdict
    if model_status == "Extracted" and model_val:
        if is_sem_match:
            verdict = "Correct"
            sem_correct = "Correct"
            rationale = "Preferred value matches human reference answer and evidence is verified."
        elif is_partial_match:
            verdict = "PartiallyCorrect"
            sem_correct = "PartiallyCorrect"
            rationale = f"Preferred value partially matches reference substance ('{model_val}' vs '{human_ref_val}')."
        else:
            verdict = "Incorrect"
            sem_correct = "Incorrect"
            rationale = f"Preferred value '{model_val}' contradicts human reference '{human_ref_val}'."
    else:
        # Preferred value not extracted (NeedsReview, Rejected, Missing)
        verdict = "Incorrect"
        if is_sem_match:
            sem_correct = "Correct"
            rationale = f"Raw candidate is semantically correct ('{audit_val}'), but governance status is '{model_status}' requiring human review."
        elif is_partial_match:
            sem_correct = "PartiallyCorrect"
            rationale = f"Raw candidate partially matches substance, but governance status is '{model_status}'."
        else:
            sem_correct = "Incorrect"
            rationale = f"Field is present in document ('{human_ref_val}') but model marked it {model_status}."

    return FieldEvaluationDetail(
        field_code=field_code,
        display_name=display_name,
        is_critical=is_critical,
        human_reference=human_ref_val,
        expected_status=expected_status,
        model_status=model_status,
        model_value=model_val,
        model_raw_value=model_raw,
        is_grounded=is_grounded,
        page_number=page_num,
        evidence_quote=quote,
        evaluation_verdict=verdict,
        semantic_correctness=sem_correct,
        governance_status=model_status,
        verdict_rationale=rationale,
    )


def summarize_model_evaluation(
    model_identifier: str,
    provider: str,
    field_inventory_items: List[Dict[str, Any]],
    duration_ms: Optional[float],
    ocr_duration_ms: float,
    counts: Dict[str, int],
    candidates: Optional[List[Dict[str, Any]]] = None,
    is_executed: bool = True,
    execution_status: str = "Executed",
    failure_reason: Optional[str] = None,
    failed_duration_ms: Optional[float] = None,
    request_dispatched: bool = True,
    successful_attempt_duration_ms: Optional[float] = None,
    retry_backoff_duration_ms: Optional[float] = None,
    attempts_made: int = 1,
) -> ModelEvaluationSummary:
    """Aggregates field-level evaluations into summary metrics."""
    total_fields = len(PROPOSAL1_HUMAN_REFERENCE)
    total_critical = sum(1 for f in PROPOSAL1_HUMAN_REFERENCE.values() if f.is_critical)
    expected_present_total = sum(1 for f in PROPOSAL1_HUMAN_REFERENCE.values() if f.expected_status == "Extracted")
    expected_missing_total = sum(1 for f in PROPOSAL1_HUMAN_REFERENCE.values() if f.expected_status == "Missing")

    if not is_executed or execution_status in ("Failed", "NotExecuted"):
        resolved_status = execution_status if execution_status in ("Failed", "NotExecuted") else "NotExecuted"
        details: Dict[str, FieldEvaluationDetail] = {}
        for code, ref in PROPOSAL1_HUMAN_REFERENCE.items():
            details[code] = FieldEvaluationDetail(
                field_code=code,
                display_name=ref.display_name,
                is_critical=ref.is_critical,
                human_reference=ref.reference_value,
                expected_status=ref.expected_status,
                model_status="Unavailable" if resolved_status == "Failed" else "NotExecuted",
                model_value=None,
                model_raw_value=None,
                is_grounded=False,
                page_number=None,
                evidence_quote=None,
                semantic_correctness="N/A",
                governance_status="Unavailable" if resolved_status == "Failed" else "NotExecuted",
                evaluation_verdict="N/A",
                verdict_rationale=(
                    f"Model execution failed: {failure_reason}"
                    if resolved_status == "Failed"
                    else f"Model not executed: {failure_reason or 'GEMINI_API_KEY unconfigured'}"
                ),
            )

        return ModelEvaluationSummary(
            model_identifier=model_identifier,
            provider=provider,
            execution_status=resolved_status,
            duration_ms=None,
            ocr_duration_ms=ocr_duration_ms,
            total_duration_ms=None,
            candidate_count=0,
            accepted_count=0,
            rejected_count=0,
            conflict_count=0,
            ungrounded_count=0,
            total_fields=total_fields,
            correct_count=0,
            partially_correct_count=0,
            incorrect_count=0,
            correct_missing_count=0,
            false_positive_count=0,
            score_17=None,
            score_17_weighted=None,
            critical_fields_count=total_critical,
            critical_correct=0,
            critical_partially_correct=0,
            critical_correct_missing=0,
            critical_score=None,
            critical_score_weighted=None,
            expected_present_total=expected_present_total,
            expected_present_correct=0,
            present_accuracy=None,
            expected_missing_total=expected_missing_total,
            expected_missing_correct=0,
            missing_accuracy=None,
            candidate_semantic_recall=None,
            field_details=details,
            request_dispatched=request_dispatched,
            failure_reason=failure_reason,
            failed_duration_ms=failed_duration_ms,
            successful_attempt_duration_ms=None,
            retry_backoff_duration_ms=None,
            attempts_made=attempts_made,
        )

    inv_by_code = {item.get("fieldCode") or item.get("field_code"): item for item in field_inventory_items}
    best_cand_by_code: Dict[str, Dict[str, Any]] = {}
    if candidates:
        for c in candidates:
            code = c.get("factCode") or c.get("fieldCode")
            if code and code not in best_cand_by_code:
                best_cand_by_code[code] = c

    details: Dict[str, FieldEvaluationDetail] = {}

    correct = 0
    partially_correct = 0
    incorrect = 0
    correct_missing = 0
    false_positive = 0

    critical_correct = 0
    critical_partially_correct = 0
    critical_correct_missing = 0
    raw_sem_correct = 0

    for code, ref in PROPOSAL1_HUMAN_REFERENCE.items():
        det = evaluate_field_extraction(ref, inv_by_code.get(code), best_cand_by_code.get(code))
        details[code] = det

        # Autonomous Preferred Fact Scoring
        if det.evaluation_verdict == "Correct":
            correct += 1
            if ref.is_critical:
                critical_correct += 1
        elif det.evaluation_verdict == "PartiallyCorrect":
            partially_correct += 1
            if ref.is_critical:
                critical_partially_correct += 1
        elif det.evaluation_verdict == "CorrectMissing":
            correct_missing += 1
            if ref.is_critical:
                critical_correct_missing += 1
        elif det.evaluation_verdict == "FalsePositive":
            false_positive += 1
        else:
            incorrect += 1

        # Candidate-level semantic recall
        if det.semantic_correctness in ("Correct", "CorrectMissing"):
            raw_sem_correct += 1
        elif det.semantic_correctness == "PartiallyCorrect":
            raw_sem_correct += 0.5

    score_17 = round((correct + correct_missing) / total_fields, 4)
    score_17_weighted = round((correct + correct_missing + 0.5 * partially_correct) / total_fields, 4)
    # Strict Critical Score: only fully correct fields and correct missings
    critical_score = round((critical_correct + critical_correct_missing) / total_critical, 4)
    critical_score_weighted = round((critical_correct + critical_correct_missing + 0.5 * critical_partially_correct) / total_critical, 4)
    present_accuracy = round(correct / expected_present_total, 4) if expected_present_total else 0.0
    missing_accuracy = round(correct_missing / expected_missing_total, 4) if expected_missing_total else 0.0
    candidate_recall = round(raw_sem_correct / total_fields, 4)

    return ModelEvaluationSummary(
        model_identifier=model_identifier,
        provider=provider,
        execution_status="Executed",
        duration_ms=duration_ms,
        ocr_duration_ms=ocr_duration_ms,
        total_duration_ms=ocr_duration_ms + (duration_ms or 0.0),
        candidate_count=counts.get("candidate_count", 0),
        accepted_count=counts.get("accepted_count", 0),
        rejected_count=counts.get("rejected_count", 0),
        conflict_count=counts.get("conflict_count", 0),
        ungrounded_count=counts.get("ungrounded_count", 0),
        total_fields=total_fields,
        correct_count=correct,
        partially_correct_count=partially_correct,
        incorrect_count=incorrect,
        correct_missing_count=correct_missing,
        false_positive_count=false_positive,
        score_17=score_17,
        score_17_weighted=score_17_weighted,
        critical_fields_count=total_critical,
        critical_correct=critical_correct,
        critical_partially_correct=critical_partially_correct,
        critical_correct_missing=critical_correct_missing,
        critical_score=critical_score,
        critical_score_weighted=critical_score_weighted,
        expected_present_total=expected_present_total,
        expected_present_correct=correct,
        present_accuracy=present_accuracy,
        expected_missing_total=expected_missing_total,
        expected_missing_correct=correct_missing,
        missing_accuracy=missing_accuracy,
        candidate_semantic_recall=candidate_recall,
        field_details=details,
        request_dispatched=request_dispatched,
        successful_attempt_duration_ms=successful_attempt_duration_ms,
        retry_backoff_duration_ms=retry_backoff_duration_ms,
        attempts_made=attempts_made,
    )


def load_cached_proposal1_ocr_pages(cached_json_path: Path) -> Tuple[List[Dict[str, Any]], Dict[str, Any], str]:
    """Loads pre-extracted OCR pages and cached run from scratch JSON."""
    if not cached_json_path.exists():
        raise FileNotFoundError(f"Cached full proposal result not found at: {cached_json_path}")
    with open(cached_json_path, "r", encoding="utf-8") as f:
        data = json.load(f)
    pages = [
        {"page_number": p["pageNumber"], "text": p.get("cleanText") or p.get("rawText", "")}
        for p in data.get("pages", [])
    ]
    parts = ["=== PAGE " + str(p["pageNumber"]) + " ===\n" + (p.get("cleanText") or p.get("rawText", "")) for p in data.get("pages", [])]
    ocr_hash = hashlib.sha256("\n\n".join(parts).encode("utf-8")).hexdigest()
    return pages, data, ocr_hash


def format_comparison_markdown_table(
    qwen_summary: Optional[ModelEvaluationSummary],
    gemini_summary: Optional[ModelEvaluationSummary],
) -> str:
    """Builds the comprehensive field-by-field A/B comparison markdown table."""
    lines = []
    lines.append("| # | Field Code | Critical (9) | Consensus Human Reference | Qwen Preferred Value | Qwen Governance Status | Qwen Verdict | Gemini Value | Gemini Verdict |")
    lines.append("|---|---|---|---|---|---|---|---|---|")

    idx = 1
    for code, ref in PROPOSAL1_HUMAN_REFERENCE.items():
        crit_badge = "Yes" if ref.is_critical else "No"
        ref_val = ref.reference_value if ref.reference_value else "_Missing (Expected)_"

        # Qwen
        if qwen_summary and code in qwen_summary.field_details:
            q_det = qwen_summary.field_details[code]
            q_val = f"`{q_det.model_value}`" if q_det.model_value else f"_{q_det.model_status}_"
            q_status = q_det.governance_status
            q_verdict = q_det.evaluation_verdict
        else:
            q_val = "_N/A_"
            q_status = "_N/A_"
            q_verdict = "_N/A_"

        # Gemini
        if gemini_summary and gemini_summary.execution_status == "Executed" and code in gemini_summary.field_details:
            g_det = gemini_summary.field_details[code]
            g_val = f"`{g_det.model_value}`" if g_det.model_value else f"_{g_det.model_status}_"
            g_verdict = g_det.evaluation_verdict
        elif gemini_summary and gemini_summary.execution_status == "Failed":
            g_val = "_Unavailable_"
            g_verdict = "_FailedRun (N/A)_"
        else:
            g_val = "_NotExecuted_"
            g_verdict = "_NotExecuted (N/A)_"

        lines.append(f"| {idx} | `{code}` | {crit_badge} | {ref_val} | {q_val} | {q_status} | {q_verdict} | {g_val} | {g_verdict} |")
        idx += 1

    return "\n".join(lines)


def format_candidate_audit_markdown_table(qwen_summary: ModelEvaluationSummary) -> str:
    """Builds detailed 17-field candidate-level audit table separating semantic validity from governance status."""
    lines = []
    lines.append("| # | Field Code | Crit | Consensus Reference | Extracted Candidate | P. | Grounded Quote | Semantic Match | Governance Status | Final Verdict |")
    lines.append("|---|---|---|---|---|---|---|---|---|---|")

    idx = 1
    for code, ref in PROPOSAL1_HUMAN_REFERENCE.items():
        det = qwen_summary.field_details[code]
        crit_badge = "Yes" if ref.is_critical else "No"
        ref_val = ref.reference_value or "_Missing_"
        cand_val = det.model_value or det.model_raw_value or "_None_"
        page_str = str(det.page_number) if det.page_number else "-"
        grounded_str = "Yes" if det.is_grounded else ("No" if det.evidence_quote else "-")
        sem_str = det.semantic_correctness
        gov_str = det.governance_status
        verdict = det.evaluation_verdict

        lines.append(f"| {idx} | `{code}` | {crit_badge} | {ref_val} | `{cand_val}` | {page_str} | {grounded_str} | {sem_str} | {gov_str} | {verdict} |")
        idx += 1

    return "\n".join(lines)


def save_timestamped_run_artifacts(
    out_dir: Path,
    timestamp_str: str,
    ocr_hash: str,
    pages_count: int,
    qwen_summary: Optional[ModelEvaluationSummary],
    gemini_summary: Optional[ModelEvaluationSummary],
    gemini_raw_response_text: Optional[str],
    gemini_candidates: Optional[List[Dict[str, Any]]],
    gemini_inventory: Optional[List[Dict[str, Any]]],
    qwen_candidates: Optional[List[Dict[str, Any]]],
    qwen_inventory: Optional[List[Dict[str, Any]]],
    gemini_configured_model_id: str,
    gemini_key_configured: bool,
    gemini_provenance_label: str,
    timing_provenance: Dict[str, str],
    gemini_key_source: str = "unconfigured",
) -> Path:
    """
    Saves the 5 required timestamped evaluation artifacts in a non-overwriting run directory:
    1. gemini_raw_response.json - Exact Gemini API response payload (or synthetic replay/failure record).
    2. candidates_and_inventory.json - Parsed candidates and final 17-field inventory.
    3. run_metadata.json - OCR input SHA-256 hash, model identifiers, page count, run timestamp.
    4. timing.json - Attempt-level timing/status and total semantic wall time.
    5. evaluation_results.json - Machine-readable field classifications and aggregate scores.
    """
    run_dir = out_dir / f"run_{timestamp_str}"
    run_dir.mkdir(parents=True, exist_ok=True)

    # 1. Exact Gemini API response
    raw_resp_path = run_dir / "gemini_raw_response.json"
    if gemini_raw_response_text and gemini_raw_response_text.strip():
        try:
            parsed = json.loads(gemini_raw_response_text)
            raw_resp_path.write_text(json.dumps(parsed, indent=2, ensure_ascii=False), encoding="utf-8")
        except Exception:
            raw_resp_path.write_text(gemini_raw_response_text, encoding="utf-8")
    elif gemini_summary and gemini_summary.execution_status == "Failed":
        raw_resp_path.write_text(
            json.dumps({
                "provenance": gemini_provenance_label,
                "execution_status": "Failed",
                "failure_reason": gemini_summary.failure_reason,
                "failed_duration_ms": gemini_summary.failed_duration_ms,
                "attempts_made": gemini_summary.attempts_made,
            }, indent=2, ensure_ascii=False),
            encoding="utf-8",
        )
    else:
        raw_resp_path.write_text(
            json.dumps({
                "provenance": gemini_provenance_label,
                "execution_status": "NotExecuted",
                "reason": "GEMINI_API_KEY unconfigured; infrastructure blocked",
            }, indent=2, ensure_ascii=False),
            encoding="utf-8",
        )

    # 2. Parsed candidates and final inventory
    cand_inv_path = run_dir / "candidates_and_inventory.json"
    cand_inv_data = {
        "timestamp_utc": timestamp_str,
        "gemini": {
            "candidates": gemini_candidates or [],
            "field_inventory": gemini_inventory or [],
        },
        "qwen": {
            "candidates": qwen_candidates or [],
            "field_inventory": qwen_inventory or [],
        },
    }
    cand_inv_path.write_text(json.dumps(cand_inv_data, indent=2, ensure_ascii=False), encoding="utf-8")

    # 3. Run metadata
    metadata_path = run_dir / "run_metadata.json"
    meta_data = {
        "timestamp_utc": timestamp_str,
        "ocr_input_sha256": ocr_hash,
        "ocr_page_count": pages_count,
        "gemini_configured_model_id": gemini_configured_model_id,
        "qwen_model_id": "qwen3:1.7b",
        "gemini_api_key_configured": gemini_key_configured,
        "gemini_key_source": gemini_key_source,
        "gemini_provenance": gemini_provenance_label,
        "gemini_execution_status": gemini_summary.execution_status if gemini_summary else "NotExecuted",
        "timing_provenance": timing_provenance,
    }
    metadata_path.write_text(json.dumps(meta_data, indent=2, ensure_ascii=False), encoding="utf-8")

    # 4. Attempt-level timing and status + total semantic wall time
    timing_path = run_dir / "timing.json"
    timing_data = {
        "timestamp_utc": timestamp_str,
        "ocr_duration_ms": 57200.0,
        "qwen": {
            "execution_status": "Executed",
            "semantic_wall_time_ms": 332000.0,
            "ocr_duration_ms": 57200.0,
            "total_wall_time_ms": 389200.0,
            "provenance": timing_provenance.get("qwen"),
        },
        "gemini": {
            "execution_status": gemini_summary.execution_status if gemini_summary else "NotExecuted",
            "attempts_made": gemini_summary.attempts_made if gemini_summary else 0,
            "duration_ms": gemini_summary.duration_ms if gemini_summary else None,
            "total_duration_ms": gemini_summary.total_duration_ms if gemini_summary else None,
            "successful_attempt_duration_ms": gemini_summary.successful_attempt_duration_ms if gemini_summary else None,
            "retry_backoff_duration_ms": gemini_summary.retry_backoff_duration_ms if gemini_summary else None,
            "failed_duration_ms": gemini_summary.failed_duration_ms if gemini_summary else None,
            "failure_reason": gemini_summary.failure_reason if gemini_summary else None,
            "provenance": timing_provenance.get("gemini"),
        },
    }
    timing_path.write_text(json.dumps(timing_data, indent=2, ensure_ascii=False), encoding="utf-8")

    # 5. Machine-readable field classifications and aggregate scores
    eval_results_path = run_dir / "evaluation_results.json"
    eval_data = {
        "timestamp_utc": timestamp_str,
        "qwen": asdict(qwen_summary) if qwen_summary else None,
        "gemini": asdict(gemini_summary) if gemini_summary else None,
    }
    eval_results_path.write_text(json.dumps(eval_data, indent=2, ensure_ascii=False), encoding="utf-8")

    return run_dir


def run_ab_comparison(
    cached_path: Optional[str] = None,
    gemini_api_key: Optional[str] = None,
    gemini_response_path: Optional[str] = None,
) -> Dict[str, Any]:
    """
    Executes the full controlled semantic-model comparison.
    Supports offline evaluation of saved Gemini JSON responses via gemini_response_path.
    """
    possible_paths = [
        Path(cached_path) if cached_path else None,
        Path("scratch/proposal1_full_result_v4.json"),
        Path(r"C:\Users\Ovindi\.gemini\antigravity\brain\2ed8aa21-0933-4d5d-b518-58823e529549\scratch\proposal1_full_result_v4.json"),
    ]
    resolved_cached = next((p for p in possible_paths if p and p.exists()), None)

    if not resolved_cached:
        raise FileNotFoundError("Could not locate cached proposal OCR output (proposal1_full_result_v4.json).")

    pages, cached_data, ocr_hash = load_cached_proposal1_ocr_pages(resolved_cached)

    # 1. Evaluate Qwen Baseline (historical run from full 18-page execution)
    qwen_inventory = cached_data.get("fieldInventory", [])
    qwen_all_candidates = cached_data.get("allCandidates", [])
    qwen_counts = {
        "candidate_count": cached_data.get("candidateCount", 0),
        "accepted_count": cached_data.get("acceptedCount", 0),
        "rejected_count": cached_data.get("rejectedCount", 0),
        "conflict_count": cached_data.get("conflictCount", 0),
        "ungrounded_count": cached_data.get("ungroundedCount", 0),
    }
    qwen_ocr_dur = 57200.0   # ~57.2s
    qwen_sem_dur = 332000.0  # ~332.0s
    qwen_summary = summarize_model_evaluation(
        model_identifier="qwen3:1.7b",
        provider="qwen",
        field_inventory_items=qwen_inventory,
        duration_ms=qwen_sem_dur,
        ocr_duration_ms=qwen_ocr_dur,
        counts=qwen_counts,
        candidates=qwen_all_candidates,
        is_executed=True,
    )

    # 2. Check Gemini Extractor (offline response file OR live API key)
    # Determine credential source with explicit precedence: CLI -> os.environ -> settings.gemini_api_key (.env)
    if gemini_api_key and gemini_api_key.strip():
        gemini_key_source = "cli_argument"
        raw_gemini_key = gemini_api_key
    elif os.getenv("GEMINI_API_KEY") and os.getenv("GEMINI_API_KEY").strip():
        gemini_key_source = "os.environ"
        raw_gemini_key = os.getenv("GEMINI_API_KEY")
    elif settings.gemini_api_key and settings.gemini_api_key.strip():
        gemini_key_source = ".env"
        raw_gemini_key = settings.gemini_api_key
    else:
        gemini_key_source = "unconfigured"
        raw_gemini_key = None

    # Pre-flight credential check: detect control characters and empty credentials
    has_ctrl_chars = bool(raw_gemini_key and any(ord(c) < 32 or ord(c) == 127 for c in str(raw_gemini_key)))
    clean_gemini_key = str(raw_gemini_key).strip().strip('"\'').strip() if raw_gemini_key else None
    resolved_gemini_key = clean_gemini_key if (clean_gemini_key and not has_ctrl_chars) else None

    gemini_summary: Optional[ModelEvaluationSummary] = None
    gemini_prov = ""
    gemini_raw_response_text: Optional[str] = None
    gemini_candidates_list: List[Dict[str, Any]] = []
    gemini_inv_list: List[Dict[str, Any]] = []
    gemini_provenance_label: str = "unconfigured_infrastructure_blocked"
    configured_model_id = os.getenv("GEMINI_MODEL") or settings.gemini_model

    # Check for explicitly provided response path, or default saved response path
    possible_resp_paths = [
        Path(gemini_response_path) if gemini_response_path else None,
        Path("evaluation/semantic/outputs/gemini_raw_response.json"),
    ]
    resolved_resp_file = next((p for p in possible_resp_paths if p and p.exists()), None)

    if gemini_response_path and not resolved_resp_file:
        raise FileNotFoundError(f"Specified Gemini response file not found at: {gemini_response_path}")

    if resolved_resp_file and (gemini_response_path or not resolved_gemini_key):
        # Offline re-evaluation using saved Gemini response
        with open(resolved_resp_file, "r", encoding="utf-8") as f:
            saved_resp = json.load(f)
        gemini_extractor = GeminiSemanticExtractor(api_key="offline-eval", raise_on_error=False)
        configured_model_id = gemini_extractor.model_name
        gemini_start = time.perf_counter()
        gemini_result = gemini_extractor.extract_from_response(
            gemini_response=saved_resp,
            pages=pages,
            duration_ms=0.0,
        )
        gemini_dur = (time.perf_counter() - gemini_start) * 1000.0
        gemini_inv_dicts = [item.model_dump() if hasattr(item, "model_dump") else asdict(item) for item in gemini_result.field_inventory]
        gemini_counts = {
            "candidate_count": gemini_result.candidate_count,
            "accepted_count": gemini_result.accepted_count,
            "rejected_count": gemini_result.rejected_count,
            "conflict_count": gemini_result.conflict_count,
            "ungrounded_count": gemini_result.ungrounded_count,
        }
        gemini_summary = summarize_model_evaluation(
            model_identifier=gemini_result.model_identifier,
            provider="gemini",
            field_inventory_items=gemini_inv_dicts,
            duration_ms=gemini_dur,
            ocr_duration_ms=qwen_ocr_dur,
            counts=gemini_counts,
            is_executed=True,
            execution_status="Executed",
            request_dispatched=True,
        )
        gemini_prov = f"Offline Replay (from {resolved_resp_file})"
        gemini_raw_response_text = json.dumps(saved_resp, indent=2, ensure_ascii=False)
        gemini_candidates_list = [c.model_dump() if hasattr(c, "model_dump") else asdict(c) for c in gemini_result.all_candidates]
        gemini_inv_list = gemini_inv_dicts
        gemini_provenance_label = f"synthetic_replay (from {resolved_resp_file})"

    elif resolved_gemini_key and resolved_gemini_key.strip():
        gemini_extractor = GeminiSemanticExtractor(api_key=resolved_gemini_key, raise_on_error=True)
        configured_model_id = gemini_extractor.model_name
        gemini_start = time.perf_counter()
        try:
            gemini_result = gemini_extractor.extract(pages=pages, category_hint="LeaseProposal")
            gemini_dur = (time.perf_counter() - gemini_start) * 1000.0

            if hasattr(gemini_extractor.client, "last_raw_http_response_text") and gemini_extractor.client.last_raw_http_response_text:
                gemini_raw_response_text = gemini_extractor.client.last_raw_http_response_text
            elif hasattr(gemini_extractor.client, "last_raw_response_text") and gemini_extractor.client.last_raw_response_text:
                gemini_raw_response_text = gemini_extractor.client.last_raw_response_text
            elif gemini_result.raw_model_response:
                gemini_raw_response_text = gemini_result.raw_model_response

            if not gemini_result.is_success or gemini_result.classification.code == "Unavailable":
                gemini_summary = summarize_model_evaluation(
                    model_identifier=gemini_result.model_identifier,
                    provider="gemini",
                    field_inventory_items=[],
                    duration_ms=None,
                    ocr_duration_ms=qwen_ocr_dur,
                    counts={},
                    is_executed=False,
                    execution_status="Failed",
                    failure_reason=gemini_result.error_message or "API execution failed",
                    failed_duration_ms=gemini_dur,
                    request_dispatched=True,
                )
                gemini_provenance_label = "live_api_failed"
            else:
                stats = gemini_extractor.client.last_execution_stats
                gemini_inv_dicts = [item.model_dump() if hasattr(item, "model_dump") else asdict(item) for item in gemini_result.field_inventory]
                gemini_counts = {
                    "candidate_count": gemini_result.candidate_count,
                    "accepted_count": gemini_result.accepted_count,
                    "rejected_count": gemini_result.rejected_count,
                    "conflict_count": gemini_result.conflict_count,
                    "ungrounded_count": gemini_result.ungrounded_count,
                }
                succ_ms = stats.get("successful_attempt_duration_seconds", 0) * 1000.0 if stats.get("successful_attempt_duration_seconds") else None
                back_ms = stats.get("retry_backoff_duration_seconds", 0) * 1000.0 if stats.get("retry_backoff_duration_seconds") else None
                attempts = stats.get("attempts_made", 1)
                gemini_summary = summarize_model_evaluation(
                    model_identifier=gemini_result.model_identifier,
                    provider="gemini",
                    field_inventory_items=gemini_inv_dicts,
                    duration_ms=gemini_dur,
                    ocr_duration_ms=qwen_ocr_dur,  # exact same OCR input
                    counts=gemini_counts,
                    is_executed=True,
                    execution_status="Executed",
                    request_dispatched=True,
                    successful_attempt_duration_ms=succ_ms,
                    retry_backoff_duration_ms=back_ms,
                    attempts_made=attempts,
                )
                gemini_prov = f"Live ({gemini_summary.model_identifier}, {gemini_summary.duration_ms / 1000.0:.2f}s)"
                gemini_candidates_list = [c.model_dump() if hasattr(c, "model_dump") else asdict(c) for c in gemini_result.all_candidates]
                gemini_inv_list = gemini_inv_dicts
                gemini_provenance_label = "live_api"
        except Exception as ex:
            gemini_dur = (time.perf_counter() - gemini_start) * 1000.0
            gemini_summary = summarize_model_evaluation(
                model_identifier=gemini_extractor.model_name,
                provider="gemini",
                field_inventory_items=[],
                duration_ms=None,
                ocr_duration_ms=qwen_ocr_dur,
                counts={},
                is_executed=False,
                execution_status="Failed",
                failure_reason=str(ex),
                failed_duration_ms=gemini_dur,
                request_dispatched=True,
            )
            gemini_prov = f"Failed ({gemini_summary.failure_reason})"
            gemini_provenance_label = "live_api_exception"
            if hasattr(gemini_extractor.client, "last_raw_http_response_text") and gemini_extractor.client.last_raw_http_response_text:
                gemini_raw_response_text = gemini_extractor.client.last_raw_http_response_text
    else:
        # Gemini API Key missing or rejected -> mark cleanly as NotExecuted with null scores/timings
        if has_ctrl_chars:
            fail_reason = "GEMINI_API_KEY contains invalid control characters; rejected before request"
            gemini_prov = "NotExecuted (Corrupted GEMINI_API_KEY with control characters)"
            gemini_provenance_label = "invalid_credentials_control_characters"
        else:
            fail_reason = "GEMINI_API_KEY unconfigured; infrastructure blocked"
            gemini_prov = "NotExecuted (Missing GEMINI_API_KEY; infrastructure blocked)"
            gemini_provenance_label = "unconfigured_infrastructure_blocked"

        gemini_summary = summarize_model_evaluation(
            model_identifier="gemini-3.8-flash",
            provider="gemini",
            field_inventory_items=[],
            duration_ms=None,
            ocr_duration_ms=qwen_ocr_dur,
            counts={},
            is_executed=False,
            execution_status="NotExecuted",
            failure_reason=fail_reason,
            request_dispatched=False,
            attempts_made=0,
        )

    table_md = format_comparison_markdown_table(qwen_summary, gemini_summary)
    audit_md = format_candidate_audit_markdown_table(qwen_summary)

    res = {
        "qwen_summary": asdict(qwen_summary) if qwen_summary else None,
        "gemini_summary": asdict(gemini_summary) if gemini_summary else None,
        "comparison_table_markdown": table_md,
        "candidate_audit_markdown": audit_md,
        "gemini_key_configured": bool(resolved_gemini_key and resolved_gemini_key.strip()),
        "gemini_key_source": gemini_key_source,
        "ocr_input_sha256": ocr_hash,
        "ocr_page_count": len(pages),
        "timing_provenance": {
            "qwen": "Historical (Full 18-page Proposal1 run, scratch/proposal1_full_result_v4.json)",
            "gemini": gemini_prov,
        },
    }

    # Persist timestamped artifacts in dedicated run directory
    run_timestamp = datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")
    out_dir = Path("evaluation/semantic/outputs")
    out_dir.mkdir(parents=True, exist_ok=True)

    run_dir = save_timestamped_run_artifacts(
        out_dir=out_dir,
        timestamp_str=run_timestamp,
        ocr_hash=ocr_hash,
        pages_count=len(pages),
        qwen_summary=qwen_summary,
        gemini_summary=gemini_summary,
        gemini_raw_response_text=gemini_raw_response_text,
        gemini_candidates=gemini_candidates_list,
        gemini_inventory=gemini_inv_list,
        qwen_candidates=qwen_all_candidates,
        qwen_inventory=qwen_inventory,
        gemini_configured_model_id=configured_model_id,
        gemini_key_configured=bool(resolved_gemini_key and resolved_gemini_key.strip()),
        gemini_provenance_label=gemini_provenance_label,
        timing_provenance=res["timing_provenance"],
        gemini_key_source=gemini_key_source,
    )

    res["run_artifacts_dir"] = str(run_dir)

    # Persist root comparison_result.json and gemini_raw_response.json for backward compatibility
    with open(out_dir / "comparison_result.json", "w", encoding="utf-8") as f:
        json.dump(res, f, indent=2, ensure_ascii=False)

    if gemini_raw_response_text and gemini_raw_response_text.strip():
        (out_dir / "gemini_raw_response.json").write_text(gemini_raw_response_text, encoding="utf-8")

    return res


def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description="A/B Semantic Comparison Runner: Qwen vs Gemini")
    parser.add_argument("--cached-json", type=str, default=None, help="Path to cached Proposal1 JSON")
    parser.add_argument("--gemini-response", type=str, default=None, help="Path to saved Gemini response JSON for offline evaluation")
    parser.add_argument("--api-key", type=str, default=None, help="Optional Gemini API key")
    args = parser.parse_args()

    res = run_ab_comparison(
        cached_path=args.cached_json,
        gemini_api_key=args.api_key,
        gemini_response_path=args.gemini_response,
    )
    print("\n" + "=" * 90)
    print("CONTROLLED SEMANTIC-MODEL COMPARISON: PROPOSAL1.PDF")
    print(f"OCR Transcript SHA-256: {res['ocr_input_sha256']} ({res['ocr_page_count']} pages)")
    print("=" * 90 + "\n")
    print(res["comparison_table_markdown"])
    print("\n" + "=" * 90)
    print("CANDIDATE-LEVEL AUDIT: SEPARATING SEMANTIC RELEVANCE FROM GOVERNANCE REVIEW STATUS")
    print("=" * 90 + "\n")
    print(res["candidate_audit_markdown"])
    print("\n" + "=" * 90)
    q = res["qwen_summary"]
    print(f"Qwen Status: {q['execution_status']}")
    print(f"Qwen Strict 17-Field Accuracy: {q['score_17'] * 100:.1f}% ({q['correct_count']} Correct, {q['correct_missing_count']} Correct Missing)")
    print(f"Qwen Weighted 17-Field Score:    {q['score_17_weighted'] * 100:.1f}% (+ {q['partially_correct_count']} Partial)")
    print(f"Qwen Expected-Present Fields:   {q['present_accuracy'] * 100:.1f}% ({q['expected_present_correct']}/{q['expected_present_total']})")
    print(f"Qwen Expected-Missing Fields:   {q['missing_accuracy'] * 100:.1f}% ({q['expected_missing_correct']}/{q['expected_missing_total']})")
    print(f"Qwen Strict 9-Critical Score:   {q['critical_score'] * 100:.1f}% ({q['critical_correct'] + q['critical_correct_missing']}/{q['critical_fields_count']})")
    print(f"Qwen Weighted 9-Critical Score: {q['critical_score_weighted'] * 100:.1f}% (+ {q['critical_partially_correct']} Partial)")
    print(f"Qwen Raw Candidate Recall:      {q['candidate_semantic_recall'] * 100:.1f}% (before conservative conflict escalation)")
    print(f"Qwen Semantic Duration:         {q['duration_ms'] / 1000.0:.1f}s ({res['timing_provenance']['qwen']})")
    print("-" * 90)
    g = res["gemini_summary"]
    if g["execution_status"] == "Executed":
        print(f"Gemini Status: {g['execution_status']} ({res['timing_provenance']['gemini']})")
        print(f"Gemini Strict 17-Field Accuracy: {g['score_17'] * 100:.1f}% ({g['correct_count']} Correct, {g['correct_missing_count']} Correct Missing)")
        print(f"Gemini Weighted 17-Field Score:    {g['score_17_weighted'] * 100:.1f}% (+ {g['partially_correct_count']} Partial)")
        print(f"Gemini Expected-Present Fields:   {g['present_accuracy'] * 100:.1f}% ({g['expected_present_correct']}/{g['expected_present_total']})")
        print(f"Gemini Expected-Missing Fields:   {g['missing_accuracy'] * 100:.1f}% ({g['expected_missing_correct']}/{g['expected_missing_total']})")
        print(f"Gemini Strict 9-Critical Score:   {g['critical_score'] * 100:.1f}% ({g['critical_correct'] + g['critical_correct_missing']}/{g['critical_fields_count']})")
        print(f"Gemini Weighted 9-Critical Score: {g['critical_score_weighted'] * 100:.1f}% (+ {g['critical_partially_correct']} Partial)")
        print(f"Gemini Raw Candidate Recall:      {g['candidate_semantic_recall'] * 100:.1f}%")
        wall_sec = g['duration_ms'] / 1000.0 if g.get('duration_ms') else 0.0
        succ_sec = g.get('successful_attempt_duration_ms', 0) / 1000.0 if g.get('successful_attempt_duration_ms') else None
        back_sec = g.get('retry_backoff_duration_ms', 0) / 1000.0 if g.get('retry_backoff_duration_ms') else None
        timing_str = f"{wall_sec:.1f}s total wall time"
        if succ_sec:
            timing_str += f" ({succ_sec:.1f}s successful attempt, {back_sec or 0.0:.1f}s backoff across {g.get('attempts_made', 1)} attempt(s))"
        print(f"Gemini Semantic Duration:         {timing_str}")
    elif g["execution_status"] == "Failed":
        fail_dur_str = f" in {g.get('failed_duration_ms', 0) / 1000.0:.2f}s" if g.get("failed_duration_ms") else ""
        print(f"Gemini Status: Failed (Request dispatched: {str(g.get('request_dispatched', True)).lower()}; Failure: {g.get('failure_reason')}{fail_dur_str})")
        print("Gemini 17-Field Score: null / N/A (Failed run; scoring skipped)")
        print("Gemini 9-Critical Score: null / N/A (Failed run; scoring skipped)")
        fail_elapsed = f"{g.get('failed_duration_ms', 0) / 1000.0:.2f}s" if g.get("failed_duration_ms") else "N/A"
        print(f"Gemini Semantic Latency: null / N/A (Failed elapsed: {fail_elapsed})")
    else:
        print(f"Gemini Status: NotExecuted (GEMINI_API_KEY unconfigured; infrastructure blocked)")
        print("Gemini 17-Field Score: null / N/A")
        print("Gemini 9-Critical Score: null / N/A")
        print("Gemini Semantic Duration: null / N/A")
    print("-" * 90)
    print(f"Artifacts Saved To:       {res.get('run_artifacts_dir', 'evaluation/semantic/outputs')}")
    print("=" * 90 + "\n")


if __name__ == "__main__":
    main()
