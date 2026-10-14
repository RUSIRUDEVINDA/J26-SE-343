"""
train_v1.py
-----------
Versioned training script that trains the Experiment 4 Arm A pipeline
(BoilerplateStripper -> TF-IDF -> Logistic Regression) on all 200
development records and saves a versioned artifact.

Pipeline selected: Arm A cleaned-text pipeline (build_pipeline_with_cleaner).
Arm A reference:   results/experiment4_synthetic_augmentation_run2/

Usage (from ML/ directory):
    py -3.13 src/train_v1.py

Output:
    models/v1/
        governance_classifier_v1.joblib
        model_metadata_v1.json

DEVELOPMENT / DEMONSTRATION ARTIFACT
-------------------------------------
The 77% (Arm A CV) accuracy is the cross-validation result from
Experiment 4 run2 on frozen folds, NOT a measured accuracy of this
full-data-fitted artifact on unseen complaints.

This script does NOT:
  - perform held-out evaluation
  - include synthetic or source-derived records
  - overwrite any existing model artifact
  - claim the model output is legally binding

This classifier was trained from the project dataset only.
No pretrained language model or pretrained classifier was used.
"""

from __future__ import annotations

import hashlib
import json
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

import joblib
import numpy as np
import pandas as pd
import scipy
import sklearn

_SRC_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(_SRC_DIR))

import config
from config import (
    DATASET_PATH,
    ID_COLUMN,
    LABEL_ORDER,
    MODELS_DIR,
    TARGET_COLUMN,
    TEXT_COLUMN,
)
from boilerplate_transformer import build_pipeline_with_cleaner
from data_loader import get_class_distribution, load_and_validate
from evaluate_experiment4 import (
    _PIPELINE_CHECKED_PARAMS,
    _verify_pipeline_config as verify_pipeline_against_arm_a,
)

import logging
logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(name)s - %(message)s")
logger = logging.getLogger(__name__)

# ---------------------------------------------------------------------------
# Version identifier
# ---------------------------------------------------------------------------

MODEL_VERSION = "v1"

# ---------------------------------------------------------------------------
# Arm A reference paths for provenance metadata
# ---------------------------------------------------------------------------

ARM_A_RUN_DIR  = config.RESULTS_DIR / "experiment4_synthetic_augmentation_run2"
ARM_A_OOF_PATH = ARM_A_RUN_DIR / "arm_a" / "oof_predictions.csv"
ARM_A_METRICS  = ARM_A_RUN_DIR / "arm_a" / "metrics.json"
ARM_A_RUN_META = ARM_A_RUN_DIR / "run_metadata.json"


def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _git_info() -> dict:
    try:
        commit = subprocess.check_output(
            ["git", "rev-parse", "HEAD"], stderr=subprocess.DEVNULL, text=True
        ).strip()
        porcelain = subprocess.check_output(
            ["git", "status", "--porcelain"], stderr=subprocess.DEVNULL, text=True
        )
        dirty = bool(porcelain.strip())
    except Exception:
        commit = "unavailable"
        dirty = None
    return {
        "commit": commit,
        "working_tree_clean": not dirty if dirty is not None else None,
        "note": (
            "working_tree_clean=False means uncommitted changes exist. "
            "git revision alone does not fully describe the running code."
        ),
    }


def _require_file(path: Path, label: str) -> None:
    if not path.is_file():
        raise FileNotFoundError(f"Required {label} not found: '{path}'")


def _resolve_recorded_reference(record: dict, label: str) -> Path:
    """Resolve a recorded input path, allowing relocation of the repository."""
    recorded_path = record.get("path")
    recorded_hash = record.get("sha256")
    if not isinstance(recorded_path, str) or not recorded_path.strip():
        raise ValueError(f"Arm A metadata is missing input_files.{label}.path")
    if not isinstance(recorded_hash, str) or not recorded_hash.strip():
        raise ValueError(f"Arm A metadata is missing input_files.{label}.sha256")

    path = Path(recorded_path)
    if not path.is_file():
        relocated = config.DATA_DIR / path.name
        path = relocated if relocated.is_file() else path
    _require_file(path, f"Arm A {label} reference")

    actual_hash = _sha256(path)
    if actual_hash != recorded_hash:
        raise ValueError(
            f"Arm A {label} reference hash mismatch: "
            f"recorded={recorded_hash}, actual={actual_hash}, path='{path}'"
        )
    return path


def _normalise_pipeline_value(value):
    if isinstance(value, tuple):
        return [_normalise_pipeline_value(item) for item in value]
    if isinstance(value, list):
        return [_normalise_pipeline_value(item) for item in value]
    if value is None or isinstance(value, (bool, int, float, str)):
        return value
    return repr(value)


def _validate_training_references(df: pd.DataFrame, pipeline) -> dict:
    """Validate future training inputs against the saved Arm A run evidence."""
    _require_file(ARM_A_RUN_META, "Arm A run metadata")
    _require_file(ARM_A_OOF_PATH, "Arm A OOF predictions")
    _require_file(ARM_A_METRICS, "Arm A metrics")

    try:
        run_meta = json.loads(ARM_A_RUN_META.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise ValueError(f"Cannot read Arm A run metadata '{ARM_A_RUN_META}': {exc}") from exc

    verify_pipeline_against_arm_a(ARM_A_RUN_DIR, pipeline)

    input_files = run_meta.get("input_files")
    if not isinstance(input_files, dict):
        raise ValueError("Arm A run metadata is missing the 'input_files' section")

    required_refs = (
        "candidate_dataset",
        "original_dataset",
        "frozen_fold_reference",
        "reproduction_reference",
    )
    missing_refs = [name for name in required_refs if not isinstance(input_files.get(name), dict)]
    if missing_refs:
        raise ValueError(f"Arm A run metadata is missing required references: {missing_refs}")

    resolved_refs = {
        name: _resolve_recorded_reference(input_files[name], name)
        for name in required_refs
    }

    original_record = input_files["original_dataset"]
    dataset_hash = _sha256(DATASET_PATH)
    if dataset_hash != original_record["sha256"]:
        raise ValueError(
            "Training dataset does not match the recorded original 200-row dataset: "
            f"recorded={original_record['sha256']}, actual={dataset_hash}. "
            "The 530-row candidate dataset is a distinct reference and must not be substituted."
        )

    oof = pd.read_csv(ARM_A_OOF_PATH)
    required_oof = {ID_COLUMN, "True_Label"}
    missing_oof = sorted(required_oof - set(oof.columns))
    if missing_oof:
        raise ValueError(f"Arm A OOF is missing required columns: {missing_oof}")

    for frame, frame_name in ((df, "training dataset"), (oof, "Arm A OOF")):
        ids = frame[ID_COLUMN]
        if ids.isna().any() or ids.astype(str).str.strip().eq("").any():
            raise ValueError(f"{frame_name} contains blank Research_ID values")
        if ids.astype(str).duplicated().any():
            raise ValueError(f"{frame_name} contains duplicate Research_ID values")
        if len(frame) != 200:
            raise ValueError(f"{frame_name} must contain exactly 200 rows, got {len(frame)}")

    dataset_view = df[[ID_COLUMN, TARGET_COLUMN, TEXT_COLUMN]].copy()
    dataset_view[ID_COLUMN] = dataset_view[ID_COLUMN].astype(str)
    oof_view = oof[[ID_COLUMN, "True_Label"]].copy()
    oof_view[ID_COLUMN] = oof_view[ID_COLUMN].astype(str)
    aligned_oof = dataset_view.merge(
        oof_view,
        on=ID_COLUMN,
        how="outer",
        validate="one_to_one",
        indicator=True,
    )
    if not aligned_oof["_merge"].eq("both").all():
        raise ValueError("Training dataset and Arm A OOF Research_ID sets differ")
    label_mismatches = aligned_oof[TARGET_COLUMN] != aligned_oof["True_Label"]
    if label_mismatches.any():
        bad_ids = aligned_oof.loc[label_mismatches, ID_COLUMN].tolist()
        raise ValueError(f"Training labels differ from Arm A OOF for IDs: {bad_ids[:10]}")

    candidate = pd.read_csv(resolved_refs["candidate_dataset"])
    candidate_required = {ID_COLUMN, TARGET_COLUMN, TEXT_COLUMN, "Record_Origin"}
    missing_candidate = sorted(candidate_required - set(candidate.columns))
    if missing_candidate:
        raise ValueError(f"Candidate dataset is missing required columns: {missing_candidate}")
    baseline = candidate.loc[
        candidate["Record_Origin"].eq("Existing baseline"),
        [ID_COLUMN, TARGET_COLUMN, TEXT_COLUMN],
    ].copy()
    if len(baseline) != 200 or baseline[ID_COLUMN].nunique() != 200:
        raise ValueError(
            "The recorded 530-row candidate dataset must contain exactly 200 unique "
            f"'Existing baseline' rows; found rows={len(baseline)}, "
            f"unique_ids={baseline[ID_COLUMN].nunique()}"
        )
    baseline[ID_COLUMN] = baseline[ID_COLUMN].astype(str)
    aligned_text = dataset_view.merge(
        baseline.rename(
            columns={TARGET_COLUMN: "Candidate_Label", TEXT_COLUMN: "Candidate_Original_Text"}
        ),
        on=ID_COLUMN,
        how="outer",
        validate="one_to_one",
        indicator=True,
    )
    if not aligned_text["_merge"].eq("both").all():
        raise ValueError("Original dataset and candidate baseline Research_ID sets differ")
    if (aligned_text[TARGET_COLUMN] != aligned_text["Candidate_Label"]).any():
        raise ValueError("Original dataset labels differ from the candidate baseline reference")
    if (aligned_text[TEXT_COLUMN] != aligned_text["Candidate_Original_Text"]).any():
        raise ValueError(
            "Original Canonical_English_Text differs from the matching raw candidate baseline. "
            "Raw text was compared with raw text; cleaned transformer output was not used."
        )

    reproduction_hash = input_files["reproduction_reference"]["sha256"]
    arm_a_oof_hash = _sha256(ARM_A_OOF_PATH)
    if arm_a_oof_hash != reproduction_hash:
        raise ValueError(
            "Arm A OOF no longer matches its recorded reproduction reference hash: "
            f"expected={reproduction_hash}, actual={arm_a_oof_hash}"
        )

    stored_metrics = json.loads(ARM_A_METRICS.read_text(encoding="utf-8"))
    required_metrics = {"Accuracy", "Macro_F1_sklearn", "Weighted_F1", "Misclassified"}
    missing_metrics = sorted(required_metrics - set(stored_metrics))
    if missing_metrics:
        raise ValueError(f"Arm A metrics are missing required fields: {missing_metrics}")

    return {
        "run_metadata_path": str(ARM_A_RUN_META),
        "run_metadata_sha256": _sha256(ARM_A_RUN_META),
        "oof_sha256": arm_a_oof_hash,
        "metrics_sha256": _sha256(ARM_A_METRICS),
        "input_reference_sha256": {
            name: _sha256(path) for name, path in resolved_refs.items()
        },
        "dataset_id_label_check": "PASS",
        "original_text_check": "PASS (raw original text compared with raw candidate baseline text)",
    }


def train_and_save_v1() -> None:
    logger.info("=== Component 4 GovernanceIntelligence - Versioned Training v1 ===")
    logger.info("Pipeline: Arm A (BoilerplateStripper -> TF-IDF -> Logistic Regression)")

    # ------------------------------------------------------------------ #
    # 0. Refuse to overwrite existing versioned artifact                  #
    # ------------------------------------------------------------------ #
    versioned_dir = MODELS_DIR / MODEL_VERSION
    joblib_path   = versioned_dir / f"governance_classifier_{MODEL_VERSION}.joblib"
    meta_path     = versioned_dir / f"model_metadata_{MODEL_VERSION}.json"

    if versioned_dir.exists():
        raise FileExistsError(
            f"Versioned artifact directory already exists: {versioned_dir}\n"
            f"Refusing to overwrite. Increment MODEL_VERSION to create a new artifact."
        )

    # ------------------------------------------------------------------ #
    # 1. Load and validate training dataset                               #
    # ------------------------------------------------------------------ #
    df = load_and_validate(DATASET_PATH)
    logger.info("Training dataset: %d records from '%s'", len(df), DATASET_PATH)

    if len(df) != 200:
        raise ValueError(f"Expected 200 training records, got {len(df)}")

    dataset_sha256 = _sha256(DATASET_PATH)
    class_dist     = get_class_distribution(df)
    unique_ids     = df[ID_COLUMN].nunique()
    if unique_ids != len(df):
        raise ValueError(f"Duplicate Research_IDs detected: {len(df)} rows, {unique_ids} unique IDs")

    logger.info("Dataset SHA-256: %s", dataset_sha256)
    logger.info("Class distribution: %s", class_dist)

    X = df[TEXT_COLUMN].tolist()
    y = df[TARGET_COLUMN].tolist()

    # ------------------------------------------------------------------ #
    # 2. Build and verify pipeline and immutable reference evidence       #
    # ------------------------------------------------------------------ #
    pipeline = build_pipeline_with_cleaner()
    reference_validation = _validate_training_references(df, pipeline)
    logger.info(
        "Training inputs, raw text, labels, hashes, and pipeline configuration "
        "verified against Arm A run2."
    )

    # ------------------------------------------------------------------ #
    # 3. Load Arm A reference metadata for provenance                     #
    # ------------------------------------------------------------------ #
    arm_a_metrics_stored = json.loads(ARM_A_METRICS.read_text(encoding="utf-8"))
    arm_a_ref = {
        "run_dir": str(ARM_A_RUN_DIR),
        "run_metadata_sha256": reference_validation["run_metadata_sha256"],
        "oof_sha256": reference_validation["oof_sha256"],
        "metrics_sha256": reference_validation["metrics_sha256"],
        "input_reference_sha256": reference_validation["input_reference_sha256"],
        "dataset_id_label_check": reference_validation["dataset_id_label_check"],
        "original_text_check": reference_validation["original_text_check"],
        "cv_metrics": {
            "Accuracy": arm_a_metrics_stored["Accuracy"],
            "Macro_F1": arm_a_metrics_stored["Macro_F1_sklearn"],
            "Weighted_F1": arm_a_metrics_stored["Weighted_F1"],
            "Misclassified": arm_a_metrics_stored["Misclassified"],
        },
        "cv_metrics_note": (
            "Development cross-validation results from Experiment 4 run2. "
            "200 baseline records, frozen Experiment 2/3 fold assignments. "
            "NOT the accuracy of this full-data-fitted artifact on unseen complaints."
        ),
    }

    # ------------------------------------------------------------------ #
    # 4. Train on all 200 records                                         #
    # ------------------------------------------------------------------ #
    logger.info("Training on all %d records (full development dataset)...", len(X))
    pipeline.fit(X, y)
    fitted_classes = pipeline.named_steps["clf"].classes_.tolist()
    logger.info("Training complete. Fitted classes: %s", fitted_classes)

    if set(fitted_classes) != set(LABEL_ORDER):
        raise ValueError(
            f"Fitted classes {fitted_classes} do not match expected LABEL_ORDER {LABEL_ORDER}"
        )

    # ------------------------------------------------------------------ #
    # 5. Save versioned artifact                                          #
    # ------------------------------------------------------------------ #
    versioned_dir.mkdir(parents=True, exist_ok=True)
    joblib.dump(pipeline, joblib_path)
    logger.info("Pipeline saved to '%s'", joblib_path)

    # ------------------------------------------------------------------ #
    # 6. Save model metadata                                              #
    # ------------------------------------------------------------------ #
    actual_params = pipeline.get_params(deep=True)
    stable_reference_params = {
        key: _normalise_pipeline_value(actual_params[key])
        for key in sorted(_PIPELINE_CHECKED_PARAMS)
    }

    metadata = {
        "model_id":             f"governance_complaint_classifier_{MODEL_VERSION}",
        "model_version":        MODEL_VERSION,
        "component":            "Component 4 - GovernanceIntelligence",
        "trained_at_utc":       datetime.now(timezone.utc).isoformat(),
        "artifact_path":        str(joblib_path),
        "pipeline_description": "BoilerplateStripper -> TF-IDF (unigrams+bigrams) -> Logistic Regression (L2)",
        "pipeline_factory":     "boilerplate_transformer.build_pipeline_with_cleaner()",
        "selected_arm":         "Arm A (Experiment 4 run2) - cleaned-text baseline, no synthetic augmentation",
        "dataset": {
            "path":          str(DATASET_PATH),
            "sha256":        dataset_sha256,
            "row_count":     len(df),
            "class_counts":  class_dist,
            "unique_ids":    unique_ids,
            "text_column":   TEXT_COLUMN,
            "target_column": TARGET_COLUMN,
            "note":          "All 200 'Existing baseline' records. No synthetic or source-derived records.",
        },
        "pipeline_params": {
            "source": "fitted pipeline.get_params(deep=True)",
            "tfidf_ngram_range":  list(actual_params["tfidf__ngram_range"]),
            "tfidf_stop_words":   actual_params["tfidf__stop_words"],
            "tfidf_sublinear_tf": actual_params["tfidf__sublinear_tf"],
            "tfidf_lowercase":    actual_params["tfidf__lowercase"],
            "clf_C":              actual_params["clf__C"],
            "clf_class_weight":   actual_params["clf__class_weight"],
            "clf_max_iter":       actual_params["clf__max_iter"],
            "clf_penalty":        actual_params["clf__penalty"],
            "clf_solver":         actual_params["clf__solver"],
            "clf_random_state":   actual_params["clf__random_state"],
            "stable_reference_params": stable_reference_params,
        },
        "fitted_classes_order": fitted_classes,
        "label_taxonomy":       LABEL_ORDER,
        "reference_arm_a":      arm_a_ref,
        "environment": {
            "python_version":   sys.version,
            "sklearn_version":  sklearn.__version__,
            "numpy_version":    np.__version__,
            "scipy_version":    scipy.__version__,
            "joblib_version":   joblib.__version__,
        },
        "git":                  _git_info(),
        "limitations": [
            "English-language complaint text only. No multilingual support.",
            "Closed-set 4-class classifier. No validated out-of-scope or unknown-category detector.",
            "Development/demonstration artifact trained on 200 records.",
            "No independent held-out evaluation was performed on this full-data-fitted artifact.",
            "Probabilities are raw logistic-regression outputs. Calibration not demonstrated.",
            "The 77% accuracy figure is the Experiment 4 Arm A development CV result, "
            "not a measured accuracy of this artifact on unseen complaints.",
            "Output is advisory only. Does not determine guilt, legal liability, "
            "final approval, or any administrative action.",
        ],
        "provenance": (
            "Trained from the project dataset only. "
            "No pretrained language model or pretrained classifier was used."
        ),
        "advisory_note": (
            "This model predicts the reported governance-issue category of an English "
            "state-land lease complaint. Output is advisory classification intelligence "
            "for subsequent human/governance processing. It does NOT determine guilt, "
            "legal liability, final approval, or administrative action."
        ),
    }

    meta_path.write_text(json.dumps(metadata, indent=2, ensure_ascii=False), encoding="utf-8")
    logger.info("Metadata saved to '%s'", meta_path)

    # ------------------------------------------------------------------ #
    # 7. Console summary                                                  #
    # ------------------------------------------------------------------ #
    print()
    print("=" * 65)
    print("VERSIONED TRAINING COMPLETE")
    print("=" * 65)
    print(f"  Version          : {MODEL_VERSION}")
    print(f"  Training rows    : {len(df)}")
    print(f"  Fitted classes   : {fitted_classes}")
    print(f"  Class counts     : {class_dist}")
    print(f"  Dataset SHA-256  : {dataset_sha256}")
    print(f"  Artifact         : {joblib_path}")
    print(f"  Metadata         : {meta_path}")
    if arm_a_ref.get("cv_metrics"):
        m = arm_a_ref["cv_metrics"]
        print(f"  Arm A CV Acc     : {m['Accuracy']}  (dev CV, NOT held-out)")
        print(f"  Arm A CV F1      : {m['Macro_F1']}")
    print()
    print("  CAUTION: Development/demonstration artifact.")
    print("           No independent held-out evaluation performed.")
    print("           Probabilities not demonstrated to be calibrated.")
    print("=" * 65)


if __name__ == "__main__":
    train_and_save_v1()
