"""HTTP-boundary tests using stubs; the production classifier is never fitted."""

from __future__ import annotations

import ast
import logging
import math
import shutil
import sys
from pathlib import Path

import pytest
from fastapi.testclient import TestClient

_ML_ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(_ML_ROOT / "src"))

from config import LABEL_ORDER
from http_service import (
    DEFAULT_MODEL_VERSION,
    METADATA_PATH_ENV,
    MODEL_PATH_ENV,
    MODEL_VERSION_ENV,
    VALIDATION_PATH_ENV,
    ServiceSettings,
    create_app as create_service_app,
)


class _StubAnomalyBundle:
    bundle_version = "workflow-anomaly-stub-v1"


def create_app(*args, **kwargs):
    """Keep complaint-boundary tests isolated from production anomaly artifacts."""

    kwargs.setdefault("anomaly_bundle_loader", lambda _path: _StubAnomalyBundle())
    return create_service_app(*args, **kwargs)


def _settings(tmp_path: Path, max_request_chars: int = 100) -> ServiceSettings:
    return ServiceSettings(
        model_path=tmp_path / "model.joblib",
        metadata_path=tmp_path / "metadata.json",
        validation_path=tmp_path / "validation.json",
        max_request_chars=max_request_chars,
    )


def _artifact() -> dict:
    return {"model_version": "v2", "pipeline": object()}


def _result(case_id=None) -> dict:
    return {
        "model_version": "v2",
        "predicted_category": LABEL_ORDER[0],
        "class_probabilities": {label: 0.25 for label in LABEL_ORDER},
        "case_id": case_id,
        "advisory_note": "Advisory classification only.",
        "closed_set_note": "Closed-set four-class classifier.",
    }


def _anomaly_result(case_id: str) -> dict:
    return {
        "case_id": case_id,
        "model_version": "workflow-anomaly-stub-v1",
        "anomaly_score": 0.42,
        "threshold": 0.57,
        "flagged": False,
        "feature_values": {
            "event_count": 1.0,
            "elapsed_days": 0.0,
            "max_gap_hours": 0.0,
            "mean_gap_hours": 0.0,
            "unique_activities": 1.0,
            "repeated_activity_count": 0.0,
            "resource_handoffs": 0.0,
            "institution_switches": 0.0,
            "distinct_resources": 1.0,
        },
        "advisory_note": "Advisory anomaly prediction only.",
    }


def test_healthy_startup_returns_readiness_and_model_version(tmp_path):
    app = create_app(_settings(tmp_path), artifact_loader=lambda **_: _artifact())
    with TestClient(app) as client:
        response = client.get("/health")
    assert response.status_code == 200
    assert response.json() == {"status": "ready", "model_version": "v2"}


def test_metrics_report_live_model_and_request_activity_without_payload(tmp_path):
    sensitive_text = "FICTIONAL-SENSITIVE-PROMETHEUS-TEXT"
    app = create_app(
        _settings(tmp_path),
        artifact_loader=lambda **_: _artifact(),
        predictor=lambda _text, _artifact, case_id: _result(case_id),
        anomaly_predictor=lambda payload, *, bundle: _anomaly_result(payload["case_id"]),
    )
    with TestClient(app) as client:
        assert client.post("/predict", json={"complaint_text": sensitive_text}).status_code == 200
        assert client.post(
            "/workflow-anomaly/predict",
            json={
                "case_id": "METRICS-WORKFLOW-CHECK",
                "events": [{
                    "event_seq": 1,
                    "activity": "Application received",
                    "institution": "Divisional Secretariat",
                    "resource": "Land Officer",
                    "timestamp": "2026-10-13T08:00:00Z",
                }],
            },
        ).status_code == 200
        response = client.get("/metrics")

    assert response.status_code == 200
    assert 'state_land_governance_ml_info{model_version="v2",data_mode="live-inference"} 1' in response.text
    assert 'state_land_governance_ml_model_ready{model="complaint-classifier",version="v2"} 1' in response.text
    assert 'state_land_governance_ml_model_ready{model="workflow-anomaly",version="workflow-anomaly-stub-v1"} 1' in response.text
    assert 'state_land_governance_ml_http_requests_total{method="POST",path="/predict",status="200"} 1' in response.text
    assert 'state_land_governance_ml_http_requests_total{method="POST",path="/workflow-anomaly/predict",status="200"} 1' in response.text
    assert sensitive_text not in response.text


def test_startup_failure_is_not_silently_recovered(tmp_path):
    def failing_loader(**_):
        raise ValueError("validation sidecar mismatch")

    app = create_app(_settings(tmp_path), artifact_loader=failing_loader)
    with pytest.raises(RuntimeError, match="failed to load"):
        with TestClient(app):
            pass


def test_model_is_loaded_once_for_multiple_requests(tmp_path):
    calls = []

    def loader(**kwargs):
        calls.append(kwargs)
        return _artifact()

    app = create_app(
        _settings(tmp_path),
        artifact_loader=loader,
        predictor=lambda _text, _artifact, case_id: _result(case_id),
    )
    with TestClient(app) as client:
        assert client.get("/health").status_code == 200
        assert client.post("/predict", json={"complaint_text": "Fictional complaint."}).status_code == 200
        assert client.post("/predict", json={"complaint_text": "Another complaint."}).status_code == 200
    assert len(calls) == 1


def test_valid_prediction_preserves_contract_and_version(tmp_path):
    app = create_app(
        _settings(tmp_path),
        artifact_loader=lambda **_: _artifact(),
        predictor=lambda _text, _artifact, case_id: _result(case_id),
    )
    with TestClient(app) as client:
        response = client.post(
            "/predict",
            json={"complaint_text": "A fictional officer requested an unofficial payment."},
        )
    assert response.status_code == 200
    body = response.json()
    assert body["model_version"] == "v2"
    assert body["predicted_category"] in LABEL_ORDER
    assert set(body["class_probabilities"]) == set(LABEL_ORDER)


@pytest.mark.parametrize(
    "payload",
    [
        {},
        {"complaint_text": "   "},
        {"complaint_text": 123},
        {"complaint_text": None},
    ],
)
def test_invalid_complaint_text_is_rejected_without_echo(tmp_path, payload):
    app = create_app(_settings(tmp_path), artifact_loader=lambda **_: _artifact())
    with TestClient(app) as client:
        response = client.post("/predict", json=payload)
    assert response.status_code == 422
    assert response.json() == {"detail": "Invalid request payload."}
    assert "123" not in response.text


def test_oversized_text_is_rejected_without_truncation(tmp_path):
    received = []

    def predictor(text, artifact, case_id):
        received.append(text)
        return _result(case_id)

    app = create_app(
        _settings(tmp_path, max_request_chars=10),
        artifact_loader=lambda **_: _artifact(),
        predictor=predictor,
    )
    with TestClient(app) as client:
        response = client.post("/predict", json={"complaint_text": "x" * 11})
    assert response.status_code == 413
    assert response.json()["detail"].endswith("10 characters.")
    assert received == []


def test_case_id_is_context_only_and_returned(tmp_path):
    observed = {}
    artifact = _artifact()

    def predictor(text, received_artifact, case_id):
        observed.update(text=text, artifact=received_artifact, case_id=case_id)
        return _result(case_id)

    app = create_app(
        _settings(tmp_path),
        artifact_loader=lambda **_: artifact,
        predictor=predictor,
    )
    with TestClient(app) as client:
        response = client.post(
            "/predict",
            json={"complaint_text": "Fictional complaint.", "case_id": "CASE-TEST-001"},
        )
    assert response.status_code == 200
    assert response.json()["case_id"] == "CASE-TEST-001"
    assert observed == {
        "text": "Fictional complaint.",
        "artifact": artifact,
        "case_id": "CASE-TEST-001",
    }


def test_prediction_failure_returns_sanitized_error(tmp_path):
    def failing_predictor(_text, _artifact, _case_id):
        raise RuntimeError("secret internal failure with local path C:/private/model.joblib")

    app = create_app(
        _settings(tmp_path),
        artifact_loader=lambda **_: _artifact(),
        predictor=failing_predictor,
    )
    with TestClient(app) as client:
        response = client.post("/predict", json={"complaint_text": "Fictional complaint."})
    assert response.status_code == 500
    assert response.json() == {"detail": "Prediction failed."}
    assert "secret" not in response.text
    assert "C:/private" not in response.text


def _clear_classifier_environment(monkeypatch):
    for name in (MODEL_VERSION_ENV, MODEL_PATH_ENV, METADATA_PATH_ENV, VALIDATION_PATH_ENV):
        monkeypatch.delenv(name, raising=False)


def test_default_environment_selects_complete_v2_package(monkeypatch):
    _clear_classifier_environment(monkeypatch)

    settings = ServiceSettings.from_environment()

    assert settings.model_version == DEFAULT_MODEL_VERSION == "v2"
    assert settings.model_path.name == "governance_classifier_v2.joblib"
    assert settings.metadata_path.name == "model_metadata_v2.json"
    assert settings.validation_path.name == "model_validation_v2.json"
    assert settings.model_path.parent == settings.metadata_path.parent
    assert settings.validation_path.parent == settings.model_path.parent / "validation"


def test_explicit_v1_environment_selects_complete_rollback_package(monkeypatch):
    _clear_classifier_environment(monkeypatch)
    monkeypatch.setenv(MODEL_VERSION_ENV, "v1")

    settings = ServiceSettings.from_environment()

    assert settings.model_version == "v1"
    assert settings.model_path.name == "governance_classifier_v1.joblib"
    assert settings.metadata_path.name == "model_metadata_v1.json"
    assert settings.validation_path.name == "model_validation_v1.json"


@pytest.mark.parametrize("configured", ["", "   ", "v3", "latest", "V2"])
def test_invalid_explicit_version_is_rejected(monkeypatch, configured):
    _clear_classifier_environment(monkeypatch)
    monkeypatch.setenv(MODEL_VERSION_ENV, configured)

    with pytest.raises(ValueError, match=MODEL_VERSION_ENV):
        ServiceSettings.from_environment()


def test_incomplete_path_override_is_rejected(monkeypatch, tmp_path):
    _clear_classifier_environment(monkeypatch)
    monkeypatch.setenv(MODEL_PATH_ENV, str(tmp_path / "model.joblib"))

    with pytest.raises(ValueError, match="complete"):
        ServiceSettings.from_environment()


def test_mixed_package_directories_are_rejected(monkeypatch, tmp_path):
    _clear_classifier_environment(monkeypatch)
    first = tmp_path / "first"
    second = tmp_path / "second"
    monkeypatch.setenv(MODEL_PATH_ENV, str(first / "model.joblib"))
    monkeypatch.setenv(METADATA_PATH_ENV, str(second / "metadata.json"))
    monkeypatch.setenv(VALIDATION_PATH_ENV, str(first / "validation" / "validation.json"))

    with pytest.raises(ValueError, match="one version package"):
        ServiceSettings.from_environment()


def test_v1_paths_with_v2_selection_fail_startup(monkeypatch):
    _clear_classifier_environment(monkeypatch)
    v1 = _ML_ROOT / "models" / "v1"
    monkeypatch.setenv(MODEL_VERSION_ENV, "v2")
    monkeypatch.setenv(MODEL_PATH_ENV, str(v1 / "governance_classifier_v1.joblib"))
    monkeypatch.setenv(METADATA_PATH_ENV, str(v1 / "model_metadata_v1.json"))
    monkeypatch.setenv(
        VALIDATION_PATH_ENV,
        str(v1 / "validation" / "model_validation_v1.json"),
    )
    app = create_app(ServiceSettings.from_environment())

    with pytest.raises(RuntimeError, match="failed to load"):
        with TestClient(app):
            pass


def test_loaded_version_mismatch_fails_startup(tmp_path):
    settings = _settings(tmp_path)
    app = create_app(settings, artifact_loader=lambda **_: {"model_version": "v1"})

    with pytest.raises(RuntimeError, match="failed to load"):
        with TestClient(app):
            pass


def test_missing_v2_artifact_fails_startup(tmp_path):
    settings = ServiceSettings(
        model_path=tmp_path / "missing.joblib",
        metadata_path=tmp_path / "missing.json",
        validation_path=tmp_path / "validation" / "missing.json",
        model_version="v2",
    )
    app = create_app(settings)

    with pytest.raises(RuntimeError, match="failed to load"):
        with TestClient(app):
            pass


def test_v2_hash_mismatch_fails_startup(tmp_path):
    source = _ML_ROOT / "models" / "v2"
    package = tmp_path / "v2"
    shutil.copytree(source, package)
    model_path = package / "governance_classifier_v2.joblib"
    model_path.write_bytes(model_path.read_bytes() + b"tamper")
    settings = ServiceSettings(
        model_path=model_path,
        metadata_path=package / "model_metadata_v2.json",
        validation_path=package / "validation" / "model_validation_v2.json",
        model_version="v2",
    )
    app = create_app(settings)

    with pytest.raises(RuntimeError, match="failed to load"):
        with TestClient(app):
            pass


def test_real_v2_predict_contract_and_case_id(monkeypatch):
    _clear_classifier_environment(monkeypatch)
    app = create_app(ServiceSettings.from_environment())
    complaint = "A fictional protected wetland lease was used to dump construction waste."

    with TestClient(app) as client:
        response = client.post(
            "/predict",
            json={"complaint_text": complaint, "case_id": "CASE-V2-HTTP-001"},
        )

    assert response.status_code == 200
    body = response.json()
    assert body["model_version"] == "v2"
    assert body["predicted_category"] in LABEL_ORDER
    assert body["case_id"] == "CASE-V2-HTTP-001"
    assert body["advisory_note"]
    assert body["closed_set_note"]
    probabilities = body["class_probabilities"]
    assert set(probabilities) == set(LABEL_ORDER)
    assert all(math.isfinite(value) and 0 <= value <= 1 for value in probabilities.values())
    assert math.isclose(sum(probabilities.values()), 1.0, rel_tol=0.0, abs_tol=1e-10)
    assert body["predicted_category"] == max(probabilities, key=probabilities.get)


def test_real_v1_rollback_remains_compatible(monkeypatch):
    _clear_classifier_environment(monkeypatch)
    monkeypatch.setenv(MODEL_VERSION_ENV, "v1")
    app = create_app(ServiceSettings.from_environment())

    with TestClient(app) as client:
        health = client.get("/health")
        prediction = client.post(
            "/predict",
            json={
                "complaint_text": "A fictional lease payment remains overdue.",
                "case_id": "CASE-V1-ROLLBACK-001",
            },
        )

    assert health.status_code == 200
    assert health.json() == {"status": "ready", "model_version": "v1"}
    assert prediction.status_code == 200
    assert prediction.json()["model_version"] == "v1"
    assert prediction.json()["case_id"] == "CASE-V1-ROLLBACK-001"


def test_unexpected_request_field_is_rejected(tmp_path):
    app = create_app(_settings(tmp_path), artifact_loader=lambda **_: _artifact())
    with TestClient(app) as client:
        response = client.post(
            "/predict",
            json={"complaint_text": "Fictional complaint.", "model_version": "v1"},
        )
    assert response.status_code == 422
    assert response.json() == {"detail": "Invalid request payload."}


def test_complaint_text_is_absent_from_prediction_failure_logs(tmp_path, caplog):
    complaint = "FICTIONAL-SENSITIVE-COMPLAINT-TEXT-DO-NOT-LOG"

    def failing_predictor(text, _artifact, _case_id):
        raise RuntimeError(text)

    app = create_app(
        _settings(tmp_path),
        artifact_loader=lambda **_: _artifact(),
        predictor=failing_predictor,
    )
    with caplog.at_level(logging.ERROR):
        with TestClient(app) as client:
            response = client.post("/predict", json={"complaint_text": complaint})

    assert response.status_code == 500
    assert complaint not in caplog.text


def test_runtime_service_contains_no_training_method_invocation():
    tree = ast.parse((_ML_ROOT / "src" / "http_service.py").read_text(encoding="utf-8"))
    prohibited = {"fit", "fit_transform", "partial_fit"}
    calls = {
        node.func.attr
        for node in ast.walk(tree)
        if isinstance(node, ast.Call)
        and isinstance(node.func, ast.Attribute)
        and node.func.attr in prohibited
    }
    assert calls == set()
