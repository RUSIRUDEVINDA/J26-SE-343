"""
test_inference_v1.py
--------------------
Focused unit tests for train_v1.py and predict_v1.py.

Tests cover:
- blank/invalid input handling
- class-probability mapping with deliberately reordered classes_
- model_version returned correctly
- missing artifact/metadata handling
- save/load preservation of predictions and probabilities
- refusal to overwrite an existing version directory
- closed-set note present in output

These tests do NOT claim accuracy evaluation. They confirm execution
correctness of the inference pipeline.
"""

from __future__ import annotations

import json
import hashlib
import sys
import tempfile
from pathlib import Path
from unittest.mock import MagicMock, patch

import numpy as np
import pytest

# Add src/ to path
_ML_ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(_ML_ROOT / "src"))

from config import LABEL_ORDER


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _make_mock_pipeline(classes_order=None):
    """Build a mock pipeline with a controllable classes_ order."""
    if classes_order is None:
        classes_order = LABEL_ORDER.copy()

    clf_mock = MagicMock()
    clf_mock.classes_ = np.array(classes_order)

    # Return probability in the order of classes_order
    n = len(classes_order)
    # Assign non-uniform probs so mapping errors are detectable
    probs = np.array([0.55, 0.25, 0.15, 0.05][:n])
    probs = probs / probs.sum()
    clf_mock.predict_proba.return_value = np.array([probs])
    clf_mock.predict.return_value = np.array([classes_order[0]])

    pipeline = MagicMock()
    pipeline.named_steps = {"clf": clf_mock}
    pipeline.predict.return_value = np.array([classes_order[0]])
    pipeline.predict_proba.return_value = np.array([probs])
    return pipeline, classes_order, probs


def _make_artifact(pipeline, version="v1"):
    return {
        "pipeline":      pipeline,
        "metadata":      {"model_version": version},
        "model_version": version,
        "joblib_path":   Path("mock/path.joblib"),
        "meta_path":     Path("mock/meta.json"),
    }


def _fit_valid_pipeline():
    from boilerplate_transformer import build_pipeline_with_cleaner

    texts = [
        "Officer demanded an unofficial payment for lease processing.",
        "Lease revenue was not deposited into the treasury.",
        "State land was transferred without authorization.",
        "Protected wetland was leased without environmental clearance.",
        "Integrity review identified a procedural conflict.",
        "Rent arrears were not recovered from the lessee.",
        "The parcel was allocated without a public process.",
        "Forest reserve land was used for commercial construction.",
    ]
    labels = LABEL_ORDER * 2
    pipeline = build_pipeline_with_cleaner()
    pipeline.fit(texts, labels)
    return pipeline


def _write_valid_pair(tmp_path, version="test-v1", fitted_order=None):
    import joblib

    pipeline = _fit_valid_pipeline()
    classes = pipeline.named_steps["clf"].classes_.tolist()
    metadata = {
        "model_version": version,
        "label_taxonomy": LABEL_ORDER,
        "fitted_classes_order": fitted_order if fitted_order is not None else classes,
    }
    model_path = tmp_path / "model.joblib"
    meta_path = tmp_path / "metadata.json"
    validation_path = tmp_path / "validation.json"
    joblib.dump(pipeline, model_path)
    meta_path.write_text(json.dumps(metadata), encoding="utf-8")
    sidecar = {
        "schema_version": 1,
        "overall_status": "PASS",
        "model_version": version,
        "artifact": {"sha256": hashlib.sha256(model_path.read_bytes()).hexdigest()},
        "metadata": {"sha256": hashlib.sha256(meta_path.read_bytes()).hexdigest()},
    }
    validation_path.write_text(json.dumps(sidecar), encoding="utf-8")
    return pipeline, model_path, meta_path, validation_path


# ---------------------------------------------------------------------------
# Import predict_v1
# ---------------------------------------------------------------------------

from predict_v1 import load_versioned_artifact, predict_complaint


# ---------------------------------------------------------------------------
# Tests: predict_complaint input validation
# ---------------------------------------------------------------------------

class TestPredictComplaintValidation:

    def test_blank_string_raises_value_error(self):
        pipeline, _, _ = _make_mock_pipeline()
        artifact = _make_artifact(pipeline)
        with pytest.raises(ValueError, match="must not be empty or blank"):
            predict_complaint("", artifact)

    def test_whitespace_only_raises_value_error(self):
        pipeline, _, _ = _make_mock_pipeline()
        artifact = _make_artifact(pipeline)
        with pytest.raises(ValueError, match="must not be empty or blank"):
            predict_complaint("   \t\n", artifact)

    def test_none_raises_type_error(self):
        pipeline, _, _ = _make_mock_pipeline()
        artifact = _make_artifact(pipeline)
        with pytest.raises(TypeError, match="must be a str"):
            predict_complaint(None, artifact)  # type: ignore

    def test_integer_raises_type_error(self):
        pipeline, _, _ = _make_mock_pipeline()
        artifact = _make_artifact(pipeline)
        with pytest.raises(TypeError, match="must be a str"):
            predict_complaint(42, artifact)  # type: ignore

    def test_list_raises_type_error(self):
        pipeline, _, _ = _make_mock_pipeline()
        artifact = _make_artifact(pipeline)
        with pytest.raises(TypeError):
            predict_complaint(["a complaint"], artifact)  # type: ignore


# ---------------------------------------------------------------------------
# Tests: probability mapping correctness
# ---------------------------------------------------------------------------

class TestProbabilityMapping:

    def test_probabilities_mapped_by_fitted_classes_order(self):
        """Verify class_probabilities uses fitted classes_ order, not LABEL_ORDER."""
        # Deliberately reverse the classes_ order
        reordered = list(reversed(LABEL_ORDER))
        pipeline, classes, probs = _make_mock_pipeline(classes_order=reordered)
        # Override pipeline's named_steps clf classes_
        pipeline.named_steps["clf"].classes_ = np.array(reordered)
        pipeline.predict_proba.return_value = np.array([probs])

        artifact = _make_artifact(pipeline)
        result = predict_complaint("A fictional land lease complaint for testing.", artifact)

        # The first class in the reordered list should map to probs[0]
        assert result["class_probabilities"][reordered[0]] == pytest.approx(float(probs[0]), abs=1e-6)
        assert result["class_probabilities"][reordered[-1]] == pytest.approx(float(probs[-1]), abs=1e-6)

    def test_probability_keys_match_classes(self):
        """All four class labels must appear as keys."""
        pipeline, classes, probs = _make_mock_pipeline()
        artifact = _make_artifact(pipeline)
        result = predict_complaint("A fictional complaint about unauthorized land allocation.", artifact)
        assert set(result["class_probabilities"].keys()) == set(LABEL_ORDER)

    def test_probabilities_are_floats(self):
        pipeline, _, probs = _make_mock_pipeline()
        artifact = _make_artifact(pipeline)
        result = predict_complaint("Test complaint.", artifact)
        for val in result["class_probabilities"].values():
            assert isinstance(val, float)
            assert np.isfinite(val)

    def test_probabilities_sum_to_approximately_one(self):
        pipeline, _, probs = _make_mock_pipeline()
        artifact = _make_artifact(pipeline)
        result = predict_complaint("Test complaint.", artifact)
        total = sum(result["class_probabilities"].values())
        assert abs(total - 1.0) < 1e-4

    def test_reordered_classes_do_not_corrupt_label_mapping(self):
        """When classes_ order differs from LABEL_ORDER, mapping must still be correct."""
        # Shuffle LABEL_ORDER
        shuffled = [LABEL_ORDER[2], LABEL_ORDER[0], LABEL_ORDER[3], LABEL_ORDER[1]]
        raw_probs = np.array([0.10, 0.60, 0.20, 0.10])
        clf_mock = MagicMock()
        clf_mock.classes_ = np.array(shuffled)
        clf_mock.predict_proba.return_value = np.array([raw_probs])
        clf_mock.predict.return_value = np.array([shuffled[1]])  # highest prob class

        pipeline = MagicMock()
        pipeline.named_steps = {"clf": clf_mock}
        pipeline.predict.return_value = np.array([shuffled[1]])
        pipeline.predict_proba.return_value = np.array([raw_probs])

        artifact = _make_artifact(pipeline)
        result = predict_complaint("A test complaint.", artifact)

        # Verify each class maps to its correct probability
        assert result["class_probabilities"][shuffled[0]] == pytest.approx(0.10, abs=1e-6)
        assert result["class_probabilities"][shuffled[1]] == pytest.approx(0.60, abs=1e-6)
        assert result["class_probabilities"][shuffled[2]] == pytest.approx(0.20, abs=1e-6)
        assert result["class_probabilities"][shuffled[3]] == pytest.approx(0.10, abs=1e-6)


# ---------------------------------------------------------------------------
# Tests: model_version returned correctly
# ---------------------------------------------------------------------------

class TestModelVersionReturned:

    def test_version_returned_in_result(self):
        pipeline, _, _ = _make_mock_pipeline()
        artifact = _make_artifact(pipeline, version="v1")
        result = predict_complaint("Test complaint.", artifact)
        assert result["model_version"] == "v1"

    def test_custom_version_returned(self):
        pipeline, _, _ = _make_mock_pipeline()
        artifact = _make_artifact(pipeline, version="v99-test")
        result = predict_complaint("Test complaint.", artifact)
        assert result["model_version"] == "v99-test"


# ---------------------------------------------------------------------------
# Tests: output fields
# ---------------------------------------------------------------------------

class TestOutputFields:

    def test_output_contains_required_keys(self):
        pipeline, _, _ = _make_mock_pipeline()
        artifact = _make_artifact(pipeline)
        result = predict_complaint("Fictional complaint.", artifact)
        for key in ["model_version", "predicted_category", "class_probabilities",
                    "advisory_note", "case_id", "closed_set_note"]:
            assert key in result, f"Missing key: {key}"

    def test_predicted_category_is_one_of_four_labels(self):
        pipeline, classes, _ = _make_mock_pipeline()
        artifact = _make_artifact(pipeline)
        result = predict_complaint("Test.", artifact)
        assert result["predicted_category"] in LABEL_ORDER

    def test_case_id_passed_through(self):
        pipeline, _, _ = _make_mock_pipeline()
        artifact = _make_artifact(pipeline)
        result = predict_complaint("Test.", artifact, case_id="TEST-001")
        assert result["case_id"] == "TEST-001"

    def test_case_id_none_by_default(self):
        pipeline, _, _ = _make_mock_pipeline()
        artifact = _make_artifact(pipeline)
        result = predict_complaint("Test.", artifact)
        assert result["case_id"] is None

    def test_closed_set_note_present(self):
        pipeline, _, _ = _make_mock_pipeline()
        artifact = _make_artifact(pipeline)
        result = predict_complaint("Test.", artifact)
        assert result["closed_set_note"]
        assert "closed" in result["closed_set_note"].lower() or "four" in result["closed_set_note"].lower()

    def test_advisory_note_present_and_nonempty(self):
        pipeline, _, _ = _make_mock_pipeline()
        artifact = _make_artifact(pipeline)
        result = predict_complaint("Test.", artifact)
        assert result["advisory_note"]


# ---------------------------------------------------------------------------
# Tests: missing artifact/metadata handling
# ---------------------------------------------------------------------------

class TestMissingArtifactHandling:

    def test_missing_joblib_raises_file_not_found(self, tmp_path):
        missing = tmp_path / "nonexistent.joblib"
        meta    = tmp_path / "meta.json"
        meta.write_text(json.dumps({"model_version": "v1"}), encoding="utf-8")
        with pytest.raises(FileNotFoundError, match="Model artifact not found"):
            load_versioned_artifact(joblib_path=missing, meta_path=meta)

    def test_missing_meta_raises_file_not_found(self, tmp_path):
        import joblib as jl
        from sklearn.pipeline import Pipeline
        from sklearn.feature_extraction.text import TfidfVectorizer
        # Write a minimal valid joblib
        pipe = Pipeline(steps=[("tfidf", TfidfVectorizer())])
        jl_path = tmp_path / "model.joblib"
        jl.dump(pipe, jl_path)
        missing_meta = tmp_path / "nonexistent.json"
        with pytest.raises(FileNotFoundError, match="Model metadata not found"):
            load_versioned_artifact(joblib_path=jl_path, meta_path=missing_meta)

    def test_metadata_without_version_raises_value_error(self, tmp_path):
        import joblib as jl
        from sklearn.pipeline import Pipeline
        from sklearn.feature_extraction.text import TfidfVectorizer
        pipe = Pipeline(steps=[("tfidf", TfidfVectorizer())])
        jl_path = tmp_path / "model.joblib"
        jl.dump(pipe, jl_path)
        meta = tmp_path / "meta.json"
        meta.write_text(json.dumps({"no_version_key": True}), encoding="utf-8")
        with pytest.raises(ValueError, match="model_version"):
            load_versioned_artifact(joblib_path=jl_path, meta_path=meta)


# ---------------------------------------------------------------------------
# Tests: save/load round-trip
# ---------------------------------------------------------------------------

class TestSaveLoadPreservation:

    def test_save_load_produces_same_prediction(self, tmp_path):
        """Train a minimal pipeline, save with joblib, load, confirm same prediction."""
        pipe, jl_path, meta_path, validation_path = _write_valid_pair(tmp_path)

        # Predict before save
        test_complaint = "The officer refused to process the file without a payment."
        pred_before = pipe.predict([test_complaint])[0]
        proba_before = pipe.predict_proba([test_complaint])[0]
        classes_before = pipe.named_steps["clf"].classes_.tolist()

        # Load
        artifact = load_versioned_artifact(
            joblib_path=jl_path,
            meta_path=meta_path,
            validation_path=validation_path,
        )
        loaded_pipe = artifact["pipeline"]

        # Predict after load
        pred_after = loaded_pipe.predict([test_complaint])[0]
        proba_after = loaded_pipe.predict_proba([test_complaint])[0]
        classes_after = loaded_pipe.named_steps["clf"].classes_.tolist()

        assert pred_before == pred_after
        assert classes_before == classes_after
        np.testing.assert_allclose(proba_before, proba_after, atol=1e-8)

    def test_load_returns_correct_model_version(self, tmp_path):
        _, jl_path, meta, validation = _write_valid_pair(tmp_path, version="v7-test")
        artifact = load_versioned_artifact(
            joblib_path=jl_path,
            meta_path=meta,
            validation_path=validation,
        )
        assert artifact["model_version"] == "v7-test"

    def test_missing_validation_sidecar_is_rejected(self, tmp_path):
        _, jl_path, meta_path, validation_path = _write_valid_pair(tmp_path)
        validation_path.unlink()
        with pytest.raises(FileNotFoundError, match="validation evidence"):
            load_versioned_artifact(
                joblib_path=jl_path,
                meta_path=meta_path,
                validation_path=validation_path,
            )

    def test_metadata_hash_mismatch_is_rejected(self, tmp_path):
        _, jl_path, meta_path, validation_path = _write_valid_pair(tmp_path)
        metadata = json.loads(meta_path.read_text(encoding="utf-8"))
        metadata["extra"] = "tampered after validation"
        meta_path.write_text(json.dumps(metadata), encoding="utf-8")
        with pytest.raises(ValueError, match="metadata hash"):
            load_versioned_artifact(
                joblib_path=jl_path,
                meta_path=meta_path,
                validation_path=validation_path,
            )

    def test_fitted_class_order_mismatch_is_rejected(self, tmp_path):
        _, jl_path, meta_path, validation_path = _write_valid_pair(
            tmp_path, fitted_order=list(reversed(sorted(LABEL_ORDER)))
        )
        with pytest.raises(ValueError, match="class order"):
            load_versioned_artifact(
                joblib_path=jl_path,
                meta_path=meta_path,
                validation_path=validation_path,
            )


# ---------------------------------------------------------------------------
# Tests: refuse to overwrite existing versioned directory
# ---------------------------------------------------------------------------

class TestRefuseOverwrite:

    def test_train_v1_raises_if_versioned_dir_exists(self, tmp_path, monkeypatch):
        """train_and_save_v1 must refuse if versioned dir already exists."""
        import train_v1

        isolated_models = tmp_path / "models"
        existing_dir = isolated_models / train_v1.MODEL_VERSION
        existing_dir.mkdir(parents=True)
        monkeypatch.setattr(train_v1, "MODELS_DIR", isolated_models)

        with pytest.raises(FileExistsError, match="Refusing to overwrite"):
            train_v1.train_and_save_v1()


# ---------------------------------------------------------------------------
# Tests: boilerplate transformer loads correctly for saved model
# ---------------------------------------------------------------------------

class TestBoilerplateTransformerImport:

    def test_boilerplate_stripper_importable_without_training(self):
        """BoilerplateStripper must be importable for joblib to deserialize saved pipelines."""
        from boilerplate_transformer import BoilerplateStripper
        bs = BoilerplateStripper()
        result = bs.transform(["Test text."])
        assert result == ["Test text."]

    def test_boilerplate_stripper_removes_phrase(self):
        from boilerplate_transformer import BoilerplateStripper, BOILERPLATE_PHRASES
        bs = BoilerplateStripper()
        text = "Some text. " + BOILERPLATE_PHRASES[0] + " More text."
        result = bs.transform([text])[0]
        assert BOILERPLATE_PHRASES[0] not in result
        assert "Some text." in result
        assert "More text." in result
