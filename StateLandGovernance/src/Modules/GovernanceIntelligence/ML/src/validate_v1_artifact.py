"""Create non-overwriting validation evidence for the existing v1 artifact.

This script validates but never retrains or rewrites the model or its metadata.
Run from the ML directory with ``python src/validate_v1_artifact.py``.
"""

from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path

import joblib

_SRC_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(_SRC_DIR))

import config
from boilerplate_transformer import BOILERPLATE_PHRASES
from data_loader import load_and_validate
from evaluate_experiment4 import _PIPELINE_CHECKED_PARAMS
from predict_v1 import (
    DEFAULT_JOBLIB,
    DEFAULT_META,
    DEFAULT_VALIDATION,
    _sha256,
    _validate_metadata,
    validate_fitted_pipeline,
)
from train_v1 import ARM_A_METRICS, ARM_A_OOF_PATH, ARM_A_RUN_META, _validate_training_references

EXPECTED_EXISTING_MODEL_SHA256 = (
    "14e91be02ef9bdb2f5e440782503c8c89dfa479d010a3fc993e651aa7a9b25c6"
)


def _normalise(value):
    if isinstance(value, tuple):
        return [_normalise(item) for item in value]
    if isinstance(value, list):
        return [_normalise(item) for item in value]
    if value is None or isinstance(value, (bool, int, float, str)):
        return value
    return repr(value)


def build_validation_sidecar() -> Path:
    for path, label in (
        (DEFAULT_JOBLIB, "v1 model"),
        (DEFAULT_META, "v1 metadata"),
        (ARM_A_RUN_META, "Arm A run metadata"),
        (ARM_A_OOF_PATH, "Arm A OOF"),
        (ARM_A_METRICS, "Arm A metrics"),
    ):
        if not path.is_file():
            raise FileNotFoundError(f"Required {label} file not found: '{path}'")
    if DEFAULT_VALIDATION.exists():
        raise FileExistsError(
            f"Validation sidecar already exists: '{DEFAULT_VALIDATION}'. Refusing to overwrite."
        )

    model_hash = _sha256(DEFAULT_JOBLIB)
    if model_hash != EXPECTED_EXISTING_MODEL_SHA256:
        raise ValueError(
            "Existing v1 model hash differs from the previously verified hash: "
            f"expected={EXPECTED_EXISTING_MODEL_SHA256}, actual={model_hash}"
        )

    metadata = json.loads(DEFAULT_META.read_text(encoding="utf-8"))
    model_version = _validate_metadata(metadata, DEFAULT_META)

    # This fixed local path is the artifact under explicit review. Loading a
    # joblib from an arbitrary or untrusted source remains unsafe.
    pipeline = joblib.load(DEFAULT_JOBLIB)
    pipeline_result = validate_fitted_pipeline(pipeline, metadata)
    dataset = load_and_validate(config.DATASET_PATH)
    reference_result = _validate_training_references(dataset, pipeline)

    model_reference = metadata.get("reference_arm_a")
    if not isinstance(model_reference, dict):
        raise ValueError("Existing model metadata is missing reference_arm_a evidence")
    if model_reference.get("oof_sha256") != reference_result["oof_sha256"]:
        raise ValueError("Existing model metadata Arm A OOF hash does not match the reference")
    if model_reference.get("metrics_sha256") != reference_result["metrics_sha256"]:
        raise ValueError("Existing model metadata Arm A metrics hash does not match the reference")
    if metadata.get("dataset", {}).get("sha256") != _sha256(config.DATASET_PATH):
        raise ValueError("Existing model metadata dataset hash does not match the original dataset")

    run_meta = json.loads(ARM_A_RUN_META.read_text(encoding="utf-8"))
    recorded_phrases = run_meta.get("pipeline", {}).get("boilerplate_phrases")
    preprocessing_check = "PASS" if recorded_phrases == list(BOILERPLATE_PHRASES) else "UNAVAILABLE"

    actual_params = pipeline.get_params(deep=True)
    stable_params = {
        key: _normalise(actual_params[key]) for key in sorted(_PIPELINE_CHECKED_PARAMS)
    }
    meta_hash = _sha256(DEFAULT_META)
    sidecar = {
        "schema_version": 1,
        "verification_timestamp_utc": datetime.now(timezone.utc).isoformat(),
        "overall_status": "PASS",
        "model_version": model_version,
        "artifact": {
            "path": str(DEFAULT_JOBLIB),
            "sha256": model_hash,
            "size_bytes": DEFAULT_JOBLIB.stat().st_size,
        },
        "metadata": {
            "path": str(DEFAULT_META),
            "sha256": meta_hash,
            "size_bytes": DEFAULT_META.stat().st_size,
        },
        "references": {
            "arm_a_run_metadata": {
                "path": str(ARM_A_RUN_META),
                "sha256": reference_result["run_metadata_sha256"],
            },
            "arm_a_oof": {
                "path": str(ARM_A_OOF_PATH),
                "sha256": reference_result["oof_sha256"],
            },
            "arm_a_metrics": {
                "path": str(ARM_A_METRICS),
                "sha256": reference_result["metrics_sha256"],
            },
            "recorded_inputs": reference_result["input_reference_sha256"],
        },
        "pipeline": {
            **pipeline_result,
            "stable_parameters": stable_params,
        },
        "checks": [
            {"name": "previously_verified_model_hash", "status": "PASS"},
            {"name": "model_metadata_pair_hashes", "status": "PASS"},
            {"name": "pipeline_steps_and_estimator_types", "status": "PASS"},
            {"name": "pipeline_fitted_state", "status": "PASS"},
            {"name": "four_class_taxonomy", "status": "PASS"},
            {"name": "fitted_class_order_matches_metadata", "status": "PASS"},
            {"name": "stable_parameters_match_arm_a_run2", "status": "PASS"},
            {"name": "dataset_ids_and_labels_match_arm_a_oof", "status": "PASS"},
            {"name": "raw_original_text_matches_candidate_baseline", "status": "PASS"},
            {"name": "recorded_reference_hashes", "status": "PASS"},
            {
                "name": "current_boilerplate_phrase_configuration_matches_recorded_metadata",
                "status": preprocessing_check,
            },
        ],
        "historical_evidence_unavailable": [
            (
                "The joblib file does not provide an immutable historical snapshot of the "
                "custom BoilerplateStripper source. Deserialization resolves the currently "
                "installed class source, so source-code identity at the 2026-10-07 training "
                "instant cannot be recovered from joblib alone."
            )
        ],
        "security_note": (
            "SHA-256 values establish consistency with this reviewed local pair; they do not "
            "make an arbitrary joblib trustworthy. Only trusted local artifacts may be loaded."
        ),
    }

    if preprocessing_check != "PASS":
        raise ValueError(
            "Current boilerplate phrase configuration does not match the phrases recorded "
            "in Arm A run metadata"
        )

    DEFAULT_VALIDATION.parent.mkdir(parents=True, exist_ok=True)
    DEFAULT_VALIDATION.write_text(
        json.dumps(sidecar, indent=2, ensure_ascii=False), encoding="utf-8"
    )
    print(f"Validation sidecar written: {DEFAULT_VALIDATION}")
    return DEFAULT_VALIDATION


if __name__ == "__main__":
    build_validation_sidecar()
