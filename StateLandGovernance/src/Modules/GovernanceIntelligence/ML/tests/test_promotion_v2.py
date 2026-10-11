"""Focused checks for evidence-gated, byte-preserving v2 promotion."""

from __future__ import annotations

import ast
import hashlib
import json
import shutil
import subprocess
import sys
from pathlib import Path
from unittest.mock import MagicMock

import joblib
import numpy as np
import pytest
from sklearn.feature_extraction.text import TfidfVectorizer

ML_ROOT = Path(__file__).resolve().parent.parent
SRC = ML_ROOT / "src"
sys.path.insert(0, str(SRC))

import promote_v2
from config import LABEL_ORDER
from predict_v1 import (
    DEFAULT_JOBLIB,
    DEFAULT_META,
    DEFAULT_VALIDATION,
    load_model_version,
    load_versioned_artifact,
    predict_complaint,
    validate_fitted_pipeline,
)


V1_MODEL_HASH = "14e91be02ef9bdb2f5e440782503c8c89dfa479d010a3fc993e651aa7a9b25c6"
V1_METADATA_HASH = "07b110dfd95087a0598c60e88ab5eec34aa3170e22f464b025df1222e9f95419"


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


@pytest.fixture(scope="module")
def promoted_package(tmp_path_factory):
    output = tmp_path_factory.mktemp("promotion-v2") / "v2"
    result = promote_v2.promote(output)
    assert result["status"] == "PASS"
    return output


def copy_package(source: Path, destination: Path) -> tuple[Path, Path, Path]:
    shutil.copytree(source, destination)
    return (
        destination / "governance_classifier_v2.joblib",
        destination / "model_metadata_v2.json",
        destination / "validation" / "model_validation_v2.json",
    )


def refresh_metadata_hash(metadata_path: Path, sidecar_path: Path) -> None:
    sidecar = json.loads(sidecar_path.read_text(encoding="utf-8"))
    sidecar["metadata"]["sha256"] = sha256(metadata_path)
    sidecar["metadata"]["size_bytes"] = metadata_path.stat().st_size
    sidecar_path.write_text(json.dumps(sidecar), encoding="utf-8")


def load_candidate():
    return joblib.load(promote_v2.CANDIDATE)


def v2_validation_metadata() -> dict:
    return {
        "model_version": "v2",
        "label_taxonomy": list(LABEL_ORDER),
        "fitted_classes_order": list(promote_v2.FITTED_CLASS_ORDER),
        "pipeline_validation": {
            "architecture": "arm_e_word_character",
            "stable_parameters": dict(promote_v2.EXPECTED_STABLE_PARAMETERS),
        },
    }


def test_exact_candidate_hash_requirement(monkeypatch):
    monkeypatch.setattr(promote_v2, "EXPECTED_CANDIDATE_SHA256", "0" * 64)
    with pytest.raises(ValueError, match="Candidate SHA-256"):
        promote_v2.validate_saved_evidence()


def test_promoted_artifact_is_byte_for_byte_copy(promoted_package):
    promoted = promoted_package / "governance_classifier_v2.joblib"
    assert promoted.read_bytes() == promote_v2.CANDIDATE.read_bytes()
    assert sha256(promoted) == promote_v2.EXPECTED_CANDIDATE_SHA256


def test_output_overwrite_is_refused(promoted_package):
    with pytest.raises(FileExistsError, match="Refusing to overwrite"):
        promote_v2.promote(promoted_package)


def test_v1_artifacts_remain_unchanged():
    assert sha256(DEFAULT_JOBLIB) == V1_MODEL_HASH
    assert sha256(DEFAULT_META) == V1_METADATA_HASH


def test_promotion_sources_have_no_training_method_calls():
    prohibited = {"fit", "fit_transform", "partial_fit"}
    for path in (SRC / "promote_v2.py", SRC / "validate_v2_fresh_process.py"):
        tree = ast.parse(path.read_text(encoding="utf-8"))
        found = {
            node.func.attr
            for node in ast.walk(tree)
            if isinstance(node, ast.Call)
            and isinstance(node.func, ast.Attribute)
            and node.func.attr in prohibited
        }
        assert found == set()


def test_malformed_v2_metadata_is_rejected(promoted_package, tmp_path):
    model, metadata, sidecar = copy_package(promoted_package, tmp_path / "v2")
    value = json.loads(metadata.read_text(encoding="utf-8"))
    del value["pipeline_validation"]
    metadata.write_text(json.dumps(value), encoding="utf-8")
    refresh_metadata_hash(metadata, sidecar)
    with pytest.raises(ValueError, match="pipeline_validation"):
        load_versioned_artifact(model, metadata, sidecar)


def test_missing_sidecar_is_rejected(promoted_package, tmp_path):
    model, metadata, sidecar = copy_package(promoted_package, tmp_path / "v2")
    sidecar.unlink()
    with pytest.raises(FileNotFoundError, match="validation evidence"):
        load_versioned_artifact(model, metadata, sidecar)


def test_model_hash_mismatch_is_rejected(promoted_package, tmp_path):
    model, metadata, sidecar = copy_package(promoted_package, tmp_path / "v2")
    model.write_bytes(model.read_bytes() + b"tamper")
    with pytest.raises(ValueError, match="Model artifact hash"):
        load_versioned_artifact(model, metadata, sidecar)


def test_metadata_hash_mismatch_is_rejected(promoted_package, tmp_path):
    model, metadata, sidecar = copy_package(promoted_package, tmp_path / "v2")
    metadata.write_text(metadata.read_text(encoding="utf-8") + " ", encoding="utf-8")
    with pytest.raises(ValueError, match="metadata hash"):
        load_versioned_artifact(model, metadata, sidecar)


def test_pipeline_step_mismatch_is_rejected():
    pipeline = load_candidate()
    pipeline.steps[1] = ("unexpected", pipeline.steps[1][1])
    with pytest.raises(ValueError, match="Unexpected pipeline steps"):
        validate_fitted_pipeline(pipeline, v2_validation_metadata())


def test_estimator_type_mismatch_is_rejected():
    pipeline = load_candidate()
    pipeline.steps[1] = ("features", TfidfVectorizer())
    with pytest.raises(ValueError, match="Unexpected estimator type"):
        validate_fitted_pipeline(pipeline, v2_validation_metadata())


@pytest.mark.parametrize(
    ("parameter", "value"),
    [
        ("features__word__lowercase", False),
        ("features__character__min_df", 2),
        ("clf__C", 9.0),
    ],
)
def test_pipeline_parameter_mismatch_is_rejected(parameter, value):
    pipeline = load_candidate()
    pipeline.set_params(**{parameter: value})
    with pytest.raises(ValueError, match="stable parameter mismatch"):
        validate_fitted_pipeline(pipeline, v2_validation_metadata())


def test_unfitted_pipeline_is_rejected():
    pipeline = load_candidate()
    word = pipeline.named_steps["features"].transformer_list[0][1]
    del word.vocabulary_
    with pytest.raises(ValueError, match="not fitted"):
        validate_fitted_pipeline(pipeline, v2_validation_metadata())


def test_taxonomy_mismatch_is_rejected(promoted_package, tmp_path):
    model, metadata, sidecar = copy_package(promoted_package, tmp_path / "v2")
    value = json.loads(metadata.read_text(encoding="utf-8"))
    value["label_taxonomy"][0] = "Unknown"
    metadata.write_text(json.dumps(value), encoding="utf-8")
    refresh_metadata_hash(metadata, sidecar)
    with pytest.raises(ValueError, match="label_taxonomy"):
        load_versioned_artifact(model, metadata, sidecar)


def test_class_order_mismatch_is_rejected(promoted_package, tmp_path):
    model, metadata, sidecar = copy_package(promoted_package, tmp_path / "v2")
    value = json.loads(metadata.read_text(encoding="utf-8"))
    value["fitted_classes_order"] = list(reversed(value["fitted_classes_order"]))
    metadata.write_text(json.dumps(value), encoding="utf-8")
    refresh_metadata_hash(metadata, sidecar)
    with pytest.raises(ValueError, match="class order"):
        load_versioned_artifact(model, metadata, sidecar)


def test_invalid_probability_is_rejected():
    pipeline = MagicMock()
    classifier = MagicMock()
    classifier.classes_ = np.array(sorted(LABEL_ORDER))
    pipeline.named_steps = {"clf": classifier}
    pipeline.predict.return_value = np.array([classifier.classes_[0]])
    pipeline.predict_proba.return_value = np.array([[float("nan"), 0.2, 0.3, 0.5]])
    with pytest.raises(ValueError, match="finite"):
        predict_complaint("Fictional complaint", {"pipeline": pipeline, "model_version": "v2"})


def test_v1_and_v2_loader_compatibility(promoted_package):
    v1 = load_versioned_artifact(DEFAULT_JOBLIB, DEFAULT_META, DEFAULT_VALIDATION)
    v2 = load_model_version("v2", models_dir=promoted_package.parent)
    assert v1["model_version"] == "v1"
    assert v2["model_version"] == "v2"


def test_fresh_process_loading(promoted_package):
    command = [
        sys.executable,
        str(SRC / "validate_v2_fresh_process.py"),
        "--joblib", str(promoted_package / "governance_classifier_v2.joblib"),
        "--metadata", str(promoted_package / "model_metadata_v2.json"),
        "--validation", str(promoted_package / "validation" / "model_validation_v2.json"),
    ]
    result = subprocess.run(command, cwd=ML_ROOT, check=True, capture_output=True, text=True)
    evidence = json.loads(result.stdout)
    assert evidence["status"] == "PASS"
    assert evidence["fictional_input_count"] == 4
    assert evidence["training_invoked"] is False


def test_fastapi_default_remains_v1(monkeypatch):
    import http_service

    for name in (
        http_service.MODEL_PATH_ENV,
        http_service.METADATA_PATH_ENV,
        http_service.VALIDATION_PATH_ENV,
    ):
        monkeypatch.delenv(name, raising=False)
    settings = http_service.ServiceSettings.from_environment()
    assert settings.model_path.resolve() == DEFAULT_JOBLIB.resolve()
    assert settings.metadata_path.resolve() == DEFAULT_META.resolve()
    assert settings.validation_path.resolve() == DEFAULT_VALIDATION.resolve()
