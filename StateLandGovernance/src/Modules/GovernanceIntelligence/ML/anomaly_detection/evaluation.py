"""One-time held-out synthetic test evaluation for the frozen model."""

from __future__ import annotations

import csv
import json
from pathlib import Path
from typing import Any, Sequence

import numpy as np

from .baseline import (
    BaselineError,
    anomaly_scores,
    classification_metrics,
    flags_from_scores,
    load_case_keys,
    load_feature_matrix,
    load_validation_labels,
)
from .bundle import (
    DEFAULT_BUNDLE_DIRECTORY,
    DEFAULT_DATASET_DIRECTORY,
    DEFAULT_EVALUATION_DIRECTORY,
    DEFAULT_RUN_DIRECTORY,
    VerifiedFrozenRun,
    create_versioned_bundle,
    sha256_file,
    verify_frozen_run,
)


TEST_PERTURBATION_TYPES = (
    "extended_gap",
    "repeated_correction_loop",
    "excessive_handoffs",
    "increased_event_count",
    "duplicated_sequence",
)


def _write_json(path: Path, content: dict[str, Any]) -> None:
    path.write_text(json.dumps(content, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def _write_test_predictions(
    path: Path,
    keys: Sequence[str],
    labels: Sequence[dict[str, str]],
    scores: np.ndarray,
    flags: np.ndarray,
    threshold: float,
) -> None:
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(
            handle,
            fieldnames=(
                "case_id",
                "synthetic_anomaly_label",
                "perturbation_type",
                "anomaly_score",
                "threshold",
                "flagged",
            ),
        )
        writer.writeheader()
        for case_id, label, score, flagged in zip(keys, labels, scores, flags):
            writer.writerow(
                {
                    "case_id": case_id,
                    "synthetic_anomaly_label": label["synthetic_anomaly_label"],
                    "perturbation_type": label["perturbation_type"],
                    "anomaly_score": repr(float(score)),
                    "threshold": repr(float(threshold)),
                    "flagged": str(bool(flagged)),
                }
            )


def perturbation_detection_counts(
    labels: np.ndarray, flags: np.ndarray, perturbation_types: Sequence[str]
) -> dict[str, dict[str, Any]]:
    """Report actual synthetic type denominators, including zero-coverage types."""

    result: dict[str, dict[str, Any]] = {}
    for perturbation_type in TEST_PERTURBATION_TYPES:
        mask = np.asarray([value == perturbation_type for value in perturbation_types], dtype=bool)
        support = int(np.sum(mask))
        if support == 0:
            result[perturbation_type] = {
                "synthetic_positive_cases": 0,
                "flagged_cases": 0,
                "detected_cases": 0,
                "missed_cases": 0,
                "detection_rate": None,
                "undefined_metrics": {"detection_rate": "zero_test_examples"},
            }
            continue
        expected_positive = labels[mask]
        type_flags = flags[mask]
        if not np.all(expected_positive == 1):
            raise BaselineError(f"test perturbation type {perturbation_type} contains non-positive labels")
        detected = int(np.sum(type_flags))
        result[perturbation_type] = {
            "synthetic_positive_cases": support,
            "flagged_cases": detected,
            "detected_cases": detected,
            "missed_cases": support - detected,
            "detection_rate": detected / support,
            "undefined_metrics": {},
        }
    return result


def evaluate_reserved_test_once(
    dataset_directory: str | Path = DEFAULT_DATASET_DIRECTORY,
    run_directory: str | Path = DEFAULT_RUN_DIRECTORY,
    evaluation_directory: str | Path = DEFAULT_EVALUATION_DIRECTORY,
    bundle_directory: str | Path = DEFAULT_BUNDLE_DIRECTORY,
) -> dict[str, Any]:
    """Score the reserved test split once without any fitting, tuning, or threshold changes."""

    output = Path(evaluation_directory)
    bundle = Path(bundle_directory)
    if output.exists():
        raise FileExistsError(f"Refusing to overwrite held-out evaluation output: {output}")
    if bundle.exists():
        raise FileExistsError(f"Refusing to overwrite model bundle output: {bundle}")
    frozen = verify_frozen_run(dataset_directory, run_directory)
    dataset = frozen.dataset_directory

    matrix, _ = load_feature_matrix(dataset / "splits" / "X_test.csv")
    keys = load_case_keys(dataset / "splits" / "keys_test.csv")
    labels_rows = load_validation_labels(dataset / "splits" / "y_test.csv")
    if len(matrix) != 108 or len(keys) != 108 or len(labels_rows) != 108:
        raise BaselineError("held-out synthetic test split must contain exactly 108 aligned rows")
    if [row["case_id"] for row in labels_rows] != keys:
        raise BaselineError("test labels do not align with test keys")
    labels = np.asarray([int(row["synthetic_anomaly_label"]) for row in labels_rows], dtype=int)
    if int(np.sum(labels == 0)) != 80 or int(np.sum(labels == 1)) != 28:
        raise BaselineError("held-out test split must contain 80 reference and 28 perturbation cases")

    scores = anomaly_scores(frozen.model, matrix)
    flags = flags_from_scores(scores, frozen.threshold)
    reference_scores = scores[labels == 0]
    reference_flags = flags[labels == 0]
    types = [row["perturbation_type"] for row in labels_rows]
    reference_application = {
        "reference_case_count": int(len(reference_scores)),
        "flagged_reference_cases": int(np.sum(reference_flags)),
        "reference_case_flag_rate": float(np.mean(reference_flags)),
        "scores_less_than_threshold": int(np.sum(reference_scores < frozen.threshold)),
        "scores_equal_to_threshold": int(np.sum(reference_scores == frozen.threshold)),
        "scores_greater_than_threshold": int(np.sum(reference_scores > frozen.threshold)),
    }
    metrics: dict[str, Any] = {
        "metric_label": "synthetic held-out evaluation",
        "evaluation_scope": "reserved synthetic test split scored once after frozen model and threshold selection",
        "test_population": {
            "total_cases": 108,
            "synthetic_reference_cases": 80,
            "controlled_perturbation_cases": 28,
        },
        "score_direction": frozen.threshold_record["score_direction"],
        "threshold": frozen.threshold,
        "strict_flag_rule": "anomaly_score > threshold",
        "overall_synthetic_held_out_metrics": classification_metrics(labels, flags),
        "reference_case_threshold_application": reference_application,
        "per_perturbation_detection_counts": perturbation_detection_counts(labels, flags, types),
        "limitations": [
            "All metrics are synthetic held-out evaluation results, not real-world corruption-detection accuracy.",
            "Reference-normal is generator-designated, not independently verified normality.",
            "repeated_correction_loop has zero test examples; its test detection rate is undefined.",
            "excessive_handoffs has only two test examples; this is insufficient for reliable per-type claims.",
            "No tuning, refitting, threshold adjustment, or split reassignment occurred after observing this test result.",
        ],
    }

    output.mkdir(parents=True, exist_ok=False)
    _write_test_predictions(output / "test_predictions.csv", keys, labels_rows, scores, flags, frozen.threshold)
    _write_json(output / "test_metrics.json", metrics)
    evaluation_metadata = {
        "metric_label": "synthetic held-out evaluation",
        "model_sha256": frozen.metadata["model_sha256"],
        "feature_order": frozen.metadata["feature_order"],
        "threshold": frozen.threshold,
        "score_direction": frozen.threshold_record["score_direction"],
        "strict_flag_rule": "anomaly_score > threshold",
        "input_sha256": {
            "X_test": sha256_file(dataset / "splits" / "X_test.csv"),
            "keys_test": sha256_file(dataset / "splits" / "keys_test.csv"),
            "y_test": sha256_file(dataset / "splits" / "y_test.csv"),
            "split_manifest": sha256_file(dataset / "splits" / "split_manifest.csv"),
        },
        "split_manifest_sha256": sha256_file(dataset / "splits" / "split_manifest.csv"),
        "fit_performed": False,
        "test_scored_once": True,
    }
    _write_json(output / "evaluation_metadata.json", evaluation_metadata)
    created_bundle = create_versioned_bundle(frozen, output, bundle)
    return {
        "status": "PASS",
        "metric_label": "synthetic held-out evaluation",
        "evaluation_directory": str(output.resolve()),
        "bundle_directory": str(created_bundle),
        "test_metrics": metrics,
    }
