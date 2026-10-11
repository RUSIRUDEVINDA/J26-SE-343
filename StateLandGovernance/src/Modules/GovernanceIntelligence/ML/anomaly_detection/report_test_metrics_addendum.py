"""Recompute metrics from frozen held-out predictions without fitting or rescoring."""

from __future__ import annotations

import argparse
import csv
from dataclasses import dataclass
from datetime import datetime, timezone
import hashlib
import json
import math
from pathlib import Path
from typing import Any, Iterable, Mapping, Sequence


ANOMALY_ROOT = Path(__file__).resolve().parent
DEFAULT_EVALUATION_DIRECTORY = ANOMALY_ROOT / "evaluations" / "synthetic_held_out_test_v1"
DEFAULT_DATASET_DIRECTORY = ANOMALY_ROOT / "data" / "synthetic_workflow_v1"
DEFAULT_BUNDLE_MANIFEST = ANOMALY_ROOT / "bundles" / "workflow_anomaly_model_v1" / "bundle_manifest.json"
DEFAULT_OUTPUT = DEFAULT_EVALUATION_DIRECTORY / "classification_metrics_addendum.json"
EXPECTED_COLUMNS = (
    "case_id", "synthetic_anomaly_label", "perturbation_type",
    "anomaly_score", "threshold", "flagged",
)
PERTURBATION_TYPES = (
    "extended_gap", "repeated_correction_loop", "excessive_handoffs",
    "increased_event_count", "duplicated_sequence",
)
WILSON_Z_95 = 1.959963984540054
FLOAT_TOLERANCE = 1e-12


class MetricsAddendumError(ValueError):
    """Raised when frozen prediction evidence is malformed or inconsistent."""


@dataclass(frozen=True, slots=True)
class PredictionRow:
    case_id: str
    label: int
    perturbation_type: str
    score: float
    threshold: float
    flagged: bool


def sha256_file(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _load_json_object(path: Path, *, description: str) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        raise MetricsAddendumError(f"Cannot read {description}: {exc}") from exc
    if not isinstance(value, dict):
        raise MetricsAddendumError(f"{description} must be a JSON object")
    return value


def read_and_validate_predictions(path: Path, *, expected_threshold: float) -> list[PredictionRow]:
    """Read immutable predictions and enforce their frozen binary contract."""
    try:
        with path.open("r", encoding="utf-8", newline="") as handle:
            reader = csv.DictReader(handle)
            if tuple(reader.fieldnames or ()) != EXPECTED_COLUMNS:
                raise MetricsAddendumError(
                    "Frozen predictions must contain exactly the expected columns in order"
                )
            raw_rows = list(reader)
    except OSError as exc:
        raise MetricsAddendumError(f"Cannot read frozen predictions: {exc}") from exc
    if not raw_rows:
        raise MetricsAddendumError("Frozen predictions must not be empty")

    seen_ids: set[str] = set()
    rows: list[PredictionRow] = []
    for row_number, raw in enumerate(raw_rows, start=2):
        case_id = raw["case_id"]
        if not case_id or not case_id.strip():
            raise MetricsAddendumError(f"Blank case_id at prediction row {row_number}")
        if case_id in seen_ids:
            raise MetricsAddendumError(f"Duplicate case_id in frozen predictions: {case_id}")
        seen_ids.add(case_id)
        label_text = raw["synthetic_anomaly_label"]
        if label_text not in {"0", "1"}:
            raise MetricsAddendumError(f"Non-binary synthetic label at prediction row {row_number}")
        label = int(label_text)
        flagged_text = raw["flagged"]
        if flagged_text not in {"True", "False"}:
            raise MetricsAddendumError(f"Invalid Boolean flag at prediction row {row_number}")
        flagged = flagged_text == "True"
        try:
            score = float(raw["anomaly_score"])
            threshold = float(raw["threshold"])
        except ValueError as exc:
            raise MetricsAddendumError(
                f"Non-numeric score or threshold at prediction row {row_number}"
            ) from exc
        if not math.isfinite(score) or not math.isfinite(threshold):
            raise MetricsAddendumError(f"Non-finite score or threshold at prediction row {row_number}")
        if threshold != expected_threshold:
            raise MetricsAddendumError(f"Frozen threshold mismatch at prediction row {row_number}")
        if flagged != (score > threshold):
            raise MetricsAddendumError(
                f"Flag does not apply the strict score > threshold rule at row {row_number}"
            )
        perturbation_type = raw["perturbation_type"]
        if label == 1 and perturbation_type not in PERTURBATION_TYPES:
            raise MetricsAddendumError(f"Unknown positive perturbation type at prediction row {row_number}")
        rows.append(PredictionRow(case_id, label, perturbation_type, score, threshold, flagged))
    return rows


def wilson_interval(successes: int, total: int, *, z: float = WILSON_Z_95) -> dict[str, float]:
    if total <= 0 or successes < 0 or successes > total:
        raise MetricsAddendumError("Wilson interval requires 0 <= successes <= total and total > 0")
    proportion = successes / total
    denominator = 1.0 + z * z / total
    centre = (proportion + z * z / (2.0 * total)) / denominator
    half_width = (
        z
        * math.sqrt(proportion * (1.0 - proportion) / total + z * z / (4.0 * total * total))
        / denominator
    )
    return {
        "confidence_level": 0.95,
        "method": "Wilson score interval",
        "lower": centre - half_width,
        "upper": centre + half_width,
    }


def _divide(numerator: int | float, denominator: int | float) -> float:
    if denominator == 0:
        raise MetricsAddendumError("Metric denominator must not be zero")
    return numerator / denominator


def compute_metrics(rows: Sequence[PredictionRow]) -> dict[str, Any]:
    if not rows:
        raise MetricsAddendumError("Cannot compute metrics for an empty population")
    tp = sum(row.label == 1 and row.flagged for row in rows)
    fp = sum(row.label == 0 and row.flagged for row in rows)
    tn = sum(row.label == 0 and not row.flagged for row in rows)
    fn = sum(row.label == 1 and not row.flagged for row in rows)
    support = len(rows)
    reference_support = tn + fp
    perturbation_support = tp + fn
    anomaly_precision = _divide(tp, tp + fp)
    anomaly_recall = _divide(tp, tp + fn)
    anomaly_f1 = _divide(2 * tp, 2 * tp + fp + fn)
    reference_precision = _divide(tn, tn + fn)
    specificity = _divide(tn, tn + fp)
    reference_f1 = _divide(2 * tn, 2 * tn + fp + fn)
    accuracy = _divide(tp + tn, support)
    balanced_accuracy = (anomaly_recall + specificity) / 2.0
    macro_precision = (anomaly_precision + reference_precision) / 2.0
    macro_f1 = (anomaly_f1 + reference_f1) / 2.0
    weighted_precision = (
        reference_precision * reference_support + anomaly_precision * perturbation_support
    ) / support
    weighted_recall = (
        specificity * reference_support + anomaly_recall * perturbation_support
    ) / support
    weighted_f1 = (
        reference_f1 * reference_support + anomaly_f1 * perturbation_support
    ) / support
    baseline_accuracy = reference_support / support
    per_perturbation: dict[str, dict[str, int]] = {}
    for perturbation_type in PERTURBATION_TYPES:
        type_rows = [
            row for row in rows
            if row.label == 1 and row.perturbation_type == perturbation_type
        ]
        detected = sum(row.flagged for row in type_rows)
        per_perturbation[perturbation_type] = {
            "detected": detected,
            "missed": len(type_rows) - detected,
            "total": len(type_rows),
        }
    return {
        "support": support,
        "reference_support": reference_support,
        "perturbation_support": perturbation_support,
        "confusion_counts": {
            "true_positive": tp, "false_positive": fp,
            "true_negative": tn, "false_negative": fn,
        },
        "accuracy": accuracy,
        "balanced_accuracy": balanced_accuracy,
        "macro_precision": macro_precision,
        "macro_recall": balanced_accuracy,
        "macro_f1": macro_f1,
        "weighted_precision": weighted_precision,
        "weighted_recall": weighted_recall,
        "weighted_f1": weighted_f1,
        "anomaly_class": {
            "precision": anomaly_precision, "recall": anomaly_recall, "f1": anomaly_f1,
        },
        "specificity": specificity,
        "reference_flag_rate": _divide(fp, reference_support),
        "always_normal_baseline_accuracy": baseline_accuracy,
        "accuracy_improvement_over_always_normal": accuracy - baseline_accuracy,
        "accuracy_wilson_95_interval": wilson_interval(tp + tn, support),
        "per_perturbation_counts": per_perturbation,
    }


def verify_manifest_artifacts(manifest_path: Path) -> tuple[dict[str, Any], dict[str, str]]:
    manifest = _load_json_object(manifest_path, description="bundle manifest")
    artifacts = manifest.get("artifacts")
    if not isinstance(artifacts, dict) or "test_predictions" not in artifacts:
        raise MetricsAddendumError("Bundle manifest lacks frozen prediction evidence")
    verified: dict[str, str] = {}
    bundle_directory = manifest_path.resolve().parent
    for name, raw_entry in artifacts.items():
        if not isinstance(raw_entry, dict):
            raise MetricsAddendumError(f"Malformed bundle artifact entry: {name}")
        relative_path = raw_entry.get("path")
        expected_hash = raw_entry.get("sha256")
        if not isinstance(relative_path, str) or not relative_path:
            raise MetricsAddendumError(f"Missing bundle path for artifact: {name}")
        if not isinstance(expected_hash, str) or len(expected_hash) != 64:
            raise MetricsAddendumError(f"Malformed bundle hash for artifact: {name}")
        artifact_path = (bundle_directory / relative_path).resolve()
        if not artifact_path.is_relative_to(ANOMALY_ROOT.resolve()):
            raise MetricsAddendumError(f"Bundle artifact leaves the trusted anomaly root: {name}")
        if not artifact_path.is_file():
            raise MetricsAddendumError(f"Bundle artifact is missing: {name}")
        actual_hash = sha256_file(artifact_path)
        if actual_hash != expected_hash:
            raise MetricsAddendumError(f"Bundle manifest hash mismatch for artifact: {name}")
        verified[name] = actual_hash
    return manifest, verified


def _read_case_ids(path: Path) -> list[str]:
    try:
        with path.open("r", encoding="utf-8", newline="") as handle:
            reader = csv.DictReader(handle)
            if reader.fieldnames != ["case_id"]:
                raise MetricsAddendumError(f"Unexpected key-file schema: {path.name}")
            values = [row["case_id"] for row in reader]
    except OSError as exc:
        raise MetricsAddendumError(f"Cannot read split keys: {path.name}") from exc
    if any(not value or not value.strip() for value in values) or len(values) != len(set(values)):
        raise MetricsAddendumError(f"Split keys are blank or duplicated: {path.name}")
    return values


def verify_split_isolation(rows: Sequence[PredictionRow], dataset_directory: Path) -> dict[str, int]:
    split_directory = dataset_directory / "splits"
    train_ids = set(_read_case_ids(split_directory / "keys_train.csv"))
    validation_ids = set(_read_case_ids(split_directory / "keys_validation.csv"))
    test_ids = _read_case_ids(split_directory / "keys_test.csv")
    prediction_ids = [row.case_id for row in rows]
    if prediction_ids != test_ids:
        raise MetricsAddendumError("Frozen prediction IDs do not exactly align with keys_test.csv")
    if set(prediction_ids) & (train_ids | validation_ids):
        raise MetricsAddendumError("Frozen test predictions overlap training or validation IDs")
    return {
        "training_overlap": 0,
        "validation_overlap": 0,
        "test_prediction_ids_matching_keys_test": len(test_ids),
    }


def verify_evaluation_input_hashes(
    evaluation_metadata: Mapping[str, Any], dataset_directory: Path
) -> dict[str, str]:
    recorded = evaluation_metadata.get("input_sha256")
    if not isinstance(recorded, dict):
        raise MetricsAddendumError("Evaluation metadata lacks frozen input hashes")
    split_directory = dataset_directory / "splits"
    paths = {
        "X_test": split_directory / "X_test.csv",
        "keys_test": split_directory / "keys_test.csv",
        "y_test": split_directory / "y_test.csv",
        "split_manifest": split_directory / "split_manifest.csv",
    }
    actual: dict[str, str] = {}
    for name, path in paths.items():
        actual[name] = sha256_file(path)
        if recorded.get(name) != actual[name]:
            raise MetricsAddendumError(f"Evaluation input hash mismatch: {name}")
    return actual


def verify_existing_metrics(
    metrics: Mapping[str, Any],
    existing_path: Path,
    rows: Sequence[PredictionRow],
    threshold: float,
) -> None:
    existing = _load_json_object(existing_path, description="existing test metrics")
    if existing.get("threshold") != threshold:
        raise MetricsAddendumError("Existing test threshold disagrees with recomputation")
    if existing.get("strict_flag_rule") != "anomaly_score > threshold":
        raise MetricsAddendumError("Existing test metrics do not preserve the strict flag rule")
    population = existing.get("test_population", {})
    if population != {
        "controlled_perturbation_cases": metrics["perturbation_support"],
        "synthetic_reference_cases": metrics["reference_support"],
        "total_cases": metrics["support"],
    }:
        raise MetricsAddendumError("Existing test population disagrees with recomputation")
    existing_overall = existing.get("overall_synthetic_held_out_metrics", {})
    counts = metrics["confusion_counts"]
    expected_values = {
        "support": metrics["support"],
        "true_positive": counts["true_positive"],
        "false_positive": counts["false_positive"],
        "true_negative": counts["true_negative"],
        "false_negative": counts["false_negative"],
        "precision": metrics["anomaly_class"]["precision"],
        "recall": metrics["anomaly_class"]["recall"],
        "f1": metrics["anomaly_class"]["f1"],
    }
    for name, expected in expected_values.items():
        actual = existing_overall.get(name)
        if isinstance(expected, float):
            if not isinstance(actual, (int, float)) or not math.isclose(
                float(actual), expected, rel_tol=0.0, abs_tol=FLOAT_TOLERANCE
            ):
                raise MetricsAddendumError(f"Existing test metric disagrees: {name}")
        elif actual != expected:
            raise MetricsAddendumError(f"Existing test metric disagrees: {name}")

    reference_rows = [row for row in rows if row.label == 0]
    expected_reference_application = {
        "reference_case_count": len(reference_rows),
        "flagged_reference_cases": sum(row.flagged for row in reference_rows),
        "reference_case_flag_rate": metrics["reference_flag_rate"],
        "scores_less_than_threshold": sum(row.score < threshold for row in reference_rows),
        "scores_equal_to_threshold": sum(row.score == threshold for row in reference_rows),
        "scores_greater_than_threshold": sum(row.score > threshold for row in reference_rows),
    }
    existing_reference = existing.get("reference_case_threshold_application", {})
    for name, expected in expected_reference_application.items():
        actual = existing_reference.get(name)
        if isinstance(expected, float):
            if not isinstance(actual, (int, float)) or not math.isclose(
                float(actual), expected, rel_tol=0.0, abs_tol=FLOAT_TOLERANCE
            ):
                raise MetricsAddendumError(
                    f"Existing reference threshold metric disagrees: {name}"
                )
        elif actual != expected:
            raise MetricsAddendumError(
                f"Existing reference threshold metric disagrees: {name}"
            )

    existing_perturbations = existing.get("per_perturbation_detection_counts", {})
    for perturbation_type, recomputed in metrics["per_perturbation_counts"].items():
        recorded = existing_perturbations.get(perturbation_type, {})
        expected_counts = {
            "synthetic_positive_cases": recomputed["total"],
            "flagged_cases": recomputed["detected"],
            "detected_cases": recomputed["detected"],
            "missed_cases": recomputed["missed"],
        }
        for name, expected in expected_counts.items():
            if recorded.get(name) != expected:
                raise MetricsAddendumError(
                    f"Existing perturbation metric disagrees: {perturbation_type}/{name}"
                )
        expected_rate = (
            recomputed["detected"] / recomputed["total"]
            if recomputed["total"]
            else None
        )
        actual_rate = recorded.get("detection_rate")
        if expected_rate is None:
            if actual_rate is not None:
                raise MetricsAddendumError(
                    f"Existing perturbation metric disagrees: {perturbation_type}/detection_rate"
                )
        elif not isinstance(actual_rate, (int, float)) or not math.isclose(
            float(actual_rate), expected_rate, rel_tol=0.0, abs_tol=FLOAT_TOLERANCE
        ):
            raise MetricsAddendumError(
                f"Existing perturbation metric disagrees: {perturbation_type}/detection_rate"
            )


def build_addendum(
    *,
    evaluation_directory: Path = DEFAULT_EVALUATION_DIRECTORY,
    dataset_directory: Path = DEFAULT_DATASET_DIRECTORY,
    bundle_manifest_path: Path = DEFAULT_BUNDLE_MANIFEST,
) -> dict[str, Any]:
    manifest, verified_bundle_hashes = verify_manifest_artifacts(bundle_manifest_path)
    threshold = manifest.get("threshold")
    if (
        not isinstance(threshold, (int, float))
        or isinstance(threshold, bool)
        or not math.isfinite(threshold)
    ):
        raise MetricsAddendumError("Bundle manifest threshold is missing or invalid")
    if manifest.get("strict_flag_rule") != "anomaly_score > threshold":
        raise MetricsAddendumError("Bundle manifest does not preserve the strict flag rule")
    predictions_path = evaluation_directory / "test_predictions.csv"
    rows = read_and_validate_predictions(predictions_path, expected_threshold=float(threshold))
    metrics = compute_metrics(rows)
    if (
        metrics["support"] != 108
        or metrics["reference_support"] != 80
        or metrics["perturbation_support"] != 28
    ):
        raise MetricsAddendumError("Frozen held-out population is not 108/80/28")
    metadata_path = evaluation_directory / "evaluation_metadata.json"
    metadata = _load_json_object(metadata_path, description="evaluation metadata")
    if metadata.get("threshold") != threshold:
        raise MetricsAddendumError("Evaluation metadata threshold disagrees with bundle")
    if metadata.get("fit_performed") is not False:
        raise MetricsAddendumError("Evaluation metadata does not prove that fitting was absent")
    verify_existing_metrics(
        metrics,
        evaluation_directory / "test_metrics.json",
        rows,
        float(threshold),
    )
    input_hashes = verify_evaluation_input_hashes(metadata, dataset_directory)
    isolation = verify_split_isolation(rows, dataset_directory)
    return {
        "artifact_type": "classification_metrics_addendum",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "evaluation_scope": "synthetic held-out evaluation only",
        "source_predictions_sha256": sha256_file(predictions_path),
        "bundle_manifest_sha256": sha256_file(bundle_manifest_path),
        "verified_bundle_artifact_sha256": verified_bundle_hashes,
        "verified_evaluation_input_sha256": input_hashes,
        "frozen_threshold": threshold,
        "strict_flag_rule": "anomaly_score > threshold",
        "threshold_ties": sum(row.score == row.threshold for row in rows),
        "threshold_adjustment_performed": False,
        "fit_or_scoring_performed": False,
        "split_isolation": isolation,
        "existing_test_metrics_agree": True,
        "metrics": metrics,
        "interpretation": {
            "reference_normal": "generator-designated reference; not independently verified normality",
            "anomaly_score": "not a probability or confidence value",
            "detector_scope": "not a corruption detector or legal-compliance detector",
            "real_world_accuracy": "not established for Sri Lankan departments or any real-world population",
        },
        "coverage_cautions": {
            "repeated_correction_loop": "zero test examples",
            "excessive_handoffs": "two test examples; insufficient for reliable per-type claims",
        },
        "limitations": [
            "Metrics describe only the frozen synthetic held-out prediction file.",
            "Reference-normal cases are generator-designated, not verified real normality.",
            "Anomaly score is not probability or confidence.",
            "No threshold adjustment, model fitting, rescoring, or prediction change occurred.",
            "This is not a corruption, fraud, wrongdoing, or legal-compliance detector.",
        ],
    }


def write_addendum(
    output_path: Path = DEFAULT_OUTPUT,
    *,
    evaluation_directory: Path = DEFAULT_EVALUATION_DIRECTORY,
    dataset_directory: Path = DEFAULT_DATASET_DIRECTORY,
    bundle_manifest_path: Path = DEFAULT_BUNDLE_MANIFEST,
) -> Path:
    if output_path.exists():
        raise FileExistsError(f"Refusing to overwrite metrics addendum: {output_path}")
    report = build_addendum(
        evaluation_directory=evaluation_directory,
        dataset_directory=dataset_directory,
        bundle_manifest_path=bundle_manifest_path,
    )
    output_path.parent.mkdir(parents=True, exist_ok=True)
    try:
        with output_path.open("x", encoding="utf-8", newline="\n") as handle:
            json.dump(report, handle, indent=2, sort_keys=True)
            handle.write("\n")
    except FileExistsError:
        raise FileExistsError(f"Refusing to overwrite metrics addendum: {output_path}") from None
    return output_path


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Recompute a non-overwriting metrics addendum from frozen predictions without fitting or rescoring."
    )
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    return parser


def main(argv: Iterable[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    try:
        output = write_addendum(args.output)
    except (MetricsAddendumError, FileExistsError, OSError) as exc:
        print(json.dumps({"status": "FAIL", "error": str(exc)}, indent=2))
        return 1
    print(json.dumps({"status": "PASS", "output": str(output.resolve())}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
