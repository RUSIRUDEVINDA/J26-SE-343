"""Focused tests for held-out evidence and complete-trace local inference."""

from __future__ import annotations

import json
from pathlib import Path
import subprocess
import sys
from types import SimpleNamespace

import numpy as np
import pytest

import anomaly_detection.bundle as bundle_module
import anomaly_detection.inference as inference_module
from anomaly_detection.bundle import ArtifactVerificationError, load_trusted_local_bundle
from anomaly_detection.evaluation import perturbation_detection_counts
from anomaly_detection.features import FEATURE_NAMES
from anomaly_detection.inference import InferenceInputError, infer_payload, trace_from_payload


def complete_payload() -> dict:
    return {
        "case_id": "FICTIONAL-INFERENCE-1",
        "events": [
            {
                "event_seq": 1,
                "activity": "Fictional Intake",
                "institution": "District",
                "resource": "Officer A",
                "timestamp": "2032-01-01T08:00:00",
            },
            {
                "event_seq": 2,
                "activity": "Fictional Review",
                "institution": "District",
                "resource": "Officer B",
                "timestamp": "2032-01-01T09:00:00",
            },
            {
                "event_seq": 3,
                "activity": "DS Final Notification",
                "institution": "District",
                "resource": "Officer B",
                "timestamp": "2032-01-01T10:00:00",
            },
        ],
    }


class DeterministicModel:
    def score_samples(self, matrix: np.ndarray) -> np.ndarray:
        return np.full(len(matrix), -0.42)


def fake_bundle(feature_order: list[str] | None = None) -> SimpleNamespace:
    return SimpleNamespace(
        bundle_version="test-bundle-v1",
        manifest={"feature_order": feature_order or list(FEATURE_NAMES)},
        frozen_run=SimpleNamespace(model=DeterministicModel(), threshold=0.40),
    )


def test_infer_payload_when_called_twice_should_be_deterministic(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(inference_module, "load_trusted_local_bundle", lambda _: fake_bundle())

    first = infer_payload(complete_payload())
    second = infer_payload(complete_payload())

    assert first == second
    assert first["anomaly_score"] == pytest.approx(0.42)
    assert first["flagged"] is True
    assert list(first["feature_values"]) == list(FEATURE_NAMES)
    assert "not a probability" in first["advisory_note"]


def test_infer_payload_when_bundle_feature_order_mismatches_should_reject(monkeypatch: pytest.MonkeyPatch) -> None:
    invalid_order = list(reversed(FEATURE_NAMES))
    monkeypatch.setattr(
        inference_module, "load_trusted_local_bundle", lambda _: fake_bundle(invalid_order)
    )

    with pytest.raises(InferenceInputError, match="feature order"):
        infer_payload(complete_payload())


def test_trace_from_payload_when_partial_or_invalid_should_reject_without_repair() -> None:
    partial = complete_payload()
    partial["events"] = partial["events"][:-1]

    with pytest.raises(InferenceInputError, match="unsupported partial"):
        trace_from_payload(partial)

    invalid = complete_payload()
    del invalid["events"][0]["timestamp"]
    with pytest.raises(InferenceInputError, match="missing required fields"):
        trace_from_payload(invalid)


def test_trace_from_payload_when_model_path_is_supplied_should_reject_payload_control() -> None:
    payload = complete_payload() | {"model_path": "untrusted.joblib"}

    with pytest.raises(InferenceInputError, match="trusted local configuration"):
        trace_from_payload(payload)


def test_perturbation_detection_counts_when_type_has_zero_examples_should_report_undefined() -> None:
    labels = np.asarray([0, 1, 1], dtype=int)
    flags = np.asarray([False, True, False])
    types = ["none", "extended_gap", "extended_gap"]

    counts = perturbation_detection_counts(labels, flags, types)

    assert counts["extended_gap"]["synthetic_positive_cases"] == 2
    assert counts["extended_gap"]["detected_cases"] == 1
    assert counts["repeated_correction_loop"] == {
        "synthetic_positive_cases": 0,
        "flagged_cases": 0,
        "detected_cases": 0,
        "missed_cases": 0,
        "detection_rate": None,
        "undefined_metrics": {"detection_rate": "zero_test_examples"},
    }


def test_load_trusted_local_bundle_when_artifact_hash_mismatches_should_fail_clearly(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    artifact = tmp_path / "model.joblib"
    artifact.write_text("fictional artifact", encoding="utf-8")
    manifest = {
        "bundle_version": "workflow_anomaly_model_v1",
        "trusted_local_only": True,
        "feature_order": list(FEATURE_NAMES),
        "score_direction": "anomaly_score = -model.score_samples(X); larger values are more statistically unusual",
        "strict_flag_rule": "anomaly_score > threshold",
        "threshold": 0.5,
        "artifacts": {
            "model": {"path": "model.joblib", "sha256": "0" * 64}
        },
    }
    (tmp_path / "bundle_manifest.json").write_text(json.dumps(manifest), encoding="utf-8")
    config = {
        "bundle_version": "workflow_anomaly_model_v1",
        "bundle_manifest": "bundle_manifest.json",
        "payload_model_paths_allowed": False,
        "trusted_local_artifact_paths": {"model": "model.joblib"},
    }
    config_path = tmp_path / "local_inference_config.json"
    config_path.write_text(json.dumps(config), encoding="utf-8")
    monkeypatch.setattr(bundle_module, "ANOMALY_ROOT", tmp_path)

    with pytest.raises(ArtifactVerificationError, match="model SHA-256 mismatch"):
        load_trusted_local_bundle(config_path)


def test_load_trusted_local_bundle_when_artifact_is_missing_should_fail_clearly(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    manifest = {
        "bundle_version": "workflow_anomaly_model_v1",
        "trusted_local_only": True,
        "feature_order": list(FEATURE_NAMES),
        "score_direction": "anomaly_score = -model.score_samples(X); larger values are more statistically unusual",
        "strict_flag_rule": "anomaly_score > threshold",
        "threshold": 0.5,
        "artifacts": {
            "model": {"path": "missing.joblib", "sha256": "0" * 64}
        },
    }
    (tmp_path / "bundle_manifest.json").write_text(json.dumps(manifest), encoding="utf-8")
    config = {
        "bundle_version": "workflow_anomaly_model_v1",
        "bundle_manifest": "bundle_manifest.json",
        "payload_model_paths_allowed": False,
        "trusted_local_artifact_paths": {"model": "missing.joblib"},
    }
    config_path = tmp_path / "local_inference_config.json"
    config_path.write_text(json.dumps(config), encoding="utf-8")
    monkeypatch.setattr(bundle_module, "ANOMALY_ROOT", tmp_path)

    with pytest.raises(ArtifactVerificationError, match="model artifact is missing"):
        load_trusted_local_bundle(config_path)


def test_frozen_bundle_and_documented_cli_input_when_loaded_in_fresh_process_should_pass() -> None:
    ml_directory = Path(__file__).resolve().parents[2]
    example = "anomaly_detection/examples/complete_synthetic_trace.json"
    verification = subprocess.run(
        [sys.executable, "-m", "anomaly_detection.cli", "verify-bundle"],
        cwd=ml_directory,
        check=True,
        capture_output=True,
        text=True,
    )
    inference = subprocess.run(
        [
            sys.executable,
            "-m",
            "anomaly_detection.cli",
            "infer",
            "--input-json",
            example,
        ],
        cwd=ml_directory,
        check=True,
        capture_output=True,
        text=True,
    )

    assert json.loads(verification.stdout)["status"] == "PASS"
    result = json.loads(inference.stdout)
    assert result["case_id"] == "SYNTHETIC-DEMO-COMPLETE-001"
    assert result["model_version"] == "workflow_anomaly_model_v1"
