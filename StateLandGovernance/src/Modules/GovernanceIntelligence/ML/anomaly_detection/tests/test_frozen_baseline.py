"""Focused tests for the frozen synthetic-only Isolation Forest baseline."""

from __future__ import annotations

import csv
import json
from pathlib import Path
import subprocess
import sys

import joblib
import numpy as np
import pytest

from anomaly_detection.baseline import (
    BaselineError,
    FEATURE_NAMES,
    anomaly_scores,
    classification_metrics,
    create_run_directory,
    fit_baseline_model,
    flags_from_scores,
    load_feature_matrix,
    select_validation_threshold,
)


class RecordingEstimator:
    """Small in-memory estimator spy; it accepts only the provided fit matrix."""

    def __init__(self) -> None:
        self.fit_matrix: np.ndarray | None = None

    def fit(self, matrix: np.ndarray) -> "RecordingEstimator":
        self.fit_matrix = np.asarray(matrix, dtype=float).copy()
        return self


class FixedScoreEstimator:
    def score_samples(self, matrix: np.ndarray) -> np.ndarray:
        assert len(matrix) == 2
        return np.asarray([-0.20, -0.75])


def feature_matrix(rows: int) -> np.ndarray:
    return np.asarray(
        [[float(row + column + 1) for column in range(len(FEATURE_NAMES))] for row in range(rows)]
    )


def write_csv(path: Path, columns: list[str], rows: list[dict[str, str]]) -> None:
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=columns)
        writer.writeheader()
        writer.writerows(rows)


def test_fit_baseline_model_when_given_training_matrix_should_only_pass_training_rows_to_fit() -> None:
    training = feature_matrix(3)
    validation = feature_matrix(2) + 1000
    estimator = RecordingEstimator()

    result = fit_baseline_model(training, estimator=estimator)

    assert result is estimator
    assert estimator.fit_matrix is not None
    np.testing.assert_array_equal(estimator.fit_matrix, training)
    assert not np.array_equal(estimator.fit_matrix, validation)


def test_load_feature_matrix_when_label_or_id_column_is_present_should_reject_leakage(tmp_path: Path) -> None:
    path = tmp_path / "leaked.csv"
    columns = list(FEATURE_NAMES) + ["case_id", "label"]
    row = {feature: "1" for feature in FEATURE_NAMES} | {"case_id": "CASE-1", "label": "1"}
    write_csv(path, columns, [row])

    with pytest.raises(BaselineError, match="unexpected columns or order"):
        load_feature_matrix(path)


def test_anomaly_scores_and_flags_when_score_equals_threshold_should_use_larger_means_unusual_and_strict_comparison() -> None:
    scores = anomaly_scores(FixedScoreEstimator(), feature_matrix(2))

    np.testing.assert_array_equal(scores, np.asarray([0.20, 0.75]))
    np.testing.assert_array_equal(flags_from_scores(scores, 0.75), np.asarray([False, False]))
    np.testing.assert_array_equal(flags_from_scores(scores, 0.74), np.asarray([False, True]))


def test_select_validation_threshold_when_perturbation_score_is_extreme_should_use_only_80_reference_rows() -> None:
    reference_scores = np.linspace(0.0, 0.79, 80)
    perturbation_scores = np.full(28, 1000.0)
    scores = np.concatenate((reference_scores, perturbation_scores))
    labels = np.concatenate((np.zeros(80, dtype=int), np.ones(28, dtype=int)))

    threshold, details = select_validation_threshold(scores, labels)

    assert threshold == pytest.approx(np.quantile(reference_scores, 0.95, method="linear"))
    assert threshold < 1.0
    assert details["reference_case_count"] == 80
    assert details["quantile_method"] == "linear"


@pytest.mark.parametrize(
    ("columns", "value", "match"),
    [
        (list(FEATURE_NAMES[:-1]), "1", "missing required columns"),
        (list(FEATURE_NAMES), "nan", "non-finite"),
    ],
)
def test_load_feature_matrix_when_features_are_missing_or_nonfinite_should_fail_clearly(
    tmp_path: Path, columns: list[str], value: str, match: str
) -> None:
    path = tmp_path / "invalid.csv"
    row = {column: value for column in columns}
    write_csv(path, columns, [row])

    with pytest.raises(BaselineError, match=match):
        load_feature_matrix(path)


def test_create_run_directory_when_path_exists_should_refuse_overwrite(tmp_path: Path) -> None:
    existing = tmp_path / "existing-run"
    existing.mkdir()

    with pytest.raises(FileExistsError, match="Refusing to overwrite"):
        create_run_directory(existing)


def test_classification_metrics_when_denominator_is_zero_should_report_undefined_state() -> None:
    metrics = classification_metrics(np.zeros(2, dtype=int), np.zeros(2, dtype=bool))

    assert metrics["precision"] is None
    assert metrics["recall"] is None
    assert metrics["f1"] is None
    assert metrics["undefined_metrics"] == {
        "precision": "no_predicted_flags",
        "recall": "no_actual_positive_labels",
        "f1": "precision_or_recall_undefined",
    }


def test_saved_model_when_loaded_in_fresh_process_should_reproduce_scores(tmp_path: Path) -> None:
    training = feature_matrix(12)
    validation = feature_matrix(4) + 0.25
    model = fit_baseline_model(training)
    expected_scores = anomaly_scores(model, validation)
    model_path = tmp_path / "model.joblib"
    matrix_path = tmp_path / "validation.npy"
    joblib.dump(model, model_path)
    np.save(matrix_path, validation)

    program = (
        "import json, joblib, numpy as np, sys; "
        "model = joblib.load(sys.argv[1]); "
        "matrix = np.load(sys.argv[2]); "
        "print(json.dumps((-model.score_samples(matrix)).tolist()))"
    )
    process = subprocess.run(
        [sys.executable, "-c", program, str(model_path), str(matrix_path)],
        check=True,
        capture_output=True,
        text=True,
    )

    np.testing.assert_allclose(np.asarray(json.loads(process.stdout)), expected_scores)
