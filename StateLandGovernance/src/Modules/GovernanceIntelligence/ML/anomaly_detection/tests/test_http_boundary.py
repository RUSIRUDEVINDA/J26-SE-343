"""HTTP adapter tests; no estimator is fitted or test split scored."""

from __future__ import annotations

import json
from dataclasses import replace
from pathlib import Path
import sys
import threading
from types import SimpleNamespace

import pytest
from fastapi.testclient import TestClient
from sklearn.ensemble import IsolationForest

ML_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ML_ROOT / "src"))

from config import LABEL_ORDER
from http_service import ServiceSettings, create_app

from anomaly_detection.bundle import DEFAULT_BUNDLE_CONFIG
from anomaly_detection.inference import trace_from_payload


EXAMPLE_PATH = ML_ROOT / "anomaly_detection" / "examples" / "complete_synthetic_trace.json"


def _settings(
    tmp_path: Path,
    *,
    max_request_bytes: int = 262_144,
    max_events: int = 500,
) -> ServiceSettings:
    return ServiceSettings(
        model_path=tmp_path / "complaint.joblib",
        metadata_path=tmp_path / "complaint_metadata.json",
        validation_path=tmp_path / "complaint_validation.json",
        anomaly_bundle_config=tmp_path / "trusted_bundle_config.json",
        max_anomaly_request_bytes=max_request_bytes,
        max_anomaly_events=max_events,
    )


def _complaint_artifact() -> dict:
    return {"model_version": "complaint-stub-v1", "pipeline": object()}


def _bundle():
    return SimpleNamespace(bundle_version="workflow_anomaly_model_v1")


def _payload() -> dict:
    return json.loads(EXAMPLE_PATH.read_text(encoding="utf-8"))


def _anomaly_result(case_id: str) -> dict:
    return {
        "case_id": case_id,
        "model_version": "workflow_anomaly_model_v1",
        "anomaly_score": 0.5,
        "threshold": 0.6,
        "flagged": False,
        "feature_values": {
            "event_count": 4.0,
            "elapsed_days": 2.0416666666666665,
            "max_gap_hours": 23.0,
            "mean_gap_hours": 16.333333333333332,
            "unique_activities": 4.0,
            "repeated_activity_count": 0.0,
            "resource_handoffs": 2.0,
            "institution_switches": 2.0,
            "distinct_resources": 3.0,
        },
        "advisory_note": "Synthetic-reference advisory only; score is not probability or confidence.",
    }


def _validated_stub_predictor(payload, *, bundle):
    case_id, _events = trace_from_payload(payload)
    assert bundle.bundle_version == "workflow_anomaly_model_v1"
    return _anomaly_result(case_id)


def _app(tmp_path: Path, **kwargs):
    return create_app(
        _settings(tmp_path),
        artifact_loader=lambda **_: _complaint_artifact(),
        anomaly_bundle_loader=lambda _path: _bundle(),
        anomaly_predictor=_validated_stub_predictor,
        **kwargs,
    )


def test_valid_complete_trace_preserves_anomaly_contract(tmp_path):
    with TestClient(_app(tmp_path)) as client:
        response = client.post("/workflow-anomaly/predict", json=_payload())

    assert response.status_code == 200
    assert response.json() == _anomaly_result("SYNTHETIC-DEMO-COMPLETE-001")
    assert "probability" in response.json()["advisory_note"]


def test_bundle_is_loaded_once_and_scoring_runs_off_the_async_service_thread(tmp_path):
    load_threads: list[int] = []
    score_threads: list[int] = []
    load_calls = 0

    def loader(_path):
        nonlocal load_calls
        load_calls += 1
        load_threads.append(threading.get_ident())
        return _bundle()

    def predictor(payload, *, bundle):
        score_threads.append(threading.get_ident())
        return _validated_stub_predictor(payload, bundle=bundle)

    app = create_app(
        _settings(tmp_path),
        artifact_loader=lambda **_: _complaint_artifact(),
        anomaly_bundle_loader=loader,
        anomaly_predictor=predictor,
    )
    with TestClient(app) as client:
        assert client.post("/workflow-anomaly/predict", json=_payload()).status_code == 200
        assert client.post("/workflow-anomaly/predict", json=_payload()).status_code == 200

    assert load_calls == 1
    assert len(score_threads) == 2
    assert all(thread_id != load_threads[0] for thread_id in score_threads)


@pytest.mark.parametrize(
    "payload",
    [
        {},
        {"case_id": "SECRET-CASE", "events": [{"event_seq": 1}]},
        {**_payload(), "model_path": "C:/private/model.joblib"},
    ],
)
def test_invalid_payload_is_sanitized(tmp_path, payload):
    with TestClient(_app(tmp_path)) as client:
        response = client.post("/workflow-anomaly/predict", json=payload)

    assert response.status_code == 422
    assert response.json() == {"detail": "Invalid request payload."}
    assert "SECRET-CASE" not in response.text
    assert "C:/private" not in response.text


def test_partial_trace_is_rejected_by_existing_trace_validator(tmp_path):
    payload = _payload()
    payload["events"] = payload["events"][:-1]
    with TestClient(_app(tmp_path)) as client:
        response = client.post("/workflow-anomaly/predict", json=payload)

    assert response.status_code == 422
    assert response.json() == {
        "detail": "Invalid or unsupported complete workflow trace."
    }


@pytest.mark.parametrize("defect", ["sequence", "timezone", "backward"])
def test_invalid_sequence_and_timestamps_are_rejected(tmp_path, defect):
    payload = _payload()
    if defect == "sequence":
        payload["events"][1]["event_seq"] = 3
    elif defect == "timezone":
        payload["events"][1]["timestamp"] += "+05:30"
    else:
        payload["events"][1]["timestamp"] = "2031-02-03T07:59:59"

    with TestClient(_app(tmp_path)) as client:
        response = client.post("/workflow-anomaly/predict", json=payload)

    assert response.status_code == 422
    assert response.json() == {
        "detail": "Invalid or unsupported complete workflow trace."
    }


def test_nonfinite_event_sequence_is_rejected(tmp_path):
    payload = _payload()
    payload["events"][0]["event_seq"] = float("nan")
    body = json.dumps(payload, allow_nan=True)

    with TestClient(_app(tmp_path)) as client:
        response = client.post(
            "/workflow-anomaly/predict",
            content=body,
            headers={"Content-Type": "application/json"},
        )

    assert response.status_code == 422
    assert response.json() == {"detail": "Invalid request payload."}


def test_event_count_limit_rejects_without_scoring(tmp_path):
    calls = []
    app = create_app(
        _settings(tmp_path, max_events=3),
        artifact_loader=lambda **_: _complaint_artifact(),
        anomaly_bundle_loader=lambda _path: _bundle(),
        anomaly_predictor=lambda *args, **kwargs: calls.append((args, kwargs)),
    )
    with TestClient(app) as client:
        response = client.post("/workflow-anomaly/predict", json=_payload())

    assert response.status_code == 413
    assert response.json()["detail"].endswith("3.")
    assert calls == []


def test_request_byte_limit_rejects_without_scoring(tmp_path):
    calls = []
    app = create_app(
        _settings(tmp_path, max_request_bytes=100),
        artifact_loader=lambda **_: _complaint_artifact(),
        anomaly_bundle_loader=lambda _path: _bundle(),
        anomaly_predictor=lambda *args, **kwargs: calls.append((args, kwargs)),
    )
    with TestClient(app) as client:
        response = client.post("/workflow-anomaly/predict", json=_payload())

    assert response.status_code == 413
    assert response.json()["detail"].endswith("100 bytes.")
    assert calls == []


def test_prediction_failure_does_not_expose_exception_or_case_id(tmp_path):
    def failing_predictor(_payload, *, bundle):
        raise RuntimeError("SECRET-CASE at C:/private/model.joblib")

    app = create_app(
        _settings(tmp_path),
        artifact_loader=lambda **_: _complaint_artifact(),
        anomaly_bundle_loader=lambda _path: _bundle(),
        anomaly_predictor=failing_predictor,
    )
    with TestClient(app) as client:
        response = client.post("/workflow-anomaly/predict", json=_payload())

    assert response.status_code == 500
    assert response.json() == {"detail": "Workflow anomaly prediction failed."}
    assert "SECRET-CASE" not in response.text
    assert "C:/private" not in response.text


def test_anomaly_artifact_failure_prevents_startup(tmp_path):
    def failing_loader(_path):
        raise ValueError("hash mismatch at C:/private/model.joblib")

    app = create_app(
        _settings(tmp_path),
        artifact_loader=lambda **_: _complaint_artifact(),
        anomaly_bundle_loader=failing_loader,
    )
    with pytest.raises(RuntimeError, match="workflow anomaly bundle failed to load"):
        with TestClient(app):
            pass


def test_real_http_path_loads_and_scores_frozen_bundle_without_fitting(
    tmp_path, monkeypatch
):
    def forbidden_fit(*_args, **_kwargs):
        raise AssertionError("HTTP inference must not fit an estimator")

    monkeypatch.setattr(IsolationForest, "fit", forbidden_fit)
    settings = replace(
        _settings(tmp_path), anomaly_bundle_config=DEFAULT_BUNDLE_CONFIG
    )
    app = create_app(
        settings,
        artifact_loader=lambda **_: _complaint_artifact(),
    )
    with TestClient(app) as client:
        response = client.post("/workflow-anomaly/predict", json=_payload())

    assert response.status_code == 200
    assert response.json()["model_version"] == "workflow_anomaly_model_v1"
    assert response.json()["threshold"] == 0.5743688923115832


def test_existing_complaint_route_contract_remains_distinct(tmp_path):
    def complaint_predictor(_text, _artifact, case_id):
        return {
            "model_version": "complaint-stub-v1",
            "predicted_category": LABEL_ORDER[0],
            "class_probabilities": {label: 0.25 for label in LABEL_ORDER},
            "case_id": case_id,
            "advisory_note": "Complaint advisory.",
            "closed_set_note": "Closed set.",
        }

    app = create_app(
        _settings(tmp_path),
        artifact_loader=lambda **_: _complaint_artifact(),
        predictor=complaint_predictor,
        anomaly_bundle_loader=lambda _path: _bundle(),
    )
    with TestClient(app) as client:
        response = client.post(
            "/predict", json={"complaint_text": "Fictional complaint."}
        )

    assert response.status_code == 200
    assert "anomaly_score" not in response.json()
    assert set(response.json()["class_probabilities"]) == set(LABEL_ORDER)
