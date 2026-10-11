"""
predict_v1.py
-------------
Versioned prediction entry point for the Component 4 GovernanceIntelligence
complaint classifier (version 1).

Loads a versioned artifact explicitly by path. Accepts a nonempty English
complaint string. Returns the predicted governance category, class
probabilities mapped via the saved fitted classes_ order, and the model
version string.

Advisory disclaimer
-------------------
Output is governance classification intelligence only.
Does NOT determine guilt, legal liability, final approval, or any
administrative action. The classifier always selects one of its four
known governance categories. No validated out-of-scope detector exists.

Probability note
----------------
Probabilities are raw logistic-regression outputs. Calibration has not
been demonstrated; do not treat them as calibrated confidence intervals.

Usage (from ML/ directory)
--------------------------
    # Programmatic
    from src.predict_v1 import load_versioned_artifact, predict_complaint
    artifact = load_versioned_artifact()
    result   = predict_complaint("Some fictional complaint text", artifact)

    # CLI example
    py -3.13 src/predict_v1.py

CLI example complaint (fictional):
    "The officer reviewing my state-land lease refused to process my file
     without an unofficial payment."
"""

from __future__ import annotations

import json
import hashlib
import math
import re
import sys
from pathlib import Path
from typing import Optional

import joblib
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.linear_model import LogisticRegression
from sklearn.pipeline import FeatureUnion, Pipeline
from sklearn.utils.validation import check_is_fitted

_SRC_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(_SRC_DIR))

import config
from boilerplate_transformer import BoilerplateStripper
from config import LABEL_ORDER, MODELS_DIR

MODEL_VERSION   = "v1"
DEFAULT_JOBLIB  = MODELS_DIR / MODEL_VERSION / f"governance_classifier_{MODEL_VERSION}.joblib"
DEFAULT_META    = MODELS_DIR / MODEL_VERSION / f"model_metadata_{MODEL_VERSION}.json"
DEFAULT_VALIDATION = MODELS_DIR / MODEL_VERSION / "validation" / f"model_validation_{MODEL_VERSION}.json"

_DISCLAIMER = (
    "\n[Advisory] This output is governance classification intelligence only.\n"
    "It does NOT determine guilt, legal liability, or any final administrative action.\n"
    "The classifier always selects one of its four known governance categories.\n"
    "No validated out-of-scope detector is currently implemented.\n"
    "Probabilities are raw model outputs; calibration has not been demonstrated.\n"
)

_CLOSED_SET_NOTE = (
    "Note: This is a closed-set classifier. It will always predict one of the four "
    "known governance categories regardless of whether the input complaint belongs "
    "to one of them. No out-of-scope or 'Other' category is supported."
)


# ---------------------------------------------------------------------------
# Artifact loading
# ---------------------------------------------------------------------------

def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _read_json_object(path: Path, label: str) -> dict:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise ValueError(f"Cannot read {label} JSON '{path}': {exc}") from exc
    if not isinstance(value, dict):
        raise ValueError(f"{label} JSON '{path}' must contain an object")
    return value


def _validate_metadata(metadata: dict, meta_path: Path) -> str:
    required = {"model_version", "fitted_classes_order", "label_taxonomy"}
    missing = sorted(required - set(metadata))
    if missing:
        raise ValueError(f"Metadata file '{meta_path}' is missing required fields: {missing}")

    version = metadata["model_version"]
    if not isinstance(version, str) or not version.strip():
        raise ValueError(
            f"Metadata file '{meta_path}' must contain a nonempty string 'model_version'."
        )
    taxonomy = metadata["label_taxonomy"]
    if taxonomy != LABEL_ORDER:
        raise ValueError(
            f"Metadata label_taxonomy does not match the fixed four-class taxonomy: {taxonomy!r}"
        )
    fitted_order = metadata["fitted_classes_order"]
    if (
        not isinstance(fitted_order, list)
        or len(fitted_order) != 4
        or len(set(fitted_order)) != 4
        or set(fitted_order) != set(LABEL_ORDER)
    ):
        raise ValueError(
            "Metadata fitted_classes_order must contain each fixed taxonomy label exactly once"
        )
    if version == "v2":
        pipeline_validation = metadata.get("pipeline_validation")
        if not isinstance(pipeline_validation, dict):
            raise ValueError("V2 metadata is missing pipeline_validation evidence")
        if pipeline_validation.get("architecture") != "arm_e_word_character":
            raise ValueError("V2 metadata pipeline architecture is not the frozen Arm E architecture")
        stable_parameters = pipeline_validation.get("stable_parameters")
        if not isinstance(stable_parameters, dict) or not stable_parameters:
            raise ValueError("V2 metadata is missing stable pipeline parameters")
    return version


def _normalise_parameter(value):
    if isinstance(value, tuple):
        return [_normalise_parameter(item) for item in value]
    if isinstance(value, list):
        return [_normalise_parameter(item) for item in value]
    if value is None or isinstance(value, (bool, int, float, str)):
        return value
    return repr(value)


def validate_fitted_pipeline(pipeline, metadata: dict) -> dict:
    """Reject unexpected, unfitted, or taxonomy-incompatible pipelines."""
    model_version = metadata.get("model_version")
    is_v2 = model_version == "v2"
    valid_pipeline_type = type(pipeline) is Pipeline if is_v2 else isinstance(pipeline, Pipeline)
    if not valid_pipeline_type:
        raise ValueError(f"Model artifact must contain an sklearn Pipeline, got {type(pipeline).__name__}")

    expected_steps = ["boilerplate", "features", "clf"] if is_v2 else ["boilerplate", "tfidf", "clf"]
    actual_steps = [name for name, _ in pipeline.steps]
    if actual_steps != expected_steps:
        raise ValueError(f"Unexpected pipeline steps: expected {expected_steps}, got {actual_steps}")

    boilerplate = pipeline.named_steps["boilerplate"]
    clf = pipeline.named_steps["clf"]
    if is_v2:
        features = pipeline.named_steps["features"]
        expected_types = (
            (boilerplate, BoilerplateStripper, "boilerplate"),
            (features, FeatureUnion, "features"),
            (clf, LogisticRegression, "clf"),
        )
    else:
        tfidf = pipeline.named_steps["tfidf"]
        expected_types = (
            (boilerplate, BoilerplateStripper, "boilerplate"),
            (tfidf, TfidfVectorizer, "tfidf"),
            (clf, LogisticRegression, "clf"),
        )
    for estimator, expected_type, step_name in expected_types:
        if type(estimator) is not expected_type:
            raise ValueError(
                f"Unexpected estimator type for step '{step_name}': "
                f"expected {expected_type.__name__}, got {type(estimator).__name__}"
            )

    estimator_types = {
        "boilerplate": type(boilerplate).__name__,
        "clf": type(clf).__name__,
    }
    try:
        if is_v2:
            transformer_names = [name for name, _ in features.transformer_list]
            if transformer_names != ["word", "character"]:
                raise ValueError(
                    "Unexpected FeatureUnion transformers: expected ['word', 'character'], "
                    f"got {transformer_names}"
                )
            word = features.transformer_list[0][1]
            character = features.transformer_list[1][1]
            if type(word) is not TfidfVectorizer or type(character) is not TfidfVectorizer:
                raise ValueError("V2 word and character feature estimators must be exact TfidfVectorizer instances")
            check_is_fitted(word, attributes=["vocabulary_", "idf_"])
            check_is_fitted(character, attributes=["vocabulary_", "idf_"])
            estimator_types.update({
                "features": type(features).__name__,
                "word": type(word).__name__,
                "character": type(character).__name__,
            })
        else:
            check_is_fitted(tfidf, attributes=["vocabulary_", "idf_"])
            estimator_types["tfidf"] = type(tfidf).__name__
        check_is_fitted(clf, attributes=["classes_", "coef_", "intercept_", "n_features_in_"])
    except Exception as exc:
        raise ValueError(f"Model pipeline is not fitted: {exc}") from exc

    fitted_classes = clf.classes_.tolist()
    if len(fitted_classes) != 4 or set(fitted_classes) != set(LABEL_ORDER):
        raise ValueError(
            f"Fitted classifier classes do not match the four-class taxonomy: {fitted_classes!r}"
        )
    if fitted_classes != metadata["fitted_classes_order"]:
        raise ValueError(
            "Fitted classifier class order does not match metadata fitted_classes_order"
        )

    stable_parameters = {}
    if is_v2:
        expected_parameters = metadata["pipeline_validation"]["stable_parameters"]
        actual_parameters = pipeline.get_params(deep=True)
        missing_parameters = sorted(set(expected_parameters) - set(actual_parameters))
        if missing_parameters:
            raise ValueError(f"V2 pipeline is missing stable parameters: {missing_parameters}")
        mismatches = {}
        for key, expected in expected_parameters.items():
            actual = _normalise_parameter(actual_parameters[key])
            if actual != expected:
                mismatches[key] = {"expected": expected, "actual": actual}
        if mismatches:
            raise ValueError(f"V2 pipeline stable parameter mismatch: {mismatches}")
        stable_parameters = {
            key: _normalise_parameter(actual_parameters[key])
            for key in sorted(expected_parameters)
        }

    return {
        "steps": actual_steps,
        "estimator_types": estimator_types,
        "fitted_classes_order": fitted_classes,
        "stable_parameters": stable_parameters,
    }


def _validate_sidecar(
    sidecar: dict,
    validation_path: Path,
    joblib_path: Path,
    meta_path: Path,
    model_version: str,
) -> None:
    expected_schema = 2 if model_version == "v2" else 1
    if sidecar.get("schema_version") != expected_schema:
        raise ValueError(f"Unsupported validation sidecar schema in '{validation_path}'")
    if sidecar.get("overall_status") != "PASS":
        raise ValueError(f"Validation sidecar '{validation_path}' does not record overall_status=PASS")
    if sidecar.get("model_version") != model_version:
        raise ValueError("Validation sidecar model_version does not match model metadata")

    expected_model_hash = sidecar.get("artifact", {}).get("sha256")
    expected_meta_hash = sidecar.get("metadata", {}).get("sha256")
    if not isinstance(expected_model_hash, str) or not isinstance(expected_meta_hash, str):
        raise ValueError(
            f"Validation sidecar '{validation_path}' is missing artifact or metadata SHA-256 evidence"
        )

    actual_model_hash = _sha256(joblib_path)
    actual_meta_hash = _sha256(meta_path)
    if actual_model_hash != expected_model_hash:
        raise ValueError(
            "Model artifact hash does not match validation evidence: "
            f"expected={expected_model_hash}, actual={actual_model_hash}"
        )
    if actual_meta_hash != expected_meta_hash:
        raise ValueError(
            "Model metadata hash does not match validation evidence: "
            f"expected={expected_meta_hash}, actual={actual_meta_hash}"
        )
    if model_version == "v2":
        pipeline_evidence = sidecar.get("pipeline_validation")
        evidence_hashes = sidecar.get("evidence_hashes")
        if not isinstance(pipeline_evidence, dict) or pipeline_evidence.get("status") != "PASS":
            raise ValueError("V2 validation sidecar lacks passing pipeline validation evidence")
        if not isinstance(evidence_hashes, dict) or not evidence_hashes:
            raise ValueError("V2 validation sidecar lacks evidence hashes")

def load_versioned_artifact(
    joblib_path: Optional[Path] = None,
    meta_path: Optional[Path] = None,
    validation_path: Optional[Path] = None,
) -> dict:
    """
    Load a versioned inference artifact from explicit paths.

    Parameters
    ----------
    joblib_path : Path, optional
        Path to the joblib pipeline file. Defaults to DEFAULT_JOBLIB.
    meta_path : Path, optional
        Path to the JSON metadata file. Defaults to DEFAULT_META.

    Returns
    -------
    dict with keys:
        pipeline      - fitted sklearn Pipeline
        metadata      - dict from model_metadata_vN.json
        model_version - str version identifier
        joblib_path   - Path used
        meta_path     - Path used

    Raises
    ------
    FileNotFoundError
        If either the joblib or metadata file does not exist.
    ValueError
        If metadata, validation evidence, or the fitted pipeline is incompatible.
    """
    joblib_path = Path(joblib_path) if joblib_path is not None else DEFAULT_JOBLIB
    meta_path   = Path(meta_path)   if meta_path   is not None else DEFAULT_META
    validation_path = (
        Path(validation_path) if validation_path is not None else DEFAULT_VALIDATION
    )

    if not joblib_path.exists():
        raise FileNotFoundError(
            f"Model artifact not found: '{joblib_path}'. "
            f"Run 'py -3.13 src/train_v1.py' to generate it."
        )
    if not meta_path.exists():
        raise FileNotFoundError(
            f"Model metadata not found: '{meta_path}'. "
            f"Run 'py -3.13 src/train_v1.py' to generate it."
        )
    metadata = _read_json_object(meta_path, "model metadata")
    version = _validate_metadata(metadata, meta_path)
    if not validation_path.exists():
        raise FileNotFoundError(
            f"Model validation evidence not found: '{validation_path}'. "
            "The artifact cannot be loaded without the sidecar for the verified model/metadata pair."
        )
    sidecar = _read_json_object(validation_path, "model validation sidecar")

    # Hashes establish consistency with the locally reviewed pair. They do not
    # make an arbitrary joblib safe; only trusted local artifacts may reach load().
    _validate_sidecar(sidecar, validation_path, joblib_path, meta_path, version)

    if version == "v2" and metadata.get("model_sha256") != _sha256(joblib_path):
        raise ValueError("V2 metadata model_sha256 does not match the model artifact")

    pipeline = joblib.load(joblib_path)
    pipeline_validation = validate_fitted_pipeline(pipeline, metadata)

    return {
        "pipeline":      pipeline,
        "metadata":      metadata,
        "model_version": version,
        "joblib_path":   joblib_path,
        "meta_path":     meta_path,
        "validation_path": validation_path,
        "pipeline_validation": pipeline_validation,
    }


def load_model_version(model_version: str, models_dir: Optional[Path] = None) -> dict:
    """Load a locally configured versioned package without accepting arbitrary paths.

    The HTTP boundary continues to use v1 defaults. Deployment configuration may
    select a trusted local version explicitly in a later milestone.
    """
    if not isinstance(model_version, str) or re.fullmatch(r"v[1-9][0-9]*", model_version) is None:
        raise ValueError("model_version must use the local version format v<positive integer>")
    trusted_models_dir = Path(models_dir) if models_dir is not None else MODELS_DIR
    version_dir = trusted_models_dir / model_version
    return load_versioned_artifact(
        joblib_path=version_dir / f"governance_classifier_{model_version}.joblib",
        meta_path=version_dir / f"model_metadata_{model_version}.json",
        validation_path=version_dir / "validation" / f"model_validation_{model_version}.json",
    )


# ---------------------------------------------------------------------------
# Prediction
# ---------------------------------------------------------------------------

def predict_complaint(
    complaint_text: str,
    artifact: dict,
    case_id: Optional[str] = None,
) -> dict:
    """
    Predict the governance category for a single English complaint text.

    Parameters
    ----------
    complaint_text : str
        Nonempty English complaint narrative. Must not be blank.
        Case ID, if provided, is application context only and never
        becomes a model feature.
    artifact : dict
        Return value of load_versioned_artifact().
    case_id : str, optional
        Optional identifier for the complaint case. Used as metadata
        in the output only; never passed to the model.

    Returns
    -------
    dict with keys:
        model_version        - str
        predicted_category   - str (one of the four governance labels)
        class_probabilities  - dict[str, float] mapped via fitted classes_
        advisory_note        - str
        case_id              - str | None
        closed_set_note      - str

    Raises
    ------
    TypeError
        If complaint_text is not a string.
    ValueError
        If complaint_text is empty or blank after stripping.
    """
    if not isinstance(complaint_text, str):
        raise TypeError(
            f"complaint_text must be a str, got {type(complaint_text).__name__}."
        )
    if not complaint_text.strip():
        raise ValueError(
            "complaint_text must not be empty or blank. "
            "Provide a nonempty English complaint narrative."
        )

    pipeline = artifact["pipeline"]

    # Predict using model input only — case_id is NOT passed to the pipeline
    predicted_label = pipeline.predict([complaint_text])[0]
    proba_vector    = pipeline.predict_proba([complaint_text])[0]

    # Map probabilities using fitted classes_ order (never assume positional order)
    fitted_classes  = pipeline.named_steps["clf"].classes_.tolist()
    if len(fitted_classes) != 4 or set(fitted_classes) != set(LABEL_ORDER):
        raise ValueError("Prediction pipeline classes do not match the fixed taxonomy")
    if len(proba_vector) != 4:
        raise ValueError("Prediction pipeline must return exactly four probabilities")
    numeric_probabilities = [float(value) for value in proba_vector]
    if not all(math.isfinite(value) and 0.0 <= value <= 1.0 for value in numeric_probabilities):
        raise ValueError("Prediction probabilities must be finite and inside [0, 1]")
    if not math.isclose(sum(numeric_probabilities), 1.0, rel_tol=0.0, abs_tol=1e-10):
        raise ValueError("Prediction probabilities must sum to one")
    argmax_label = fitted_classes[max(range(4), key=numeric_probabilities.__getitem__)]
    if predicted_label != argmax_label:
        raise ValueError("Predicted category must equal probability argmax")
    class_probs = {
        cls: probability
        for cls, probability in zip(fitted_classes, numeric_probabilities)
    }

    return {
        "model_version":       artifact["model_version"],
        "predicted_category":  str(predicted_label),
        "class_probabilities": class_probs,
        "advisory_note": (
            "Governance classification intelligence only. "
            "Does NOT determine guilt, legal liability, or administrative action."
        ),
        "case_id":             case_id,
        "closed_set_note":     _CLOSED_SET_NOTE,
    }


# ---------------------------------------------------------------------------
# CLI entry point
# ---------------------------------------------------------------------------

def _run_cli() -> None:
    print()
    print("=" * 65)
    print("Component 4 - GovernanceIntelligence Classifier (v1)")
    print("BoilerplateStripper -> TF-IDF -> Logistic Regression")
    print("=" * 65)
    print(_DISCLAIMER)

    try:
        artifact = load_versioned_artifact()
    except (FileNotFoundError, ValueError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        sys.exit(1)

    meta = artifact["metadata"]
    print(f"  Model version    : {artifact['model_version']}")
    print(f"  Trained at       : {meta.get('trained_at_utc', 'N/A')}")
    print(f"  Training records : {meta.get('dataset', {}).get('row_count', 'N/A')}")
    print(f"  Fitted classes   : {artifact['pipeline'].named_steps['clf'].classes_.tolist()}")
    print()
    print("  CAUTION: Development/demonstration artifact.")
    print("  The 77% CV accuracy is from Experiment 4 Arm A (frozen folds),")
    print("  not a measured accuracy on unseen complaints.")
    print()
    print(_CLOSED_SET_NOTE)
    print()
    print("Type an English complaint and press Enter. Type 'exit' to quit.")
    print()

    while True:
        try:
            complaint = input("Enter complaint:\n> ").strip()
        except (EOFError, KeyboardInterrupt):
            print("\nExiting.")
            break

        if complaint.lower() == "exit":
            print("Exiting.")
            break

        if not complaint:
            print("(Empty input - please enter a complaint text or 'exit'.)\n")
            continue

        try:
            result = predict_complaint(complaint, artifact)
        except (TypeError, ValueError) as exc:
            print(f"ERROR: {exc}\n")
            continue

        print()
        print(f"  Predicted Governance Category: {result['predicted_category']}")
        print()
        print("  Class probabilities (sorted high to low):")
        for cls, prob in sorted(result["class_probabilities"].items(), key=lambda x: -x[1]):
            print(f"    {cls}: {prob:.4f}")
        print()
        print(f"  [Advisory] {result['advisory_note']}")
        print("-" * 65)


if __name__ == "__main__":
    _run_cli()
