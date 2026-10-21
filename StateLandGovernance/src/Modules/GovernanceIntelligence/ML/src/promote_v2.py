"""Evidence-gated, byte-preserving promotion of the Experiment 6 Arm E candidate.

The candidate is never rebuilt or serialized. Promotion fails closed and refuses
to overwrite an existing v2 directory.
"""

from __future__ import annotations

import argparse
import ast
import hashlib
import json
import math
import shutil
import subprocess
import sys
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import joblib
import numpy as np
import pandas as pd
from sklearn.metrics import (
    accuracy_score,
    balanced_accuracy_score,
    confusion_matrix,
    f1_score,
    precision_recall_fscore_support,
    precision_score,
    recall_score,
)

_SRC_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(_SRC_DIR))

import config
from evaluate_experiment6 import canonical_hash, reconstruct_frozen_arm_e_configuration
from predict_v1 import _sha256, validate_fitted_pipeline


ML_ROOT = _SRC_DIR.parent
EXP5_ROOT = ML_ROOT / "results" / "experiment5_combined_real_synthetic"
EXP6_ROOT = ML_ROOT / "results" / "experiment6_arm_e_prospective_synthetic_test"
CANDIDATE = EXP6_ROOT / "candidate" / "arm_e_candidate.joblib"
DEFAULT_OUTPUT = ML_ROOT / "models" / "v2"
EXPECTED_CANDIDATE_SHA256 = "63b37eb7175e74a8dcde82bf042fcf9dcbb4f0432fbe3b4677aac037007a20ae"
EXPECTED_SNAPSHOT_SHA256 = "41bed6d4cb40a42e8e081e5b2e90a32a9df83897891142727ec1240887f72498"
EXPECTED_CONFIGURATION_SHA256 = "a71710157a029de2df6a6329e276ceec12fc44433799960f862778d89fe68376"
LABEL_ORDER = list(config.LABEL_ORDER)
FITTED_CLASS_ORDER = sorted(LABEL_ORDER)
PROBABILITY_TOLERANCE = 1e-10

EXP6_FILES = {
    "candidate_metadata": EXP6_ROOT / "candidate" / "candidate_metadata.json",
    "candidate_sha256_record": EXP6_ROOT / "candidate" / "candidate.sha256",
    "training_manifest": EXP6_ROOT / "candidate_training_manifest.csv",
    "prospective_manifest": EXP6_ROOT / "prospective_population_manifest.csv",
    "review_snapshot": EXP6_ROOT / "frozen_review_snapshot.csv",
    "predictions": EXP6_ROOT / "predictions.csv",
    "family_predictions": EXP6_ROOT / "family_predictions.csv",
    "metrics": EXP6_ROOT / "metrics.json",
    "per_class_metrics": EXP6_ROOT / "per_class_metrics.csv",
    "confusion_matrix": EXP6_ROOT / "confusion_matrix.csv",
    "integrity_report": EXP6_ROOT / "integrity_report.json",
    "promotion_decision": EXP6_ROOT / "promotion_decision.json",
    "evaluation_metadata": EXP6_ROOT / "evaluation_metadata.json",
    "experiment6_report": EXP6_ROOT / "experiment6_report.md",
}

STABLE_PARAMETER_KEYS = (
    "features__word__analyzer", "features__word__binary",
    "features__word__lowercase", "features__word__max_df",
    "features__word__max_features", "features__word__min_df",
    "features__word__ngram_range", "features__word__norm",
    "features__word__smooth_idf", "features__word__stop_words",
    "features__word__sublinear_tf", "features__character__analyzer",
    "features__character__binary", "features__character__lowercase",
    "features__character__max_df", "features__character__max_features",
    "features__character__min_df", "features__character__ngram_range",
    "features__character__norm", "features__character__smooth_idf",
    "features__character__stop_words", "features__character__sublinear_tf",
    "clf__C", "clf__class_weight", "clf__fit_intercept", "clf__max_iter",
    "clf__multi_class", "clf__penalty", "clf__random_state", "clf__solver",
    "clf__tol",
)

EXPECTED_STABLE_PARAMETERS = {
    "features__word__analyzer": "word",
    "features__word__binary": False,
    "features__word__lowercase": True,
    "features__word__max_df": 1.0,
    "features__word__max_features": None,
    "features__word__min_df": 1,
    "features__word__ngram_range": [1, 2],
    "features__word__norm": "l2",
    "features__word__smooth_idf": True,
    "features__word__stop_words": "english",
    "features__word__sublinear_tf": True,
    "features__character__analyzer": "char_wb",
    "features__character__binary": False,
    "features__character__lowercase": True,
    "features__character__max_df": 1.0,
    "features__character__max_features": None,
    "features__character__min_df": 1,
    "features__character__ngram_range": [3, 5],
    "features__character__norm": "l2",
    "features__character__smooth_idf": True,
    "features__character__stop_words": None,
    "features__character__sublinear_tf": True,
    "clf__C": 1.0,
    "clf__class_weight": "balanced",
    "clf__fit_intercept": True,
    "clf__max_iter": 2000,
    "clf__multi_class": "auto",
    "clf__penalty": "l2",
    "clf__random_state": 42,
    "clf__solver": "lbfgs",
    "clf__tol": 0.0001,
}


def _json(path: Path) -> dict:
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"Expected JSON object: {path}")
    return value


def _csv(path: Path) -> pd.DataFrame:
    raw = path.read_bytes()
    encoding = "utf-8" if _is_utf8(raw) else "cp1252"
    return pd.read_csv(path, encoding=encoding, keep_default_na=False)


def _is_utf8(raw: bytes) -> bool:
    try:
        raw.decode("utf-8")
        return True
    except UnicodeDecodeError:
        return False


def _normalise(value: Any) -> Any:
    if isinstance(value, tuple):
        return [_normalise(item) for item in value]
    if isinstance(value, list):
        return [_normalise(item) for item in value]
    if value is None or isinstance(value, (bool, int, float, str)):
        return value
    return repr(value)


def _write_json_new(path: Path, payload: dict) -> None:
    if path.exists():
        raise FileExistsError(f"Refusing to overwrite: {path}")
    path.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def _display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ML_ROOT.resolve()).as_posix()
    except ValueError:
        return str(path.resolve())


def _assert_close(actual: float, expected: float, label: str, tolerance: float = 1e-15) -> None:
    if not math.isclose(float(actual), float(expected), rel_tol=0.0, abs_tol=tolerance):
        raise ValueError(f"{label} mismatch: expected={expected!r}, actual={actual!r}")


def _probability_column(label: str) -> str:
    return f"prob_{label.replace('/', '_').replace(' ', '_')}"


def protected_inventory() -> dict[str, str]:
    roots = (
        ML_ROOT / "data", ML_ROOT / "candidate-data", ML_ROOT / "results",
        ML_ROOT / "models" / "v1", ML_ROOT / "anomaly_detection" / "bundles",
        ML_ROOT / "anomaly_detection" / "runs", ML_ROOT / "anomaly_detection" / "evaluations",
    )
    files = {ML_ROOT / "src" / "http_service.py"}
    for root in roots:
        if root.exists():
            files.update(path for path in root.rglob("*") if path.is_file())
    return {
        path.relative_to(ML_ROOT).as_posix(): _sha256(path)
        for path in sorted(files)
    }


def validate_no_training_calls(paths: list[Path]) -> None:
    prohibited = {"fit", "fit_transform", "partial_fit"}
    for path in paths:
        tree = ast.parse(path.read_text(encoding="utf-8"), filename=str(path))
        for node in ast.walk(tree):
            if isinstance(node, ast.Call) and isinstance(node.func, ast.Attribute):
                if node.func.attr in prohibited:
                    raise ValueError(f"Training call {node.func.attr} found in {path}")


def validate_saved_evidence() -> dict:
    required = [CANDIDATE, *EXP6_FILES.values()]
    missing = [str(path) for path in required if not path.is_file()]
    if missing:
        raise FileNotFoundError(f"Missing promotion evidence: {missing}")

    candidate_hash = _sha256(CANDIDATE)
    if candidate_hash != EXPECTED_CANDIDATE_SHA256:
        raise ValueError("Candidate SHA-256 does not match the registered promotion candidate")
    recorded_digest = EXP6_FILES["candidate_sha256_record"].read_text(encoding="utf-8").split()[0]
    if recorded_digest != candidate_hash:
        raise ValueError("candidate.sha256 does not match candidate bytes")

    evaluation = _json(EXP6_FILES["evaluation_metadata"])
    integrity = _json(EXP6_FILES["integrity_report"])
    candidate_metadata = _json(EXP6_FILES["candidate_metadata"])
    saved_metrics = _json(EXP6_FILES["metrics"])
    decision = _json(EXP6_FILES["promotion_decision"])
    exp5_protocol = _json(EXP5_ROOT / "experiment5_protocol.json")
    exp5_arm_e = _json(EXP5_ROOT / "arm_e" / "run_metadata.json")
    frozen = reconstruct_frozen_arm_e_configuration(exp5_protocol, exp5_arm_e)
    configuration_hash = canonical_hash(frozen)
    if frozen != evaluation.get("configuration") or configuration_hash != EXPECTED_CONFIGURATION_SHA256:
        raise ValueError("Frozen Arm E configuration does not match Experiment 5/6 evidence")
    if evaluation.get("configuration_sha256") != configuration_hash:
        raise ValueError("Evaluation configuration hash mismatch")

    for name, evidence in evaluation.get("input_hashes", {}).items():
        evidence_path = ML_ROOT / evidence["path"]
        if _sha256(evidence_path) != evidence["sha256"]:
            raise ValueError(f"Input hash mismatch: {name}")
        if integrity.get("input_hashes", {}).get(name) != evidence:
            raise ValueError(f"Integrity/evaluation input evidence mismatch: {name}")
    snapshot_hash = _sha256(EXP6_FILES["review_snapshot"])
    if snapshot_hash != EXPECTED_SNAPSHOT_SHA256:
        raise ValueError("Frozen review snapshot hash mismatch")
    review_evidence = evaluation["input_hashes"]["prospective_corrected_review"]["sha256"]
    if snapshot_hash != review_evidence or integrity.get("frozen_review_snapshot_sha256") != snapshot_hash:
        raise ValueError("Frozen review snapshot is not byte-identical to the reviewed source")

    training = _csv(EXP6_FILES["training_manifest"])
    prospective = _csv(EXP6_FILES["prospective_manifest"])
    predictions = _csv(EXP6_FILES["predictions"])
    family_saved = _csv(EXP6_FILES["family_predictions"])
    dataset = _csv(ML_ROOT / "data" / "state_land_complaints_development_530.csv")
    exp5_population = _csv(EXP5_ROOT / "experiment5_population_manifest.csv")

    if len(training) != 425 or training["Research_ID"].nunique() != 425:
        raise ValueError("Training manifest must contain 425 unique Research_ID values")
    origin_counts = training["population_origin"].value_counts().to_dict()
    if origin_counts != {"development_synthetic": 225, "real_baseline": 200}:
        raise ValueError(f"Training population mismatch: {origin_counts}")
    development = training[training["population_origin"] == "development_synthetic"]
    if development["Synthetic_Family_ID"].nunique() != 75:
        raise ValueError("Training manifest must contain 75 development families")
    if not (development.groupby("Synthetic_Family_ID").size() == 3).all():
        raise ValueError("Every development family must contain three variants")
    if not np.allclose(development["sample_weight"].astype(float), 1.0 / 3.0, atol=1e-15, rtol=0.0):
        raise ValueError("Development synthetic sample weights differ from 1/3")
    real = training[training["population_origin"] == "real_baseline"]
    if not np.array_equal(real["sample_weight"].astype(float).to_numpy(), np.ones(200)):
        raise ValueError("Real sample weights differ from 1.0")

    if len(prospective) != 75 or prospective["Research_ID"].nunique() != 75:
        raise ValueError("Prospective manifest must contain 75 unique records")
    if prospective["Synthetic_Family_ID"].nunique() != 25:
        raise ValueError("Prospective manifest must contain 25 families")
    if not (prospective.groupby("Synthetic_Family_ID").size() == 3).all():
        raise ValueError("Every prospective family must contain three variants")
    eligibility = prospective["eligibility"].value_counts().to_dict()
    if eligibility != {"eligible": 69, "excluded": 6}:
        raise ValueError(f"Prospective eligibility mismatch: {eligibility}")

    training_ids = set(training["Research_ID"].astype(str))
    prospective_ids = set(prospective["Research_ID"].astype(str))
    training_families = set(development["Synthetic_Family_ID"].astype(str))
    prospective_families = set(prospective["Synthetic_Family_ID"].astype(str))
    if training_ids & prospective_ids:
        raise ValueError("Training/prospective Research_ID overlap")
    if training_families & prospective_families:
        raise ValueError("Training/prospective family overlap")
    exp5_families = set(
        exp5_population.loc[
            exp5_population["included_in_experiment"].astype(str).str.lower() == "true",
            "synthetic_family_id",
        ].astype(str)
    ) - {""}
    if prospective_families & exp5_families:
        raise ValueError("Prospective families entered Experiment 5")

    dataset_by_id = dataset.set_index("Research_ID")
    if not training_ids | prospective_ids <= set(dataset_by_id.index.astype(str)):
        raise ValueError("Manifest Research_ID absent from source dataset")
    training_texts = set(dataset_by_id.loc[sorted(training_ids), "Canonical_English_Text"].astype(str))
    prospective_texts = set(dataset_by_id.loc[sorted(prospective_ids), "Canonical_English_Text"].astype(str))
    if training_texts & prospective_texts:
        raise ValueError("Training/prospective exact-text overlap")
    training_origins = set(dataset_by_id.loc[sorted(training_ids), "Record_Origin"].astype(str))
    if "New source-derived candidate" in training_origins:
        raise ValueError("Source-derived records entered training")

    eligible = prospective[prospective["eligibility"] == "eligible"].copy()
    if len(predictions) != 69 or predictions["Research_ID"].nunique() != 69:
        raise ValueError("Saved predictions must contain 69 unique eligible records")
    if set(predictions["Research_ID"].astype(str)) != set(eligible["Research_ID"].astype(str)):
        raise ValueError("Saved prediction population differs from eligible manifest")
    label_map = eligible.set_index("Research_ID")["Final_Label"].astype(str).to_dict()
    if any(label_map[str(row.Research_ID)] != str(row.True_Label) for row in predictions.itertuples()):
        raise ValueError("Prediction true labels differ from frozen review labels")

    probability_columns = [_probability_column(label) for label in LABEL_ORDER]
    probability_matrix = predictions[probability_columns].astype(float).to_numpy()
    if not np.isfinite(probability_matrix).all():
        raise ValueError("Saved probabilities contain non-finite values")
    if ((probability_matrix < 0.0) | (probability_matrix > 1.0)).any():
        raise ValueError("Saved probabilities fall outside [0, 1]")
    sums = probability_matrix.sum(axis=1)
    if not np.allclose(sums, 1.0, atol=PROBABILITY_TOLERANCE, rtol=0.0):
        raise ValueError("Saved probability rows do not sum to one")
    argmax = np.asarray(LABEL_ORDER, dtype=object)[probability_matrix.argmax(axis=1)]
    if not np.array_equal(argmax, predictions["Predicted_Label"].astype(str).to_numpy()):
        raise ValueError("Saved predictions differ from probability argmax")

    true = predictions["True_Label"].astype(str)
    predicted = predictions["Predicted_Label"].astype(str)
    precision, recall, per_f1, support = precision_recall_fscore_support(
        true, predicted, labels=LABEL_ORDER, zero_division=0
    )
    recomputed = {
        "support": len(predictions),
        "correct": int((true == predicted).sum()),
        "incorrect": int((true != predicted).sum()),
        "accuracy": accuracy_score(true, predicted),
        "balanced_accuracy": balanced_accuracy_score(true, predicted),
        "macro_precision": precision_score(true, predicted, labels=LABEL_ORDER, average="macro", zero_division=0),
        "macro_recall": recall_score(true, predicted, labels=LABEL_ORDER, average="macro", zero_division=0),
        "macro_f1": f1_score(true, predicted, labels=LABEL_ORDER, average="macro", zero_division=0),
        "weighted_f1": f1_score(true, predicted, labels=LABEL_ORDER, average="weighted", zero_division=0),
    }
    for key, actual in recomputed.items():
        if isinstance(actual, (int, np.integer)):
            if int(saved_metrics[key]) != int(actual):
                raise ValueError(f"Saved metric mismatch: {key}")
        else:
            _assert_close(actual, saved_metrics[key], key)

    saved_per_class = _csv(EXP6_FILES["per_class_metrics"])
    if saved_per_class["label"].tolist() != LABEL_ORDER:
        raise ValueError("Per-class label order mismatch")
    for column, values in (("precision", precision), ("recall", recall), ("f1", per_f1)):
        if not np.allclose(saved_per_class[column].astype(float), values, atol=1e-15, rtol=0.0):
            raise ValueError(f"Per-class {column} mismatch")
    if saved_per_class["support"].astype(int).tolist() != support.astype(int).tolist():
        raise ValueError("Per-class support mismatch")
    saved_cm = _csv(EXP6_FILES["confusion_matrix"])
    recomputed_cm = confusion_matrix(true, predicted, labels=LABEL_ORDER)
    if saved_cm.columns.tolist()[1:] != LABEL_ORDER or not np.array_equal(
        saved_cm.iloc[:, 1:].astype(int).to_numpy(), recomputed_cm
    ):
        raise ValueError("Confusion matrix mismatch")

    family_rows = []
    for family_id, group in predictions.groupby("Synthetic_Family_ID", sort=True):
        counts = Counter(group["Predicted_Label"].astype(str))
        max_votes = max(counts.values())
        tied = [label for label in LABEL_ORDER if counts[label] == max_votes]
        if len(tied) != 1:
            sums_by_label = {label: float(group[_probability_column(label)].sum()) for label in tied}
            highest = max(sums_by_label.values())
            tied = [label for label in tied if math.isclose(sums_by_label[label], highest, abs_tol=1e-15)]
        majority = min(tied, key=LABEL_ORDER.index)
        family_rows.append((family_id, str(group["True_Label"].iloc[0]), majority, int((group["True_Label"] == group["Predicted_Label"]).sum()), group["Predicted_Label"].nunique() > 1))
    family = pd.DataFrame(family_rows, columns=["Synthetic_Family_ID", "True_Label", "majority", "correct_variants", "disagreement"])
    family_metrics = {
        "eligible_family_count": len(family),
        "family_majority_accuracy": accuracy_score(family["True_Label"], family["majority"]),
        "family_majority_macro_f1": f1_score(family["True_Label"], family["majority"], labels=LABEL_ORDER, average="macro", zero_division=0),
        "families_all_three_variants_correct": int((family["correct_variants"] == 3).sum()),
        "correct_variants_distribution": {str(value): int((family["correct_variants"] == value).sum()) for value in range(4)},
        "families_with_variant_prediction_disagreement": int(family["disagreement"].sum()),
        "families_with_majority_ties": int(family_saved["Majority_Tie"].astype(str).str.lower().eq("true").sum()),
    }
    if family_metrics != saved_metrics["family_metrics"]:
        raise ValueError("Family metrics mismatch")
    if family_saved["Synthetic_Family_ID"].tolist() != family["Synthetic_Family_ID"].tolist():
        raise ValueError("Family prediction IDs mismatch")
    if family_saved["Family_Majority_Prediction"].tolist() != family["majority"].tolist():
        raise ValueError("Family majority predictions mismatch")

    leakage_expected = {
        "training_test_id_overlap": 0, "training_test_family_overlap": 0,
        "training_test_exact_text_overlap": 0, "source_derived_training_records": 0,
        "prospective_records_in_training": 0, "prospective_records_in_experiment5_oof": 0,
    }
    if integrity.get("leakage_checks") != leakage_expected:
        raise ValueError("Saved leakage report does not record all-zero overlap")
    if not integrity.get("protected_inputs_unchanged") or integrity.get("changed_protected_files"):
        raise ValueError("Experiment 6 protected-input inventory did not pass")
    if not decision.get("all_criteria_passed") or decision.get("automatic_deployment_authorized"):
        raise ValueError("Promotion decision evidence is not the expected review-only PASS")
    if not all(decision.get("checks", {}).values()):
        raise ValueError("At least one saved promotion criterion failed")
    if candidate_metadata.get("fit_count") != 1:
        raise ValueError("Experiment 6 does not record exactly one candidate fit")
    if candidate_metadata.get("model_sha256") != candidate_hash or candidate_metadata.get("prospective_data_used_in_fitting") is not False:
        raise ValueError("Candidate provenance evidence mismatch")

    evidence_hashes = {
        name: _sha256(path) for name, path in EXP6_FILES.items()
    }
    evidence_hashes["candidate"] = candidate_hash
    evidence_hashes.update({f"experiment6_input:{name}": item["sha256"] for name, item in evaluation["input_hashes"].items()})
    return {
        "candidate_hash": candidate_hash,
        "snapshot_hash": snapshot_hash,
        "configuration_hash": configuration_hash,
        "evaluation": evaluation,
        "candidate_metadata": candidate_metadata,
        "metrics": saved_metrics,
        "exp5_summary": _json(EXP5_ROOT / "experiment5_summary.json"),
        "evidence_hashes": evidence_hashes,
        "checks": {
            "candidate_hash": "PASS", "configuration": "PASS", "input_hashes": "PASS",
            "populations": "PASS", "leakage": "PASS", "saved_predictions": "PASS",
            "aggregate_metrics": "PASS", "per_class_metrics": "PASS",
            "confusion_matrix": "PASS", "family_metrics": "PASS",
            "probabilities": "PASS", "promotion_decision": "PASS",
            "protected_input_inventory": "PASS", "recorded_fit_count": "PASS",
        },
    }


def _metadata(evidence: dict, pipeline_result: dict, timestamp: str) -> dict:
    evaluation = evidence["evaluation"]
    return {
        "model_id": "governance_complaint_classifier_v2",
        "model_version": "v2",
        "component": "Component 4 - GovernanceIntelligence",
        "created_at_utc": timestamp,
        "model_sha256": evidence["candidate_hash"],
        "promotion_source": "Experiment 6 Arm E already-fitted candidate; copied byte-for-byte without reserialization",
        "pipeline_description": "BoilerplateStripper -> FeatureUnion(word TF-IDF, character TF-IDF) -> LogisticRegression",
        "pipeline_validation": {
            "architecture": "arm_e_word_character",
            "steps": pipeline_result["steps"],
            "estimator_types": pipeline_result["estimator_types"],
            "stable_parameters": pipeline_result["stable_parameters"],
        },
        "label_taxonomy": LABEL_ORDER,
        "fitted_classes_order": pipeline_result["fitted_classes_order"],
        "training_population": {
            "real_baseline_records": 200,
            "reviewed_development_synthetic_records": 225,
            "reviewed_development_synthetic_families": 75,
            "total_training_rows": 425,
        },
        "sample_weight_policy": {
            "real_record": 1.0, "synthetic_variant": 1.0 / 3.0, "synthetic_family_total": 1.0,
        },
        "excluded_populations": {
            "source_derived_candidates": 30,
            "all_prospective_families": True,
            "review_decisions": ["Rejected", "Needs_Discussion"],
        },
        "experiment5_development_results": evidence["exp5_summary"]["arms"]["arm_e"],
        "experiment6_prospective_synthetic_results": evidence["metrics"],
        "evidence": {
            "review_snapshot_sha256": evidence["snapshot_hash"],
            "configuration_sha256": evidence["configuration_hash"],
            "input_hashes": evaluation["input_hashes"],
            "artifact_hashes": evidence["evidence_hashes"],
        },
        "dependency_versions": evaluation["dependency_versions"],
        "fit_count_evidence": {
            "recorded_count": evidence["candidate_metadata"]["fit_count"],
            "independently_provable_from_saved_artifact": False,
            "limitation": "The saved metadata records one fit, but the joblib and evidence do not provide an immutable invocation log proving the historical fit count independently.",
        },
        "previous_version": {"version": "v1", "status": "unchanged rollback version"},
        "advisory_purpose": "English state-land complaint category classification for subsequent human governance review.",
        "limitations": [
            "Experiment 6 is a synthetic prospective evaluation, not real-world accuracy.",
            "No independent departmental validation has been performed.",
            "Development and prospective synthetic families share a common generator origin.",
            "Closed-set four-class model with no validated out-of-scope detector.",
            "Raw logistic-regression probabilities are not demonstrated to be calibrated.",
            "Output is advisory and does not determine guilt, legal liability, final approval, or administrative action.",
        ],
        "deployment_status": "packaged for deployment review; not deployed and not the service default",
    }


def _run_fresh_process(joblib_path: Path, metadata_path: Path, validation_path: Path) -> dict:
    command = [
        sys.executable, str(_SRC_DIR / "validate_v2_fresh_process.py"),
        "--joblib", str(joblib_path), "--metadata", str(metadata_path),
        "--validation", str(validation_path), "--expected-version", "v2",
    ]
    completed = subprocess.run(command, cwd=ML_ROOT, check=True, capture_output=True, text=True)
    result = json.loads(completed.stdout)
    if result.get("status") != "PASS" or result.get("training_invoked") is not False:
        raise ValueError("Fresh-process validation did not pass")
    return result


def promote(output_dir: Path = DEFAULT_OUTPUT) -> dict:
    output_dir = Path(output_dir)
    if output_dir.exists():
        raise FileExistsError(f"Refusing to overwrite existing v2 package: {output_dir}")
    validate_no_training_calls([Path(__file__), _SRC_DIR / "validate_v2_fresh_process.py"])
    protected_before = protected_inventory()
    evidence = validate_saved_evidence()

    draft_metadata = {
        "model_version": "v2", "label_taxonomy": LABEL_ORDER,
        "fitted_classes_order": FITTED_CLASS_ORDER,
        "pipeline_validation": {
            "architecture": "arm_e_word_character",
            "stable_parameters": EXPECTED_STABLE_PARAMETERS,
        },
    }
    pipeline = joblib.load(CANDIDATE)
    pipeline_result = validate_fitted_pipeline(pipeline, draft_metadata)
    if set(pipeline_result["stable_parameters"]) != set(STABLE_PARAMETER_KEYS):
        raise ValueError("Validated stable parameter inventory is incomplete")
    smoke_probabilities = np.asarray(pipeline.predict_proba(["A fictional lease-file complaint for structural validation."]), dtype=float)
    smoke_prediction = str(pipeline.predict(["A fictional lease-file complaint for structural validation."])[0])
    if smoke_probabilities.shape != (1, 4) or not np.isfinite(smoke_probabilities).all():
        raise ValueError("Candidate did not produce four finite probabilities")
    if ((smoke_probabilities < 0.0) | (smoke_probabilities > 1.0)).any() or not np.allclose(smoke_probabilities.sum(axis=1), 1.0, atol=PROBABILITY_TOLERANCE, rtol=0.0):
        raise ValueError("Candidate probabilities violate the prediction contract")
    classes = pipeline.named_steps["clf"].classes_.tolist()
    if smoke_prediction != classes[int(smoke_probabilities[0].argmax())]:
        raise ValueError("Candidate prediction differs from probability argmax")
    del pipeline

    output_dir.mkdir(parents=True, exist_ok=False)
    validation_dir = output_dir / "validation"
    validation_dir.mkdir()
    model_path = output_dir / "governance_classifier_v2.joblib"
    metadata_path = output_dir / "model_metadata_v2.json"
    final_validation_path = validation_dir / "model_validation_v2.json"
    bootstrap_validation_path = validation_dir / ".bootstrap_validation_v2.json"
    shutil.copyfile(CANDIDATE, model_path)
    if _sha256(model_path) != evidence["candidate_hash"] or model_path.read_bytes() != CANDIDATE.read_bytes():
        raise ValueError("Promoted model is not byte-identical to the candidate")

    timestamp = datetime.now(timezone.utc).isoformat()
    metadata = _metadata(evidence, pipeline_result, timestamp)
    _write_json_new(metadata_path, metadata)
    metadata_hash = _sha256(metadata_path)
    bootstrap = {
        "schema_version": 2, "overall_status": "PASS", "model_version": "v2",
        "artifact": {"path": _display_path(model_path), "sha256": evidence["candidate_hash"], "size_bytes": model_path.stat().st_size},
        "metadata": {"path": _display_path(metadata_path), "sha256": metadata_hash, "size_bytes": metadata_path.stat().st_size},
        "pipeline_validation": {"status": "PASS", **pipeline_result},
        "evidence_hashes": evidence["evidence_hashes"],
    }
    _write_json_new(bootstrap_validation_path, bootstrap)
    fresh = _run_fresh_process(model_path, metadata_path, bootstrap_validation_path)
    bootstrap_validation_path.unlink()

    protected_after = protected_inventory()
    if protected_after != protected_before:
        changed = sorted(key for key in set(protected_before) | set(protected_after) if protected_before.get(key) != protected_after.get(key))
        raise ValueError(f"Protected sources changed during promotion: {changed}")
    sidecar = {
        "schema_version": 2,
        "verification_timestamp_utc": timestamp,
        "overall_status": "PASS",
        "model_version": "v2",
        "artifact": bootstrap["artifact"],
        "metadata": bootstrap["metadata"],
        "pipeline_validation": {"status": "PASS", **pipeline_result},
        "fresh_process_prediction": fresh,
        "evidence_validation": evidence["checks"],
        "evidence_hashes": evidence["evidence_hashes"],
        "protected_source_inventory": {
            "status": "PASS", "file_count": len(protected_before),
            "before_sha256": canonical_hash(protected_before),
            "after_sha256": canonical_hash(protected_after),
        },
        "byte_identity": {
            "status": "PASS", "source_sha256": evidence["candidate_hash"],
            "destination_sha256": _sha256(model_path), "byte_for_byte_equal": True,
            "candidate_reserialized": False,
        },
        "training_invoked_during_promotion": False,
        "limitations": metadata["limitations"] + [metadata["fit_count_evidence"]["limitation"]],
        "security_note": "Only trusted local joblib artifacts may be loaded; matching hashes do not make an untrusted pickle safe.",
    }
    _write_json_new(final_validation_path, sidecar)
    final_fresh = _run_fresh_process(model_path, metadata_path, final_validation_path)
    if final_fresh != fresh:
        raise ValueError("Final-sidecar fresh-process result differs from recorded result")
    return {
        "status": "PASS", "output_dir": str(output_dir),
        "model_sha256": _sha256(model_path), "metadata_sha256": _sha256(metadata_path),
        "validation_sha256": _sha256(final_validation_path),
        "fresh_process": final_fresh, "evidence_checks": evidence["checks"],
        "protected_source_inventory_sha256": canonical_hash(protected_after),
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-dir", type=Path, default=DEFAULT_OUTPUT)
    args = parser.parse_args()
    print(json.dumps(promote(args.output_dir), indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
