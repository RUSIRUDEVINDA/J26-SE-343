"""Frozen synthetic-only Isolation Forest baseline for workflow anomaly research.

This module intentionally trains only on the supplied reference training matrix
and scores only validation during milestone 2.  It is not a production model,
corruption detector, legal-compliance engine, or operational decision system.
"""

from __future__ import annotations

from collections import Counter
import csv
from datetime import UTC, datetime
import hashlib
import json
import math
from pathlib import Path
import platform
import sys
from typing import Any, Mapping, Sequence

import joblib
import numpy as np
import sklearn
from sklearn.ensemble import IsolationForest

from .features import FEATURE_NAMES
from .loader import DatasetFormatError, load_csv, load_json, parse_finite_number, require_unique_ids
from .validation import DatasetValidationError, validate_dataset


BASELINE_NAME = "isolation_forest_baseline_v1"
MODEL_FILENAME = "isolation_forest.joblib"
THRESHOLD_FILENAME = "threshold_and_feature_contract.json"
PREDICTIONS_FILENAME = "validation_predictions.csv"
METRICS_FILENAME = "validation_metrics.json"
METADATA_FILENAME = "metadata.json"
MODEL_HASH_FILENAME = "model.sha256"

MODEL_PARAMETERS: dict[str, Any] = {
    "n_estimators": 200,
    "max_samples": "auto",
    "max_features": 1.0,
    "contamination": "auto",
    "bootstrap": False,
    "random_state": 42,
}
THRESHOLD_QUANTILE = 0.95
THRESHOLD_QUANTILE_METHOD = "linear"
SCORE_DIRECTION = "anomaly_score = -model.score_samples(X); larger values are more statistically unusual"


class BaselineError(ValueError):
    """Raised when frozen baseline inputs, artifacts, or outputs are invalid."""


def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _write_json(path: Path, value: Mapping[str, Any]) -> None:
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def _ensure_finite_matrix(matrix: np.ndarray, *, context: str) -> np.ndarray:
    array = np.asarray(matrix, dtype=float)
    if array.ndim != 2:
        raise BaselineError(f"{context} must be a two-dimensional matrix")
    if array.shape[1] != len(FEATURE_NAMES):
        raise BaselineError(
            f"{context} must have exactly {len(FEATURE_NAMES)} features; "
            f"received {array.shape[1]}"
        )
    if array.shape[0] == 0:
        raise BaselineError(f"{context} must contain at least one row")
    if not np.isfinite(array).all():
        raise BaselineError(f"{context} contains missing or non-finite feature values")
    return array


def load_feature_matrix(path: str | Path) -> tuple[np.ndarray, list[dict[str, str]]]:
    """Load an exact, feature-only CSV matrix; IDs and labels are forbidden."""

    csv_path = Path(path)
    try:
        columns, rows = load_csv(csv_path, FEATURE_NAMES, exact_columns=FEATURE_NAMES)
    except DatasetFormatError as exc:
        raise BaselineError(str(exc)) from exc
    if columns != list(FEATURE_NAMES):
        raise BaselineError("feature matrix columns must match the frozen feature order")

    values: list[list[float]] = []
    try:
        for row_number, row in enumerate(rows, start=2):
            values.append(
                [
                    parse_finite_number(
                        row[feature], path=csv_path, row_number=row_number, column=feature
                    )
                    for feature in FEATURE_NAMES
                ]
            )
    except DatasetFormatError as exc:
        raise BaselineError(str(exc)) from exc
    return _ensure_finite_matrix(np.asarray(values, dtype=float), context=str(csv_path)), rows


def load_case_keys(path: str | Path) -> list[str]:
    key_path = Path(path)
    try:
        _, rows = load_csv(key_path, ("case_id",), exact_columns=("case_id",))
        require_unique_ids(rows, key_path)
    except DatasetFormatError as exc:
        raise BaselineError(str(exc)) from exc
    return [row["case_id"] for row in rows]


def load_validation_labels(path: str | Path) -> list[dict[str, str]]:
    label_path = Path(path)
    expected_columns = ("case_id", "synthetic_anomaly_label", "perturbation_type")
    try:
        _, rows = load_csv(label_path, expected_columns, exact_columns=expected_columns)
        require_unique_ids(rows, label_path)
    except DatasetFormatError as exc:
        raise BaselineError(str(exc)) from exc
    for row_number, row in enumerate(rows, start=2):
        if row["synthetic_anomaly_label"] not in ("0", "1"):
            raise BaselineError(
                f"invalid synthetic_anomaly_label in '{label_path}' row {row_number}"
            )
        if not row["perturbation_type"].strip():
            raise BaselineError(f"blank perturbation_type in '{label_path}' row {row_number}")
    return rows


def fit_baseline_model(
    training_matrix: np.ndarray, *, estimator: Any | None = None
) -> Any:
    """Fit exactly one estimator using only the supplied feature-only training matrix."""

    matrix = _ensure_finite_matrix(training_matrix, context="training matrix")
    model = estimator or IsolationForest(**MODEL_PARAMETERS)
    return model.fit(matrix)


def anomaly_scores(model: Any, matrix: np.ndarray) -> np.ndarray:
    """Invert sklearn normality scores so larger values consistently mean unusual."""

    features = _ensure_finite_matrix(matrix, context="scoring matrix")
    raw_scores = np.asarray(model.score_samples(features), dtype=float)
    if raw_scores.shape != (features.shape[0],):
        raise BaselineError("score_samples returned an unexpected score shape")
    if not np.isfinite(raw_scores).all():
        raise BaselineError("score_samples returned non-finite values")
    return -raw_scores


def select_validation_threshold(
    scores: np.ndarray, labels: np.ndarray
) -> tuple[float, dict[str, int | float | str]]:
    """Set the declared 95th-percentile threshold using validation references only."""

    score_array = np.asarray(scores, dtype=float)
    label_array = np.asarray(labels, dtype=int)
    if score_array.ndim != 1 or label_array.ndim != 1 or len(score_array) != len(label_array):
        raise BaselineError("validation scores and labels must be aligned one-dimensional arrays")
    if not np.isfinite(score_array).all():
        raise BaselineError("validation scores contain non-finite values")
    if not np.isin(label_array, (0, 1)).all():
        raise BaselineError("validation labels must be 0 or 1")

    reference_scores = score_array[label_array == 0]
    if len(reference_scores) != 80:
        raise BaselineError(
            "threshold selection requires exactly 80 validation reference cases; "
            f"received {len(reference_scores)}"
        )
    threshold = float(
        np.quantile(reference_scores, THRESHOLD_QUANTILE, method=THRESHOLD_QUANTILE_METHOD)
    )
    if not math.isfinite(threshold):
        raise BaselineError("computed threshold is non-finite")
    return threshold, {
        "reference_case_count": int(len(reference_scores)),
        "quantile": THRESHOLD_QUANTILE,
        "quantile_method": THRESHOLD_QUANTILE_METHOD,
        "strict_comparison": "anomaly_score > threshold",
    }


def flags_from_scores(scores: np.ndarray, threshold: float) -> np.ndarray:
    """Apply the frozen strict comparison; a score equal to threshold is not flagged."""

    score_array = np.asarray(scores, dtype=float)
    if score_array.ndim != 1 or not np.isfinite(score_array).all():
        raise BaselineError("scores must be a one-dimensional finite array")
    if not math.isfinite(float(threshold)):
        raise BaselineError("threshold must be finite")
    return score_array > float(threshold)


def _metric_value(numerator: int, denominator: int, undefined_reason: str) -> tuple[float | None, str | None]:
    if denominator == 0:
        return None, undefined_reason
    return numerator / denominator, None


def classification_metrics(labels: np.ndarray, flags: np.ndarray) -> dict[str, Any]:
    """Compute confusion counts and explicit metric undefined-state metadata."""

    actual = np.asarray(labels, dtype=int)
    predicted = np.asarray(flags, dtype=bool)
    if actual.ndim != 1 or predicted.ndim != 1 or len(actual) != len(predicted):
        raise BaselineError("labels and flags must be aligned one-dimensional arrays")
    if not np.isin(actual, (0, 1)).all():
        raise BaselineError("labels must be 0 or 1")

    true_positive = int(np.sum((actual == 1) & predicted))
    false_positive = int(np.sum((actual == 0) & predicted))
    true_negative = int(np.sum((actual == 0) & ~predicted))
    false_negative = int(np.sum((actual == 1) & ~predicted))

    precision, precision_undefined = _metric_value(
        true_positive, true_positive + false_positive, "no_predicted_flags"
    )
    recall, recall_undefined = _metric_value(
        true_positive, true_positive + false_negative, "no_actual_positive_labels"
    )
    if precision is None or recall is None:
        f1: float | None = None
        f1_undefined = "precision_or_recall_undefined"
    elif precision + recall == 0:
        f1 = 0.0
        f1_undefined = None
    else:
        f1 = 2 * precision * recall / (precision + recall)
        f1_undefined = None

    undefined_metrics = {
        metric: reason
        for metric, reason in (
            ("precision", precision_undefined),
            ("recall", recall_undefined),
            ("f1", f1_undefined),
        )
        if reason is not None
    }
    return {
        "support": int(len(actual)),
        "true_positive": true_positive,
        "false_positive": false_positive,
        "true_negative": true_negative,
        "false_negative": false_negative,
        "precision": precision,
        "recall": recall,
        "f1": f1,
        "undefined_metrics": undefined_metrics,
    }


def metrics_by_perturbation_type(
    labels: np.ndarray, flags: np.ndarray, perturbation_types: Sequence[str]
) -> dict[str, dict[str, Any]]:
    if len(labels) != len(flags) or len(labels) != len(perturbation_types):
        raise BaselineError("labels, flags, and perturbation types must be aligned")
    result: dict[str, dict[str, Any]] = {}
    for perturbation_type in sorted(set(perturbation_types)):
        mask = np.asarray(
            [value == perturbation_type for value in perturbation_types], dtype=bool
        )
        result[perturbation_type] = classification_metrics(labels[mask], flags[mask])
    return result


def create_run_directory(path: str | Path) -> Path:
    """Create a new output directory and reject every pre-existing path."""

    run_directory = Path(path)
    if run_directory.exists():
        raise FileExistsError(
            f"Refusing to overwrite existing anomaly baseline output directory: {run_directory}"
        )
    run_directory.mkdir(parents=True, exist_ok=False)
    return run_directory


def _load_source_origin(dataset_dir: Path) -> list[str]:
    catalog_path = dataset_dir / "prepared" / "case_catalog.csv"
    try:
        _, catalog = load_csv(catalog_path, ("record_origin",))
    except DatasetFormatError as exc:
        raise BaselineError(str(exc)) from exc
    origins = sorted({row["record_origin"] for row in catalog})
    if not origins or any(not origin for origin in origins):
        raise BaselineError("case catalog has no usable record origin")
    return origins


def _validate_split_rows(
    matrix: np.ndarray,
    keys: Sequence[str],
    labels: Sequence[Mapping[str, str]] | None,
    *,
    expected_rows: int,
    split: str,
) -> None:
    if len(matrix) != expected_rows or len(keys) != expected_rows:
        raise BaselineError(
            f"{split} requires exactly {expected_rows} rows; "
            f"matrix={len(matrix)} keys={len(keys)}"
        )
    if labels is not None:
        if len(labels) != expected_rows:
            raise BaselineError(f"{split} labels require exactly {expected_rows} rows")
        label_ids = [row["case_id"] for row in labels]
        if label_ids != list(keys):
            raise BaselineError(f"{split} labels do not align with split keys")


def _write_predictions(
    path: Path,
    keys: Sequence[str],
    labels: Sequence[Mapping[str, str]],
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


def run_frozen_baseline(dataset_dir: str | Path, run_directory: str | Path) -> dict[str, Any]:
    """Fit once on X_train and freeze a validation-derived threshold without test scoring."""

    dataset = Path(dataset_dir)
    output = Path(run_directory)
    if output.exists():
        raise FileExistsError(
            f"Refusing to overwrite existing anomaly baseline output directory: {output}"
        )

    try:
        validation_report = validate_dataset(dataset)
    except DatasetValidationError as exc:
        raise BaselineError(f"dataset boundary validation failed: {exc}") from exc
    if validation_report["status"] != "PASS":
        raise BaselineError("dataset boundary validation did not pass")

    contract_path = dataset / "feature_contract.json"
    contract = load_json(contract_path)
    if contract.get("model_features") != list(FEATURE_NAMES):
        raise BaselineError("feature contract does not match the frozen nine-feature order")

    train_matrix, _ = load_feature_matrix(dataset / "splits" / "X_train.csv")
    train_keys = load_case_keys(dataset / "splits" / "keys_train.csv")
    _validate_split_rows(train_matrix, train_keys, None, expected_rows=240, split="train")

    validation_matrix, _ = load_feature_matrix(dataset / "splits" / "X_validation.csv")
    validation_keys = load_case_keys(dataset / "splits" / "keys_validation.csv")
    validation_labels = load_validation_labels(dataset / "splits" / "y_validation.csv")
    _validate_split_rows(
        validation_matrix,
        validation_keys,
        validation_labels,
        expected_rows=108,
        split="validation",
    )

    # Deliberately the only fit call in this run. Validation labels, IDs, and
    # metadata are held outside the numeric matrix and never enter estimator.fit.
    model = fit_baseline_model(train_matrix)
    validation_scores = anomaly_scores(model, validation_matrix)
    labels = np.asarray(
        [int(row["synthetic_anomaly_label"]) for row in validation_labels], dtype=int
    )
    threshold, threshold_policy = select_validation_threshold(validation_scores, labels)
    flags = flags_from_scores(validation_scores, threshold)

    reference_scores = validation_scores[labels == 0]
    reference_flags = flags[labels == 0]
    reference_comparison = {
        "scores_less_than_threshold": int(np.sum(reference_scores < threshold)),
        "scores_equal_to_threshold": int(np.sum(reference_scores == threshold)),
        "scores_greater_than_threshold": int(np.sum(reference_scores > threshold)),
        "flagged_reference_cases": int(np.sum(reference_flags)),
        "reference_case_count": int(len(reference_scores)),
        "actual_reference_flag_rate": float(np.mean(reference_flags)),
    }

    metrics = classification_metrics(labels, flags)
    perturbation_types = [row["perturbation_type"] for row in validation_labels]
    metric_report: dict[str, Any] = {
        "evaluation_scope": "validation only; no test rows were scored",
        "labels": "supplied synthetic scenario labels only; not independently verified normality or wrongdoing",
        "threshold": threshold,
        "threshold_policy": threshold_policy,
        "validation_reference_threshold_application": reference_comparison,
        "overall": metrics,
        "by_perturbation_type": metrics_by_perturbation_type(
            labels, flags, perturbation_types
        ),
    }

    output = create_run_directory(output)
    model_path = output / MODEL_FILENAME
    joblib.dump(model, model_path)
    model_hash = _sha256(model_path)

    _write_json(
        output / THRESHOLD_FILENAME,
        {
            "baseline_name": BASELINE_NAME,
            "feature_order": list(FEATURE_NAMES),
            "feature_contract_sha256": _sha256(contract_path),
            "feature_contract": contract,
            "score_direction": SCORE_DIRECTION,
            "threshold": threshold,
            "threshold_policy": threshold_policy,
            "threshold_limitations": (
                "The 5% target is a declared synthetic experiment choice; it is not "
                "a legal rule, operational SLA, calibrated probability, or guaranteed false-positive rate."
            ),
        },
    )
    _write_predictions(
        output / PREDICTIONS_FILENAME,
        validation_keys,
        validation_labels,
        validation_scores,
        flags,
        threshold,
    )
    _write_json(output / METRICS_FILENAME, metric_report)

    input_paths = {
        "feature_contract": contract_path,
        "dataset_readme": dataset / "README.md",
        "audit_summary": dataset / "audit" / "audit_summary.json",
        "split_manifest": dataset / "splits" / "split_manifest.csv",
        "X_train": dataset / "splits" / "X_train.csv",
        "keys_train": dataset / "splits" / "keys_train.csv",
        "X_validation": dataset / "splits" / "X_validation.csv",
        "keys_validation": dataset / "splits" / "keys_validation.csv",
        "y_validation": dataset / "splits" / "y_validation.csv",
        "case_catalog": dataset / "prepared" / "case_catalog.csv",
    }
    audit_summary = load_json(input_paths["audit_summary"])
    metadata: dict[str, Any] = {
        "baseline_name": BASELINE_NAME,
        "created_utc": datetime.now(UTC).isoformat(),
        "dataset_path": str(dataset.resolve()),
        "output_directory": str(output.resolve()),
        "model_artifact": MODEL_FILENAME,
        "model_sha256": model_hash,
        "actual_estimator_parameters": model.get_params(deep=False),
        "dependencies": {
            "python": sys.version,
            "platform": platform.platform(),
            "numpy": np.__version__,
            "scikit_learn": sklearn.__version__,
            "joblib": joblib.__version__,
        },
        "input_sha256": {name: _sha256(path) for name, path in input_paths.items()},
        "split_manifest_sha256": _sha256(input_paths["split_manifest"]),
        "feature_order": list(FEATURE_NAMES),
        "preprocessing": "none; raw validated numerical feature matrix used directly",
        "score_direction": SCORE_DIRECTION,
        "threshold_policy": threshold_policy,
        "random_seed": 42,
        "training": {
            "rows": int(len(train_matrix)),
            "source": "splits/X_train.csv only",
            "labels_or_ids_passed_to_fit": False,
            "test_rows_scored": False,
        },
        "validation": {
            "rows": int(len(validation_matrix)),
            "reference_rows_used_for_threshold": int(len(reference_scores)),
            "labels_used_only_after_scoring_for_threshold_and_reporting": True,
        },
        "source_origin": {
            "record_origins": _load_source_origin(dataset),
            "recorded_source_zip_sha256": audit_summary.get("source_zip_sha256"),
            "source_zip_hash_independently_verified": False,
        },
        "limitations": [
            "Synthetic-only completed-case experiment; no production or departmental validation.",
            "Reference-normal is generator-designated and not independently verified normality.",
            "Synthetic anomaly labels are controlled perturbation scenarios, not corruption, legal noncompliance, or wrongdoing labels.",
            "Anomaly scores are not probabilities or calibrated confidence estimates.",
            "The source injected anomaly fraction is not a real-world prevalence estimate.",
            "No test set was scored in this milestone.",
            "Generator lineage is not supplied; exact trace-group checks do not establish independent family generation.",
        ],
    }
    _write_json(output / METADATA_FILENAME, metadata)
    (output / MODEL_HASH_FILENAME).write_text(f"{model_hash}  {MODEL_FILENAME}\n", encoding="utf-8")

    return {
        "run_directory": str(output.resolve()),
        "model_sha256": model_hash,
        "threshold": threshold,
        "validation_metrics": metric_report,
        "validation_reference_flag_rate": reference_comparison["actual_reference_flag_rate"],
        "test_rows_scored": False,
    }


def verify_saved_validation_scores(run_directory: str | Path) -> dict[str, Any]:
    """Reload a saved local artifact and reproduce its persisted validation scores.

    The artifact is only loaded from an explicitly supplied, locally created run
    directory. Loading arbitrary untrusted joblib files remains unsafe.
    """

    run = Path(run_directory)
    metadata = load_json(run / METADATA_FILENAME)
    frozen = load_json(run / THRESHOLD_FILENAME)
    expected_order = frozen.get("feature_order")
    if expected_order != list(FEATURE_NAMES):
        raise BaselineError("saved feature order differs from the frozen nine-feature contract")
    if metadata.get("model_sha256") != _sha256(run / MODEL_FILENAME):
        raise BaselineError("saved model SHA-256 does not match metadata")

    dataset = Path(str(metadata.get("dataset_path", "")))
    validation_matrix, _ = load_feature_matrix(dataset / "splits" / "X_validation.csv")
    keys = load_case_keys(dataset / "splits" / "keys_validation.csv")
    prediction_path = run / PREDICTIONS_FILENAME
    expected_columns = (
        "case_id",
        "synthetic_anomaly_label",
        "perturbation_type",
        "anomaly_score",
        "threshold",
        "flagged",
    )
    try:
        _, predictions = load_csv(prediction_path, expected_columns, exact_columns=expected_columns)
    except DatasetFormatError as exc:
        raise BaselineError(str(exc)) from exc
    if [row["case_id"] for row in predictions] != keys:
        raise BaselineError("saved validation predictions no longer align with validation keys")

    model = joblib.load(run / MODEL_FILENAME)
    reproduced_scores = anomaly_scores(model, validation_matrix)
    saved_scores = np.asarray(
        [float(row["anomaly_score"]) for row in predictions], dtype=float
    )
    if not np.isfinite(saved_scores).all():
        raise BaselineError("saved validation predictions contain non-finite scores")
    maximum_absolute_difference = float(np.max(np.abs(reproduced_scores - saved_scores)))
    scores_match = bool(
        np.allclose(reproduced_scores, saved_scores, rtol=1e-12, atol=1e-12)
    )
    threshold = float(frozen["threshold"])
    saved_flags = np.asarray([row["flagged"] == "True" for row in predictions], dtype=bool)
    flags_match = bool(np.array_equal(flags_from_scores(reproduced_scores, threshold), saved_flags))
    if not scores_match or not flags_match:
        raise BaselineError(
            "saved artifact did not reproduce persisted validation scores or strict flags; "
            f"maximum_absolute_difference={maximum_absolute_difference}"
        )
    return {
        "status": "PASS",
        "run_directory": str(run.resolve()),
        "validation_rows": int(len(reproduced_scores)),
        "maximum_absolute_score_difference": maximum_absolute_difference,
        "strict_flags_match": flags_match,
    }
