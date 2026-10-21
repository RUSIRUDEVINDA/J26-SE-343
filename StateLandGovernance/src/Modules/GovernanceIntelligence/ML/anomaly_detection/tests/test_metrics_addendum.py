"""Tests for read-only reconstruction of frozen held-out metrics."""

from __future__ import annotations

import csv
import json
from pathlib import Path

import pytest
from sklearn.ensemble import IsolationForest

from anomaly_detection.report_test_metrics_addendum import (
    DEFAULT_BUNDLE_MANIFEST,
    DEFAULT_DATASET_DIRECTORY,
    DEFAULT_EVALUATION_DIRECTORY,
    MetricsAddendumError,
    PredictionRow,
    build_addendum,
    compute_metrics,
    read_and_validate_predictions,
    verify_manifest_artifacts,
    wilson_interval,
    write_addendum,
)


FROZEN_THRESHOLD = 0.5743688923115832


def _rows() -> list[PredictionRow]:
    rows = [PredictionRow(f"TN-{i}", 0, "none", 0.1, FROZEN_THRESHOLD, False) for i in range(74)]
    rows += [PredictionRow(f"FP-{i}", 0, "none", 0.8, FROZEN_THRESHOLD, True) for i in range(6)]
    rows += [PredictionRow(f"TP-{i}", 1, "extended_gap", 0.8, FROZEN_THRESHOLD, True) for i in range(19)]
    rows += [PredictionRow(f"FN-{i}", 1, "extended_gap", 0.1, FROZEN_THRESHOLD, False) for i in range(9)]
    return rows


def _write_predictions(path: Path, rows: list[dict[str, str]]) -> None:
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=(
            "case_id", "synthetic_anomaly_label", "perturbation_type",
            "anomaly_score", "threshold", "flagged",
        ))
        writer.writeheader()
        writer.writerows(rows)


def _valid_csv_row(**overrides: str) -> dict[str, str]:
    row = {
        "case_id": "CASE-1",
        "synthetic_anomaly_label": "1",
        "perturbation_type": "extended_gap",
        "anomaly_score": "0.8",
        "threshold": repr(FROZEN_THRESHOLD),
        "flagged": "True",
    }
    row.update(overrides)
    return row


def test_compute_metrics_reconstructs_confusion_counts_and_requested_metrics() -> None:
    metrics = compute_metrics(_rows())
    assert metrics["support"] == 108
    assert metrics["reference_support"] == 80
    assert metrics["perturbation_support"] == 28
    assert metrics["confusion_counts"] == {
        "true_positive": 19, "false_positive": 6,
        "true_negative": 74, "false_negative": 9,
    }
    assert metrics["accuracy"] == pytest.approx(0.8611111111111112)
    assert metrics["balanced_accuracy"] == pytest.approx(0.8017857142857143)
    assert metrics["macro_f1"] == pytest.approx(0.8124782957052336)
    assert metrics["weighted_f1"] == pytest.approx(0.858458006786907)
    assert metrics["anomaly_class"]["precision"] == pytest.approx(0.76)
    assert metrics["anomaly_class"]["recall"] == pytest.approx(0.6785714285714286)
    assert metrics["anomaly_class"]["f1"] == pytest.approx(0.7169811320754716)
    assert metrics["always_normal_baseline_accuracy"] == pytest.approx(0.7407407407407407)


def test_wilson_interval_for_frozen_accuracy_is_bounded_and_expected() -> None:
    interval = wilson_interval(93, 108)
    assert interval["lower"] == pytest.approx(0.7834258826)
    assert interval["upper"] == pytest.approx(0.9139899132)
    assert interval["lower"] < 93 / 108 < interval["upper"]


def test_read_predictions_enforces_strict_threshold_and_accepts_unflagged_tie(tmp_path: Path) -> None:
    path = tmp_path / "predictions.csv"
    _write_predictions(path, [_valid_csv_row(anomaly_score=repr(FROZEN_THRESHOLD), flagged="False")])
    rows = read_and_validate_predictions(path, expected_threshold=FROZEN_THRESHOLD)
    assert rows[0].score == rows[0].threshold
    assert rows[0].flagged is False


@pytest.mark.parametrize(("overrides", "message"), [
    ({"synthetic_anomaly_label": "2"}, "Non-binary"),
    ({"flagged": "yes"}, "Invalid Boolean"),
    ({"anomaly_score": "nan"}, "Non-finite"),
    ({"anomaly_score": "0.1", "flagged": "True"}, "strict"),
])
def test_read_predictions_rejects_malformed_evidence(
    tmp_path: Path, overrides: dict[str, str], message: str
) -> None:
    path = tmp_path / "predictions.csv"
    _write_predictions(path, [_valid_csv_row(**overrides)])
    with pytest.raises(MetricsAddendumError, match=message):
        read_and_validate_predictions(path, expected_threshold=FROZEN_THRESHOLD)


def test_read_predictions_rejects_duplicate_case_ids(tmp_path: Path) -> None:
    path = tmp_path / "predictions.csv"
    _write_predictions(path, [_valid_csv_row(), _valid_csv_row()])
    with pytest.raises(MetricsAddendumError, match="Duplicate case_id"):
        read_and_validate_predictions(path, expected_threshold=FROZEN_THRESHOLD)


def test_read_predictions_rejects_threshold_mismatch(tmp_path: Path) -> None:
    path = tmp_path / "predictions.csv"
    _write_predictions(path, [_valid_csv_row(threshold="0.5")])
    with pytest.raises(MetricsAddendumError, match="threshold mismatch"):
        read_and_validate_predictions(path, expected_threshold=FROZEN_THRESHOLD)


def test_verify_manifest_artifacts_rejects_hash_mismatch(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    prediction = tmp_path / "test_predictions.csv"
    prediction.write_text("immutable evidence\n", encoding="utf-8")
    manifest = {"artifacts": {"test_predictions": {
        "path": "test_predictions.csv", "sha256": "0" * 64,
    }}}
    manifest_path = tmp_path / "bundle_manifest.json"
    manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
    monkeypatch.setattr("anomaly_detection.report_test_metrics_addendum.ANOMALY_ROOT", tmp_path)
    with pytest.raises(MetricsAddendumError, match="hash mismatch"):
        verify_manifest_artifacts(manifest_path)


def test_build_addendum_recomputes_frozen_evidence_without_training(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.setattr(
        IsolationForest,
        "fit",
        lambda *_args, **_kwargs: (_ for _ in ()).throw(
            AssertionError("Metrics addendum must not fit an estimator")
        ),
    )
    report = build_addendum()
    assert report["fit_or_scoring_performed"] is False
    assert report["threshold_adjustment_performed"] is False
    assert report["existing_test_metrics_agree"] is True
    assert report["metrics"]["confusion_counts"]["true_positive"] == 19
    assert report["metrics"]["per_perturbation_counts"]["repeated_correction_loop"] == {
        "detected": 0, "missed": 0, "total": 0,
    }
    assert report["metrics"]["per_perturbation_counts"]["excessive_handoffs"]["total"] == 2


def test_write_addendum_is_non_overwriting(tmp_path: Path) -> None:
    output = tmp_path / "classification_metrics_addendum.json"
    created = write_addendum(output)
    original = created.read_bytes()
    with pytest.raises(FileExistsError, match="Refusing to overwrite"):
        write_addendum(output)
    assert created.read_bytes() == original


def test_frozen_paths_used_by_addendum_remain_present() -> None:
    assert DEFAULT_BUNDLE_MANIFEST.is_file()
    assert DEFAULT_DATASET_DIRECTORY.is_dir()
    assert (DEFAULT_EVALUATION_DIRECTORY / "test_predictions.csv").is_file()
