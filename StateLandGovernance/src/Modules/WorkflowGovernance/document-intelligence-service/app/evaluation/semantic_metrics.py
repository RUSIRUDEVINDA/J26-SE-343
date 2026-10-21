"""Semantic evaluation metrics: Classification Macro-F1, Critical-Field Accuracy, and Evidence-Span Precision."""

from collections import Counter, defaultdict
from dataclasses import dataclass
from typing import Any, Dict, List, Optional, Set, Tuple


def calculate_classification_metrics(
    ground_truths: List[str],
    predictions: List[str],
) -> Dict[str, Any]:
    """
    Compute multiclass classification precision, recall, F1 per class, and Macro-F1.
    Does not use external heavy libraries.
    """
    if len(ground_truths) != len(predictions):
        raise ValueError("Length of ground_truths and predictions must match.")

    classes = sorted(list(set(ground_truths) | set(predictions)))
    if not classes:
        return {"macro_f1": 0.0, "classes": {}}

    confusion: Dict[str, Dict[str, int]] = {c: {c2: 0 for c2 in classes} for c in classes}
    for gt, pred in zip(ground_truths, predictions):
        confusion[gt][pred] += 1

    per_class_metrics: Dict[str, Dict[str, float]] = {}
    f1_sum = 0.0

    for c in classes:
        tp = confusion[c][c]
        fp = sum(confusion[other][c] for other in classes if other != c)
        fn = sum(confusion[c][other] for other in classes if other != c)

        precision = tp / (tp + fp) if (tp + fp) > 0 else 0.0
        recall = tp / (tp + fn) if (tp + fn) > 0 else 0.0
        f1 = (2 * precision * recall) / (precision + recall) if (precision + recall) > 0 else 0.0

        per_class_metrics[c] = {
            "precision": round(precision, 4),
            "recall": round(recall, 4),
            "f1": round(f1, 4),
            "support": sum(confusion[c].values()),
        }
        f1_sum += f1

    macro_f1 = f1_sum / len(classes)

    return {
        "macro_f1": round(macro_f1, 4),
        "per_class": per_class_metrics,
        "confusion_matrix": confusion,
    }


@dataclass
class FieldEvaluationDetail:
    """Outcome of evaluating an individual field extraction."""

    status: str  # "correct", "wrong_value", "missed_field", "hallucinated_field"
    expected: Optional[str] = None
    predicted: Optional[str] = None


def normalize_value(val: Optional[str]) -> str:
    """Normalize extracted or ground-truth value string for robust comparison."""
    if not val:
        return ""
    import unicodedata
    n = unicodedata.normalize("NFC", val.strip().lower())
    # Collapse multiple whitespace
    return " ".join(n.split())


def evaluate_critical_field_extraction(
    ground_truth_fields: Dict[str, str],
    extracted_fields: Dict[str, str],
    critical_field_set: Optional[Set[str]] = None,
) -> Dict[str, Any]:
    """
    Evaluate candidate field extraction against ground truth.
    Strictly categorizes into 4 error classes:
    - correct: predicted matches ground truth.
    - wrong_value: field exists in both, but values differ.
    - missed_field: field present in ground truth, but missing in extraction.
    - hallucinated_field: field extracted by model, but absent in ground truth.
    """
    all_field_codes = set(ground_truth_fields.keys()) | set(extracted_fields.keys())
    if critical_field_set is not None:
        all_field_codes = all_field_codes & critical_field_set

    counts = {
        "correct": 0,
        "wrong_value": 0,
        "missed_field": 0,
        "hallucinated_field": 0,
    }
    field_details: Dict[str, Dict[str, Any]] = {}

    for code in sorted(all_field_codes):
        in_gt = code in ground_truth_fields and bool(ground_truth_fields[code].strip())
        in_ext = code in extracted_fields and bool(extracted_fields[code].strip())

        if in_gt and in_ext:
            gt_norm = normalize_value(ground_truth_fields[code])
            ext_norm = normalize_value(extracted_fields[code])

            # Exact or containment match for conservative comparison
            if gt_norm == ext_norm or gt_norm in ext_norm or ext_norm in gt_norm:
                status = "correct"
            else:
                status = "wrong_value"
        elif in_gt and not in_ext:
            status = "missed_field"
        elif not in_gt and in_ext:
            status = "hallucinated_field"
        else:
            continue

        counts[status] += 1
        field_details[code] = {
            "status": status,
            "expected": ground_truth_fields.get(code),
            "predicted": extracted_fields.get(code),
        }

    total_gt_fields = counts["correct"] + counts["wrong_value"] + counts["missed_field"]
    accuracy = (counts["correct"] / total_gt_fields) if total_gt_fields > 0 else 0.0

    return {
        "critical_field_accuracy": round(accuracy, 4),
        "counts": counts,
        "total_ground_truth_fields": total_gt_fields,
        "field_details": field_details,
    }


def evaluate_evidence_span_precision(
    ground_truth_evidence_quotes: List[str],
    extracted_evidence_quotes: List[str],
) -> Dict[str, Any]:
    """
    Evaluate deterministic evidence span precision.
    A candidate evidence quote is counted as correct if it has substring or significant token overlap
    with a manually labelled ground-truth evidence quote.
    """
    if not extracted_evidence_quotes:
        return {
            "evidence_span_precision": 0.0 if ground_truth_evidence_quotes else 1.0,
            "matched_spans": 0,
            "total_extracted_spans": 0,
            "total_ground_truth_spans": len(ground_truth_evidence_quotes),
        }

    norm_gt = [normalize_value(q) for q in ground_truth_evidence_quotes if q and q.strip()]
    matched = 0

    for ext_q in extracted_evidence_quotes:
        norm_ext = normalize_value(ext_q)
        if not norm_ext:
            continue

        is_match = False
        for gt in norm_gt:
            # Overlap criteria: substring or token overlap > 0.5
            if norm_ext in gt or gt in norm_ext:
                is_match = True
                break
            ext_tokens = set(norm_ext.split())
            gt_tokens = set(gt.split())
            if ext_tokens and gt_tokens:
                intersection = ext_tokens & gt_tokens
                overlap = len(intersection) / min(len(ext_tokens), len(gt_tokens))
                if overlap >= 0.6:
                    is_match = True
                    break

        if is_match:
            matched += 1

    precision = matched / len(extracted_evidence_quotes) if extracted_evidence_quotes else 0.0
    return {
        "evidence_span_precision": round(precision, 4),
        "matched_spans": matched,
        "total_extracted_spans": len(extracted_evidence_quotes),
        "total_ground_truth_spans": len(ground_truth_evidence_quotes),
    }

