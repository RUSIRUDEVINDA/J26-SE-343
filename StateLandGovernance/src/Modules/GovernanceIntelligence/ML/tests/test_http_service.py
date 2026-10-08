"""HTTP-boundary tests using stubs; the production classifier is never fitted."""

from __future__ import annotations

import sys
from pathlib import Path

import pytest
from fastapi.testclient import TestClient

_ML_ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(_ML_ROOT / "src"))

from config import LABEL_ORDER
from http_service import ServiceSettings, create_app as create_service_app


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
    return {"model_version": "stub-v1", "pipeline": object()}


def _result(case_id=None) -> dict:
    return {
        "model_version": "stub-v1",
        "predicted_category": LABEL_ORDER[0],
        "class_probabilities": {label: 0.25 for label in LABEL_ORDER},
        "case_id": case_id,
        "advisory_note": "Advisory classification only.",
        "closed_set_note": "Closed-set four-class classifier.",
    }


def test_healthy_startup_returns_readiness_and_model_version(tmp_path):
    app = create_app(_settings(tmp_path), artifact_loader=lambda **_: _artifact())
    with TestClient(app) as client:
        response = client.get("/health")
    assert response.status_code == 200
    assert response.json() == {"status": "ready", "model_version": "stub-v1"}


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
    assert body["model_version"] == "stub-v1"
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
