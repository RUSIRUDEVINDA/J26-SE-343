"""Leakage-safe combined real + synthetic development evaluation (Experiment 5).

This module deliberately keeps the frozen real Combined_Group folds and the
reviewed synthetic-family folds independent.  It creates development OOF
evidence only; it never trains or promotes a production model.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import subprocess
from collections import Counter
from pathlib import Path
from typing import Any, Iterable

import numpy as np
import pandas as pd
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import (
    accuracy_score,
    balanced_accuracy_score,
    confusion_matrix,
    f1_score,
    precision_recall_fscore_support,
    precision_score,
    recall_score,
)
from sklearn.model_selection import StratifiedKFold
from sklearn.pipeline import FeatureUnion, Pipeline

import config
from boilerplate_transformer import BoilerplateStripper, build_pipeline_with_cleaner


ML_ROOT = Path(__file__).resolve().parent.parent
DATASET_PATH = ML_ROOT / "data" / "state_land_governance_confirmed_200.csv"
CANDIDATE_PATH = ML_ROOT / "data" / "state_land_complaints_development_530.csv"
REVIEW_PATH = ML_ROOT / "candidate-data" / "v1" / "synthetic_family_review_pack_confirmed_20261007.csv"
REAL_FOLD_PATH = ML_ROOT / "results" / "experiment4_synthetic_augmentation_run2" / "baseline_fold_manifest.csv"
ARM_A_REFERENCE_PATH = ML_ROOT / "results" / "experiment4_synthetic_augmentation_run2" / "arm_a" / "oof_predictions.csv"
ARM_B_REFERENCE_PATH = ML_ROOT / "results" / "experiment4_synthetic_augmentation_run5" / "arm_b" / "oof_predictions.csv"
DEFAULT_OUTPUT_DIR = ML_ROOT / "results" / "experiment5_combined_real_synthetic"

ID_COLUMN = config.ID_COLUMN
TEXT_COLUMN = config.TEXT_COLUMN
TARGET_COLUMN = config.TARGET_COLUMN
GROUP_COLUMN = config.GROUP_COLUMN
LABEL_ORDER = list(config.LABEL_ORDER)
RANDOM_STATE = config.RANDOM_STATE
N_FOLDS = config.CV_N_SPLITS
PROBABILITY_TOLERANCE = 1e-10
ARM_A_REPRODUCTION_TOLERANCE = 0.0001

BASELINE_ORIGIN = "Existing baseline"
SOURCE_ORIGIN = "New source-derived candidate"
SYNTHETIC_ORIGIN = "New synthetic scenario"
ARMS = ("arm_a", "arm_b", "arm_c", "arm_d", "arm_e")


def probability_column(label: str) -> str:
    return f"prob_{label.replace('/', '_').replace(' ', '_')}"


PROBABILITY_COLUMNS = [probability_column(label) for label in LABEL_ORDER]


def sha256_file(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _json_default(value: Any) -> Any:
    if isinstance(value, (np.integer,)):
        return int(value)
    if isinstance(value, (np.floating,)):
        return float(value)
    if isinstance(value, Path):
        return str(value)
    raise TypeError(f"Cannot JSON-encode {type(value).__name__}")


def write_json(path: Path, payload: Any) -> None:
    path.write_text(json.dumps(payload, indent=2, default=_json_default) + "\n", encoding="utf-8")


def ensure_new_output_directory(path: Path) -> None:
    if path.exists() and any(path.iterdir()):
        raise FileExistsError(f"Experiment 5 output directory is non-empty; refusing to overwrite: {path}")
    path.mkdir(parents=True, exist_ok=True)


def _require_columns(df: pd.DataFrame, required: Iterable[str], source: str) -> None:
    missing = set(required) - set(df.columns)
    if missing:
        raise ValueError(f"{source} missing required columns: {sorted(missing)}")


def validate_unique_ids(df: pd.DataFrame, source: str) -> None:
    _require_columns(df, [ID_COLUMN], source)
    blank = df[ID_COLUMN].isna() | df[ID_COLUMN].astype(str).str.strip().eq("")
    if blank.any():
        raise ValueError(f"{source} contains {int(blank.sum())} missing/blank Research_ID values")
    duplicate = df[ID_COLUMN].duplicated(keep=False)
    if duplicate.any():
        ids = sorted(df.loc[duplicate, ID_COLUMN].astype(str).unique())[:5]
        raise ValueError(f"{source} contains duplicate Research_ID values: {ids}")


def _normalise_variant(value: Any) -> tuple[int, str]:
    text = str(value).strip()
    digits = "".join(ch for ch in text if ch.isdigit())
    return (int(digits) if digits else 10**9, text)


def select_one_variant_per_family(synthetic_train: pd.DataFrame) -> pd.DataFrame:
    """Select the lowest variant deterministically, without model outcomes."""
    if synthetic_train.empty:
        return synthetic_train.copy()
    selected_indices: list[int] = []
    for _, family in synthetic_train.groupby("Synthetic_Family_ID", sort=True):
        ordered = sorted(family.index, key=lambda idx: (_normalise_variant(family.at[idx, "Synthetic_Variant"]), str(family.at[idx, ID_COLUMN])))
        selected_indices.append(ordered[0])
    return synthetic_train.loc[selected_indices].sort_values("Synthetic_Family_ID").reset_index(drop=True)


def family_balanced_weights(n_real: int, synthetic_rows: pd.DataFrame) -> np.ndarray:
    """Give every real row weight 1 and each synthetic family total weight 1."""
    real_weights = np.ones(n_real, dtype=float)
    if synthetic_rows.empty:
        return real_weights
    counts = synthetic_rows.groupby("Synthetic_Family_ID")[ID_COLUMN].transform("count").to_numpy(dtype=float)
    return np.concatenate([real_weights, 1.0 / counts])


def assign_synthetic_family_folds(synthetic: pd.DataFrame, seed: int = RANDOM_STATE) -> dict[str, int]:
    """Assign whole families to deterministic label-stratified folds."""
    _require_columns(synthetic, ["Synthetic_Family_ID", TARGET_COLUMN], "approved synthetic population")
    labels_per_family = synthetic.groupby("Synthetic_Family_ID")[TARGET_COLUMN].nunique()
    bad = labels_per_family[labels_per_family != 1]
    if not bad.empty:
        raise ValueError(f"Approved families with multiple labels: {bad.index.tolist()[:5]}")
    families = (
        synthetic.groupby("Synthetic_Family_ID", as_index=False)[TARGET_COLUMN]
        .first()
        .sort_values("Synthetic_Family_ID")
        .reset_index(drop=True)
    )
    if families.empty:
        raise ValueError("No approved synthetic families are available")
    class_counts = families[TARGET_COLUMN].value_counts()
    if int(class_counts.min()) < N_FOLDS:
        raise ValueError(f"Cannot place every synthetic class in every fold; family counts are {class_counts.to_dict()}")
    splitter = StratifiedKFold(n_splits=N_FOLDS, shuffle=True, random_state=seed)
    fold_map: dict[str, int] = {}
    dummy = np.zeros(len(families))
    for fold, (_, eval_indices) in enumerate(splitter.split(dummy, families[TARGET_COLUMN]), start=1):
        for idx in eval_indices:
            fold_map[str(families.at[idx, "Synthetic_Family_ID"])] = fold
    if set(fold_map) != set(families["Synthetic_Family_ID"].astype(str)):
        raise AssertionError("Synthetic fold assignment is incomplete")
    return fold_map


def validate_no_train_evaluation_overlap(
    real_train: pd.DataFrame,
    real_eval: pd.DataFrame,
    synthetic_train: pd.DataFrame,
    synthetic_eval: pd.DataFrame,
) -> None:
    if set(real_train[ID_COLUMN]) & set(real_eval[ID_COLUMN]):
        raise ValueError("Real Research_ID train/evaluation overlap detected")
    if set(real_train["Combined_Group"]) & set(real_eval["Combined_Group"]):
        raise ValueError("Real Combined_Group train/evaluation overlap detected")
    if set(synthetic_train[ID_COLUMN]) & set(synthetic_eval[ID_COLUMN]):
        raise ValueError("Synthetic Research_ID train/evaluation overlap detected")
    if set(synthetic_train["Synthetic_Family_ID"]) & set(synthetic_eval["Synthetic_Family_ID"]):
        raise ValueError("Synthetic family train/evaluation overlap detected")


def build_pipeline(arm: str) -> Pipeline:
    if arm != "arm_e":
        return build_pipeline_with_cleaner()
    word = TfidfVectorizer(**config.TFIDF_CONFIG)
    char = TfidfVectorizer(
        analyzer="char_wb",
        ngram_range=(3, 5),
        lowercase=True,
        sublinear_tf=True,
        min_df=1,
    )
    classifier = LogisticRegression(**config.LR_CONFIG)
    return Pipeline([
        ("boilerplate", BoilerplateStripper()),
        ("features", FeatureUnion([("word", word), ("character", char)])),
        ("clf", classifier),
    ])


def fit_pipeline(
    pipeline: Any,
    texts: list[str],
    labels: list[str],
    sample_weights: np.ndarray | None = None,
) -> Any:
    """Fit a sklearn pipeline, routing optional weights only to its classifier."""
    kwargs = {} if sample_weights is None else {"clf__sample_weight": sample_weights}
    return pipeline.fit(texts, labels, **kwargs)


def align_probabilities(classes: Iterable[str], probabilities: np.ndarray) -> np.ndarray:
    classes = [str(value) for value in classes]
    if set(classes) != set(LABEL_ORDER):
        raise ValueError(f"Classifier classes do not match the four-class taxonomy: {classes}")
    class_index = {label: index for index, label in enumerate(classes)}
    return probabilities[:, [class_index[label] for label in LABEL_ORDER]]


def validate_probabilities(probabilities: np.ndarray, predicted: Iterable[str]) -> None:
    matrix = np.asarray(probabilities, dtype=float)
    if matrix.ndim != 2 or matrix.shape[1] != len(LABEL_ORDER):
        raise ValueError(f"Probability matrix must have four columns; got {matrix.shape}")
    if not np.isfinite(matrix).all():
        raise ValueError("Probability matrix contains non-finite values")
    if ((matrix < 0.0) | (matrix > 1.0)).any():
        raise ValueError("Probability matrix contains values outside [0, 1]")
    if not np.allclose(matrix.sum(axis=1), 1.0, atol=PROBABILITY_TOLERANCE, rtol=0.0):
        raise ValueError("Probability rows do not sum to one within tolerance")
    argmax_labels = np.asarray(LABEL_ORDER, dtype=object)[np.argmax(matrix, axis=1)]
    if not np.array_equal(argmax_labels, np.asarray(list(predicted), dtype=object)):
        raise ValueError("Predicted label does not equal the maximum-probability class")


def _predict_rows(pipeline: Pipeline, frame: pd.DataFrame, population: str) -> pd.DataFrame:
    texts = frame[TEXT_COLUMN].astype(str).tolist()
    predicted = pipeline.predict(texts)
    raw = pipeline.predict_proba(texts)
    probabilities = align_probabilities(pipeline.named_steps["clf"].classes_, raw)
    validate_probabilities(probabilities, predicted)
    result = pd.DataFrame({
        ID_COLUMN: frame[ID_COLUMN].astype(str).to_numpy(),
        "population_origin": population,
        "source_group": frame.get("Combined_Group", pd.Series([""] * len(frame))).fillna("").astype(str).to_numpy(),
        "synthetic_family_id": frame.get("Synthetic_Family_ID", pd.Series([""] * len(frame))).fillna("").astype(str).to_numpy(),
        "synthetic_variant": frame.get("Synthetic_Variant", pd.Series([""] * len(frame))).fillna("").astype(str).to_numpy(),
        "True_Label": frame[TARGET_COLUMN].astype(str).to_numpy(),
        "Predicted_Label": predicted,
        "Fold": frame["evaluation_fold"].astype(int).to_numpy(),
    })
    for index, column in enumerate(PROBABILITY_COLUMNS):
        result[column] = probabilities[:, index]
    return result


def calculate_metrics(oof: pd.DataFrame) -> tuple[dict[str, Any], pd.DataFrame, pd.DataFrame]:
    if oof.empty:
        raise ValueError("Cannot calculate metrics for an empty OOF population")
    true = oof["True_Label"].tolist()
    predicted = oof["Predicted_Label"].tolist()
    precision, recall, f1, support = precision_recall_fscore_support(
        true, predicted, labels=LABEL_ORDER, zero_division=0
    )
    cm = confusion_matrix(true, predicted, labels=LABEL_ORDER)
    per_class = pd.DataFrame({
        "label": LABEL_ORDER,
        "precision": precision,
        "recall": recall,
        "f1": f1,
        "support": support.astype(int),
        "precision_defined": cm.sum(axis=0) > 0,
        "recall_defined": cm.sum(axis=1) > 0,
        "f1_defined": (cm.sum(axis=0) + cm.sum(axis=1)) > 0,
    })
    correct = int((oof["True_Label"] == oof["Predicted_Label"]).sum())
    metrics = {
        "support": int(len(oof)),
        "correct": correct,
        "incorrect": int(len(oof) - correct),
        "accuracy": float(accuracy_score(true, predicted)),
        "balanced_accuracy": float(balanced_accuracy_score(true, predicted)),
        "macro_precision": float(precision_score(true, predicted, labels=LABEL_ORDER, average="macro", zero_division=0)),
        "macro_recall": float(recall_score(true, predicted, labels=LABEL_ORDER, average="macro", zero_division=0)),
        "macro_f1": float(f1_score(true, predicted, labels=LABEL_ORDER, average="macro", zero_division=0)),
        "weighted_f1": float(f1_score(true, predicted, labels=LABEL_ORDER, average="weighted", zero_division=0)),
        "all_class_metrics_defined": bool(per_class[["precision_defined", "recall_defined", "f1_defined"]].to_numpy().all()),
        "no_zero_recall_class": bool((per_class["recall"] > 0).all()),
    }
    cm_frame = pd.DataFrame(cm, index=LABEL_ORDER, columns=LABEL_ORDER)
    cm_frame.index.name = "true_label"
    return metrics, per_class, cm_frame


def calculate_family_metrics(synthetic_oof: pd.DataFrame) -> dict[str, Any]:
    required = ["synthetic_family_id", "True_Label", "Predicted_Label", *PROBABILITY_COLUMNS]
    _require_columns(synthetic_oof, required, "synthetic OOF")
    rows: list[dict[str, Any]] = []
    for family_id, family in synthetic_oof.groupby("synthetic_family_id", sort=True):
        if family["True_Label"].nunique() != 1:
            raise ValueError(f"Synthetic OOF family {family_id} has multiple true labels")
        counts = Counter(family["Predicted_Label"])
        max_votes = max(counts.values())
        tied = [label for label in LABEL_ORDER if counts[label] == max_votes]
        if len(tied) == 1:
            majority = tied[0]
        else:
            probability_sums = {label: float(family[probability_column(label)].sum()) for label in tied}
            majority = max(tied, key=lambda label: (probability_sums[label], -LABEL_ORDER.index(label)))
        correct_variants = int((family["True_Label"] == family["Predicted_Label"]).sum())
        rows.append({
            "synthetic_family_id": family_id,
            "true_label": family["True_Label"].iloc[0],
            "majority_prediction": majority,
            "correct_variants": correct_variants,
        })
    family_frame = pd.DataFrame(rows)
    distribution = family_frame["correct_variants"].value_counts().reindex(range(4), fill_value=0).sort_index()
    return {
        "family_support": int(len(family_frame)),
        "family_majority_accuracy": float(accuracy_score(family_frame["true_label"], family_frame["majority_prediction"])),
        "family_majority_macro_f1": float(f1_score(family_frame["true_label"], family_frame["majority_prediction"], labels=LABEL_ORDER, average="macro", zero_division=0)),
        "families_all_three_variants_correct": int((family_frame["correct_variants"] == 3).sum()),
        "correct_variants_distribution": {str(index): int(value) for index, value in distribution.items()},
    }


def paired_comparison(reference: pd.DataFrame, candidate: pd.DataFrame) -> tuple[dict[str, Any], pd.DataFrame]:
    required = [ID_COLUMN, "True_Label", "Predicted_Label", "Fold"]
    for name, frame in (("reference", reference), ("candidate", candidate)):
        _require_columns(frame, required, name)
        validate_unique_ids(frame, name)
    if set(reference[ID_COLUMN]) != set(candidate[ID_COLUMN]):
        raise ValueError("Paired comparison Research_ID sets differ")
    merged = reference[required].rename(columns={"Predicted_Label": "Arm_A_Pred"}).merge(
        candidate[required].rename(columns={"True_Label": "True_Label_Candidate", "Predicted_Label": "Candidate_Pred", "Fold": "Fold_Candidate"}),
        on=ID_COLUMN,
        validate="one_to_one",
    )
    if not merged["True_Label"].equals(merged["True_Label_Candidate"]):
        raise ValueError("Paired comparison true label mismatch")
    if not merged["Fold"].equals(merged["Fold_Candidate"]):
        raise ValueError("Paired comparison fold mismatch")
    merged["Arm_A_Correct"] = merged["True_Label"] == merged["Arm_A_Pred"]
    merged["Candidate_Correct"] = merged["True_Label"] == merged["Candidate_Pred"]
    merged["Transition"] = np.select(
        [
            ~merged["Arm_A_Correct"] & merged["Candidate_Correct"],
            merged["Arm_A_Correct"] & ~merged["Candidate_Correct"],
            merged["Arm_A_Correct"] & merged["Candidate_Correct"],
            (merged["Arm_A_Pred"] == merged["Candidate_Pred"]),
        ],
        ["helped", "hurt", "both_correct", "both_wrong_same_prediction"],
        default="both_wrong_different_predictions",
    )
    helped = int((merged["Transition"] == "helped").sum())
    hurt = int((merged["Transition"] == "hurt").sum())
    per_class_changes = {}
    for label in LABEL_ORDER:
        subset = merged[merged["True_Label"] == label]
        per_class_changes[label] = int(subset["Candidate_Correct"].sum() - subset["Arm_A_Correct"].sum())
    summary = {
        "support": int(len(merged)),
        "helped": helped,
        "hurt": hurt,
        "net_gain": helped - hurt,
        "both_correct": int((merged["Transition"] == "both_correct").sum()),
        "both_wrong_same_prediction": int((merged["Transition"] == "both_wrong_same_prediction").sum()),
        "both_wrong_different_predictions": int((merged["Transition"] == "both_wrong_different_predictions").sum()),
        "prediction_changes": int((merged["Arm_A_Pred"] != merged["Candidate_Pred"]).sum()),
        "per_class_correct_count_changes": per_class_changes,
    }
    return summary, merged


def _reference_metrics(frame: pd.DataFrame) -> dict[str, float]:
    return {
        "accuracy": float(accuracy_score(frame["True_Label"], frame["Predicted_Label"])),
        "macro_f1": float(f1_score(frame["True_Label"], frame["Predicted_Label"], labels=LABEL_ORDER, average="macro", zero_division=0)),
    }


def _protected_inventory() -> dict[str, str]:
    files: set[Path] = {DATASET_PATH, CANDIDATE_PATH, REVIEW_PATH}
    for root in [ML_ROOT / "models" / "v1", ML_ROOT / "anomaly_detection" / "bundles", ML_ROOT / "anomaly_detection" / "runs", ML_ROOT / "anomaly_detection" / "evaluations"]:
        if root.exists():
            files.update(path for path in root.rglob("*") if path.is_file())
    results_root = ML_ROOT / "results"
    files.update(path for path in results_root.rglob("*") if path.is_file() and "experiment5_" not in str(path.relative_to(results_root)).replace("\\", "/"))
    return {str(path.relative_to(ML_ROOT)).replace("\\", "/"): sha256_file(path) for path in sorted(files)}


def validate_inputs() -> dict[str, Any]:
    input_paths = {
        "original_baseline": DATASET_PATH,
        "candidate_dataset": CANDIDATE_PATH,
        "confirmed_review_pack": REVIEW_PATH,
        "experiment4_arm_a_oof": ARM_A_REFERENCE_PATH,
        "experiment4_arm_b_oof": ARM_B_REFERENCE_PATH,
        "experiment4_real_fold_manifest": REAL_FOLD_PATH,
        "model_v1_metadata": ML_ROOT / "models" / "v1" / "model_metadata_v1.json",
        "model_v1_validation": ML_ROOT / "models" / "v1" / "validation" / "model_validation_v1.json",
        "model_v1_artifact": ML_ROOT / "models" / "v1" / "governance_classifier_v1.joblib",
    }
    missing = [str(path) for path in input_paths.values() if not path.is_file()]
    if missing:
        raise FileNotFoundError(f"Required Experiment 5 inputs are missing: {missing}")
    hashes = {name: {"path": str(path.relative_to(ML_ROOT)).replace("\\", "/"), "sha256": sha256_file(path)} for name, path in input_paths.items()}

    original = pd.read_csv(DATASET_PATH)
    candidate = pd.read_csv(CANDIDATE_PATH)
    review = pd.read_csv(REVIEW_PATH)
    real_folds = pd.read_csv(REAL_FOLD_PATH)
    arm_a_reference = pd.read_csv(ARM_A_REFERENCE_PATH)
    arm_b_reference = pd.read_csv(ARM_B_REFERENCE_PATH)

    for name, frame in (("original baseline", original), ("candidate dataset", candidate)):
        _require_columns(frame, [ID_COLUMN, TEXT_COLUMN, TARGET_COLUMN, GROUP_COLUMN], name)
        validate_unique_ids(frame, name)
    _require_columns(candidate, ["Record_Origin", "Synthetic_Family_ID", "Synthetic_Variant"], "candidate dataset")
    _require_columns(review, ["Synthetic_Family_ID", "Proposed_Label", "Review_Decision"], "review pack")

    origin_counts = candidate["Record_Origin"].value_counts().to_dict()
    expected_origins = {BASELINE_ORIGIN: 200, SOURCE_ORIGIN: 30, SYNTHETIC_ORIGIN: 300}
    if origin_counts != expected_origins:
        raise ValueError(f"Candidate origin composition mismatch: expected {expected_origins}, found {origin_counts}")
    baseline = candidate[candidate["Record_Origin"] == BASELINE_ORIGIN].copy()
    source_candidates = candidate[candidate["Record_Origin"] == SOURCE_ORIGIN].copy()
    all_synthetic = candidate[candidate["Record_Origin"] == SYNTHETIC_ORIGIN].copy()
    compare_columns = [ID_COLUMN, TEXT_COLUMN, TARGET_COLUMN, GROUP_COLUMN]
    left = baseline[compare_columns].sort_values(ID_COLUMN).reset_index(drop=True)
    right = original[compare_columns].sort_values(ID_COLUMN).reset_index(drop=True)
    if not left.equals(right):
        raise ValueError("Candidate baseline IDs/texts/labels/source groups do not exactly match the original 200-row dataset")

    review_ids = review["Synthetic_Family_ID"].astype(str)
    if review_ids.duplicated().any() or review_ids.str.strip().eq("").any():
        raise ValueError("Review pack Synthetic_Family_ID values must be nonblank and unique")
    synthetic_family_ids = set(all_synthetic["Synthetic_Family_ID"].astype(str))
    if set(review_ids) != synthetic_family_ids:
        raise ValueError("Review pack family IDs do not exactly match candidate synthetic families")
    decision = review["Review_Decision"].fillna("").astype(str).str.strip()
    if set(decision) - {"", "Approved"}:
        raise ValueError(f"Unexpected review decisions: {sorted(set(decision) - {'', 'Approved'})}")
    approved_review = review[decision == "Approved"].copy()
    unreviewed_review = review[decision == ""].copy()
    if len(approved_review) != 75 or len(unreviewed_review) != 25:
        raise ValueError(f"Review composition mismatch: approved={len(approved_review)}, unreviewed={len(unreviewed_review)}")
    approved_ids = set(approved_review["Synthetic_Family_ID"].astype(str))
    approved = all_synthetic[all_synthetic["Synthetic_Family_ID"].astype(str).isin(approved_ids)].copy()
    if len(approved) != 225:
        raise ValueError(f"Approved-family record count must be 225; found {len(approved)}")
    family_sizes = approved.groupby("Synthetic_Family_ID").size()
    variant_counts = approved.groupby("Synthetic_Family_ID")["Synthetic_Variant"].nunique()
    label_counts = approved.groupby("Synthetic_Family_ID")[TARGET_COLUMN].nunique()
    if not family_sizes.eq(3).all() or not variant_counts.eq(3).all():
        raise ValueError("Every approved family must have exactly three unique variants")
    if not label_counts.eq(1).all():
        raise ValueError("An approved family has multiple labels")
    proposed = approved_review.set_index("Synthetic_Family_ID")["Proposed_Label"].astype(str)
    actual = approved.groupby("Synthetic_Family_ID")[TARGET_COLUMN].first().astype(str)
    if not actual.sort_index().equals(proposed.sort_index()):
        raise ValueError("Approved family proposed labels do not match candidate labels")
    selected = pd.concat([baseline, approved], ignore_index=True)
    validate_unique_ids(selected, "selected Experiment 5 population")
    if selected[TEXT_COLUMN].duplicated(keep=False).any():
        raise ValueError("Exact duplicate text exists in selected Experiment 5 populations")

    _require_columns(real_folds, [ID_COLUMN, "Combined_Group", "Evaluation_Fold"], "Experiment 4 real fold manifest")
    validate_unique_ids(real_folds, "Experiment 4 real fold manifest")
    if set(real_folds[ID_COLUMN]) != set(baseline[ID_COLUMN]):
        raise ValueError("Frozen real fold manifest does not contain exactly the baseline IDs")
    real_folds["Evaluation_Fold"] = pd.to_numeric(real_folds["Evaluation_Fold"], errors="raise").astype(int)
    if set(real_folds["Evaluation_Fold"]) != set(range(1, N_FOLDS + 1)):
        raise ValueError("Frozen real folds are not exactly 1..5")
    if real_folds["Combined_Group"].isna().any() or real_folds["Combined_Group"].astype(str).str.strip().eq("").any():
        raise ValueError("Frozen real fold manifest contains blank Combined_Group values")
    if (real_folds.groupby("Combined_Group")["Evaluation_Fold"].nunique() != 1).any():
        raise ValueError("A frozen real Combined_Group spans evaluation folds")
    baseline = baseline.merge(real_folds[[ID_COLUMN, "Combined_Group", "Evaluation_Fold"]], on=ID_COLUMN, validate="one_to_one")
    baseline = baseline.rename(columns={"Evaluation_Fold": "evaluation_fold"})
    if not baseline["Combined_Group"].equals(baseline["Combined_Group"].astype(str)):
        baseline["Combined_Group"] = baseline["Combined_Group"].astype(str)

    synthetic_fold_map = assign_synthetic_family_folds(approved)
    approved["evaluation_fold"] = approved["Synthetic_Family_ID"].astype(str).map(synthetic_fold_map).astype(int)
    if (approved.groupby("Synthetic_Family_ID")["evaluation_fold"].nunique() != 1).any():
        raise ValueError("An approved synthetic family spans folds")
    fold_class_counts = approved.drop_duplicates("Synthetic_Family_ID").groupby("evaluation_fold")[TARGET_COLUMN].nunique()
    if not fold_class_counts.eq(len(LABEL_ORDER)).all():
        raise ValueError(f"A synthetic fold lacks a class: {fold_class_counts.to_dict()}")

    for reference_name, reference in (("Arm A", arm_a_reference), ("Arm B", arm_b_reference)):
        _require_columns(reference, [ID_COLUMN, "True_Label", "Predicted_Label", "Fold"], f"Experiment 4 {reference_name} OOF")
        validate_unique_ids(reference, f"Experiment 4 {reference_name} OOF")
        if set(reference[ID_COLUMN]) != set(baseline[ID_COLUMN]):
            raise ValueError(f"Experiment 4 {reference_name} OOF population differs from the baseline")
    discovery = {
        "candidate_origin_counts": {str(k): int(v) for k, v in origin_counts.items()},
        "synthetic_families": int(all_synthetic["Synthetic_Family_ID"].nunique()),
        "approved_families": int(len(approved_review)),
        "approved_records": int(len(approved)),
        "unreviewed_families": int(len(unreviewed_review)),
        "unreviewed_records": int(len(all_synthetic) - len(approved)),
        "source_derived_excluded": int(len(source_candidates)),
        "baseline_matches_original": True,
        "selected_duplicate_ids": 0,
        "selected_duplicate_exact_texts": 0,
        "experiment4_arm_a_recomputed": _reference_metrics(arm_a_reference),
        "experiment4_arm_b_recomputed": _reference_metrics(arm_b_reference),
        "synthetic_family_class_counts": approved.drop_duplicates("Synthetic_Family_ID")[TARGET_COLUMN].value_counts().reindex(LABEL_ORDER, fill_value=0).astype(int).to_dict(),
        "synthetic_fold_class_counts": approved.drop_duplicates("Synthetic_Family_ID").groupby(["evaluation_fold", TARGET_COLUMN]).size().unstack(fill_value=0).reindex(columns=LABEL_ORDER, fill_value=0).astype(int).to_dict(orient="index"),
    }
    return {
        "original": original,
        "candidate": candidate,
        "review": review,
        "baseline": baseline,
        "approved": approved,
        "approved_ids": approved_ids,
        "synthetic_fold_map": synthetic_fold_map,
        "arm_a_reference": arm_a_reference,
        "hashes": hashes,
        "discovery": discovery,
    }


def build_manifests(validated: dict[str, Any]) -> tuple[pd.DataFrame, pd.DataFrame]:
    candidate = validated["candidate"].copy()
    baseline_fold = validated["baseline"].set_index(ID_COLUMN)["evaluation_fold"].to_dict()
    combined_group = validated["baseline"].set_index(ID_COLUMN)["Combined_Group"].to_dict()
    synthetic_fold = validated["synthetic_fold_map"]
    approved_ids = validated["approved_ids"]
    population_rows = []
    for _, row in candidate.iterrows():
        origin = str(row["Record_Origin"])
        family_id = "" if pd.isna(row["Synthetic_Family_ID"]) else str(row["Synthetic_Family_ID"])
        if origin == BASELINE_ORIGIN:
            included, reason, exclusion = True, "frozen real baseline population", ""
            fold = baseline_fold[str(row[ID_COLUMN])]
            source_group = combined_group[str(row[ID_COLUMN])]
        elif origin == SYNTHETIC_ORIGIN and family_id in approved_ids:
            included, reason, exclusion = True, "confirmed Approved synthetic family", ""
            fold = synthetic_fold[family_id]
            source_group = ""
        elif origin == SOURCE_ORIGIN:
            included, reason, exclusion = False, "", "source-derived candidate review pending"
            fold, source_group = "", ""
        elif origin == SYNTHETIC_ORIGIN:
            included, reason, exclusion = False, "", "synthetic family unreviewed"
            fold, source_group = "", ""
        else:
            raise ValueError(f"Unexpected Record_Origin while building manifest: {origin}")
        population_rows.append({
            ID_COLUMN: row[ID_COLUMN],
            "population_origin": origin,
            "label": row[TARGET_COLUMN],
            "source_group": source_group,
            "synthetic_family_id": family_id,
            "synthetic_variant": "" if pd.isna(row["Synthetic_Variant"]) else row["Synthetic_Variant"],
            "evaluation_fold": fold,
            "eligibility_reason": reason,
            "included_in_experiment": included,
            "exclusion_reason": exclusion,
        })
    population = pd.DataFrame(population_rows)
    folds = population[population["included_in_experiment"]].copy()
    folds["evaluation_fold"] = folds["evaluation_fold"].astype(int)
    return population, folds


def _save_population_outputs(arm_dir: Path, name: str, oof: pd.DataFrame) -> dict[str, Any]:
    metrics, per_class, cm = calculate_metrics(oof)
    if name == "synthetic":
        metrics["family_metrics"] = calculate_family_metrics(oof)
    oof.to_csv(arm_dir / f"oof_predictions_{name}.csv", index=False)
    write_json(arm_dir / f"metrics_{name}.json", metrics)
    per_class.to_csv(arm_dir / f"per_class_metrics_{name}.csv", index=False)
    cm.to_csv(arm_dir / f"confusion_matrix_{name}.csv")
    return metrics


def run_arm(arm: str, baseline: pd.DataFrame, approved: pd.DataFrame, arm_dir: Path) -> dict[str, Any]:
    arm_dir.mkdir(parents=False, exist_ok=False)
    real_oof: list[pd.DataFrame] = []
    synthetic_oof: list[pd.DataFrame] = []
    fold_metric_rows: list[dict[str, Any]] = []
    for fold in range(1, N_FOLDS + 1):
        real_train = baseline[baseline["evaluation_fold"] != fold].copy()
        real_eval = baseline[baseline["evaluation_fold"] == fold].copy()
        synthetic_all_train = approved[approved["evaluation_fold"] != fold].copy()
        synthetic_eval = approved[approved["evaluation_fold"] == fold].copy()
        synthetic_train = synthetic_all_train.iloc[0:0].copy() if arm == "arm_a" else synthetic_all_train
        if arm == "arm_b":
            synthetic_train = select_one_variant_per_family(synthetic_train)
        validate_no_train_evaluation_overlap(real_train, real_eval, synthetic_train, synthetic_eval)
        train = pd.concat([real_train, synthetic_train], ignore_index=True)
        pipeline = build_pipeline(arm)
        weights = None
        if arm in {"arm_d", "arm_e"}:
            weights = family_balanced_weights(len(real_train), synthetic_train)
        fit_pipeline(
            pipeline,
            train[TEXT_COLUMN].astype(str).tolist(),
            train[TARGET_COLUMN].astype(str).tolist(),
            weights,
        )
        real_prediction = _predict_rows(pipeline, real_eval, "real")
        synthetic_prediction = _predict_rows(pipeline, synthetic_eval, "synthetic")
        real_oof.append(real_prediction)
        synthetic_oof.append(synthetic_prediction)
        for population, predictions in (("real", real_prediction), ("synthetic", synthetic_prediction), ("combined", pd.concat([real_prediction, synthetic_prediction], ignore_index=True))):
            metrics, _, _ = calculate_metrics(predictions)
            fold_metric_rows.append({
                "fold": fold,
                "population": population,
                "support": metrics["support"],
                "accuracy": metrics["accuracy"],
                "balanced_accuracy": metrics["balanced_accuracy"],
                "macro_f1": metrics["macro_f1"],
                "real_train_records": int(len(real_train)),
                "synthetic_train_records": int(len(synthetic_train)),
                "synthetic_train_families": int(synthetic_train["Synthetic_Family_ID"].nunique()),
                "sample_weight_sum": None if weights is None else float(weights.sum()),
            })
    real = pd.concat(real_oof, ignore_index=True).sort_values(ID_COLUMN).reset_index(drop=True)
    synthetic = pd.concat(synthetic_oof, ignore_index=True).sort_values(ID_COLUMN).reset_index(drop=True)
    combined = pd.concat([real, synthetic], ignore_index=True).sort_values(ID_COLUMN).reset_index(drop=True)
    if len(real) != len(baseline) or len(synthetic) != len(approved) or len(combined) != len(baseline) + len(approved):
        raise AssertionError(f"{arm} OOF population sizes are incomplete")
    validate_unique_ids(real, f"{arm} real OOF")
    validate_unique_ids(synthetic, f"{arm} synthetic OOF")
    validate_unique_ids(combined, f"{arm} combined OOF")
    metrics = {
        "real": _save_population_outputs(arm_dir, "real", real),
        "synthetic": _save_population_outputs(arm_dir, "synthetic", synthetic),
        "combined": _save_population_outputs(arm_dir, "combined", combined),
    }
    pd.DataFrame(fold_metric_rows).to_csv(arm_dir / "fold_metrics.csv", index=False)
    metadata = {
        "arm": arm,
        "random_seed": RANDOM_STATE,
        "probability_class_order": LABEL_ORDER,
        "real_training_weight": 1.0,
        "synthetic_training": {
            "arm_a": "none",
            "arm_b": "lowest deterministic variant per training family",
            "arm_c": "all variants, unweighted",
            "arm_d": "all variants, each family total weight 1",
            "arm_e": "all variants, each family total weight 1",
        }[arm],
        "pipeline": "word + character TF-IDF" if arm == "arm_e" else "established word TF-IDF",
        "character_tfidf": None if arm != "arm_e" else {"analyzer": "char_wb", "ngram_range": [3, 5], "lowercase": True, "sublinear_tf": True, "min_df": 1},
        "development_note": "Development grouped CV; not a final independent or prospective test.",
        "model_saved": False,
    }
    write_json(arm_dir / "run_metadata.json", metadata)
    return {"oof": {"real": real, "synthetic": synthetic, "combined": combined}, "metrics": metrics, "metadata": metadata}


def validate_identical_oof_populations(arm_results: dict[str, Any]) -> None:
    reference = arm_results["arm_a"]["oof"]
    for arm in ARMS[1:]:
        for population in ("real", "synthetic", "combined"):
            left = reference[population].sort_values(ID_COLUMN).reset_index(drop=True)
            right = arm_results[arm]["oof"][population].sort_values(ID_COLUMN).reset_index(drop=True)
            for column in (ID_COLUMN, "True_Label", "Fold"):
                if not left[column].equals(right[column]):
                    raise ValueError(f"OOF {population} {column} mismatch between arm_a and {arm}")


def validate_arm_a_reproduction(actual: pd.DataFrame, reference: pd.DataFrame) -> dict[str, Any]:
    validate_unique_ids(actual, "Experiment 5 Arm A real OOF")
    validate_unique_ids(reference, "Experiment 4 Arm A reference")
    columns = [ID_COLUMN, "True_Label", "Predicted_Label", "Fold", *PROBABILITY_COLUMNS]
    _require_columns(reference, columns, "Experiment 4 Arm A reference")
    merged = actual[columns].merge(reference[columns], on=ID_COLUMN, suffixes=("_actual", "_reference"), validate="one_to_one")
    for column in ("True_Label", "Predicted_Label", "Fold"):
        if not merged[f"{column}_actual"].equals(merged[f"{column}_reference"]):
            raise ValueError(f"Arm A reproduction failed for {column}")
    max_diff = max(float((merged[f"{column}_actual"] - merged[f"{column}_reference"]).abs().max()) for column in PROBABILITY_COLUMNS)
    if max_diff > ARM_A_REPRODUCTION_TOLERANCE:
        raise ValueError(f"Arm A reproduction probability difference {max_diff} exceeds {ARM_A_REPRODUCTION_TOLERANCE}")
    return {"status": "PASS", "records": int(len(merged)), "prediction_mismatches": 0, "max_abs_probability_difference": max_diff, "tolerance": ARM_A_REPRODUCTION_TOLERANCE}


def select_candidate(arm_results: dict[str, Any]) -> dict[str, Any]:
    arm_a_real_accuracy = arm_results["arm_a"]["metrics"]["real"]["accuracy"]
    evaluations: dict[str, Any] = {}
    passing: list[str] = []
    for arm in ARMS[1:]:
        metrics = arm_results[arm]["metrics"]
        checks = {
            "combined_accuracy_gt_0_80": metrics["combined"]["accuracy"] > 0.80,
            "combined_macro_f1_gt_0_80": metrics["combined"]["macro_f1"] > 0.80,
            "synthetic_accuracy_gt_0_80": metrics["synthetic"]["accuracy"] > 0.80,
            "real_accuracy_decrease_le_0_01": metrics["real"]["accuracy"] >= arm_a_real_accuracy - 0.01,
            "all_class_metrics_defined": all(metrics[name]["all_class_metrics_defined"] for name in ("real", "synthetic", "combined")),
            "no_zero_recall_class": all(metrics[name]["no_zero_recall_class"] for name in ("real", "synthetic", "combined")),
            "valid_probabilities": True,
        }
        passed = all(checks.values())
        evaluations[arm] = {"checks": checks, "passed_all": passed}
        if passed:
            passing.append(arm)
    selected = None
    if passing:
        selected = max(passing, key=lambda arm: (arm_results[arm]["metrics"]["combined"]["macro_f1"], arm_results[arm]["metrics"]["combined"]["accuracy"], arm))
    return {
        "arm_a_real_accuracy_reference": arm_a_real_accuracy,
        "arm_evaluations": evaluations,
        "passing_arms": passing,
        "selected_candidate": selected,
        "result": "CANDIDATE_IDENTIFIED" if selected else "NO_CANDIDATE_SELECTED",
        "deployment_authorized": False,
    }


def _git_info() -> dict[str, Any]:
    try:
        return {
            "branch": subprocess.check_output(["git", "branch", "--show-current"], cwd=ML_ROOT, text=True).strip(),
            "commit": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ML_ROOT, text=True).strip(),
        }
    except Exception as exc:
        return {"error": str(exc)}


def _make_report(summary: dict[str, Any], comparisons: dict[str, Any]) -> str:
    lines = [
        "# Experiment 5: Leakage-Safe Combined Real + Synthetic Development Evaluation",
        "",
        "> Development evidence only. This is not a final independent or prospective test, and no model was promoted.",
        "",
        "## Validated populations",
        "",
        f"- Real/source-confirmed baseline: {summary['discovery']['candidate_origin_counts'][BASELINE_ORIGIN]}",
        f"- Approved synthetic families/records: {summary['discovery']['approved_families']} / {summary['discovery']['approved_records']}",
        f"- Excluded source-derived candidates: {summary['discovery']['source_derived_excluded']}",
        f"- Excluded unreviewed families/records: {summary['discovery']['unreviewed_families']} / {summary['discovery']['unreviewed_records']}",
        "",
        "## Split strategy and leakage checks",
        "",
        "The exact frozen Experiment 4 Arm A real folds and Combined_Group assignments were reused. Approved synthetic families were label-stratified into five deterministic folds with seed 42; all three variants stay together. For each fold, neither real Combined_Group nor Synthetic_Family_ID crosses training/evaluation.",
        "",
        "## Arm metrics",
        "",
        "| Arm | Population | Support | Accuracy | Balanced accuracy | Macro-F1 | Weighted-F1 |",
        "|---|---:|---:|---:|---:|---:|---:|",
    ]
    for arm in ARMS:
        for population in ("real", "synthetic", "combined"):
            metric = summary["arms"][arm][population]
            lines.append(f"| {arm.upper()} | {population} | {metric['support']} | {metric['accuracy']:.6f} | {metric['balanced_accuracy']:.6f} | {metric['macro_f1']:.6f} | {metric['weighted_f1']:.6f} |")
    lines.extend(["", "## Synthetic family metrics", "", "| Arm | Majority accuracy | Majority macro-F1 | All 3 correct | 0/1/2/3 correct distribution |", "|---|---:|---:|---:|---|"])
    for arm in ARMS:
        family = summary["arms"][arm]["synthetic"]["family_metrics"]
        lines.append(f"| {arm.upper()} | {family['family_majority_accuracy']:.6f} | {family['family_majority_macro_f1']:.6f} | {family['families_all_three_variants_correct']} | {family['correct_variants_distribution']} |")
    lines.extend(["", "## Paired comparisons versus Arm A", ""])
    for arm, populations in comparisons.items():
        lines.append(f"### {arm.upper()}")
        lines.append("")
        for population, values in populations.items():
            lines.append(f"- {population}: helped {values['helped']}, hurt {values['hurt']}, net {values['net_gain']}, prediction changes {values['prediction_changes']}.")
        lines.append("")
    selection = summary["selection"]
    verdict = "EXPERIMENT 5 CANDIDATE IDENTIFIED — PROSPECTIVE TEST STILL REQUIRED" if selection["selected_candidate"] else "EXPERIMENT 5 NO CANDIDATE SELECTED"
    lines.extend([
        "## Selection result",
        "",
        f"Selected development candidate: `{selection['selected_candidate']}`." if selection["selected_candidate"] else "No arm satisfied every preregistered selection check.",
        "Passing these checks does not authorize deployment.",
        "",
        "## Limitations",
        "",
        "This is development cross-validation over the existing real baseline and researcher-reviewed synthetic scenarios. Synthetic review is not an independent prospective test, Arm E is development-oriented, and the same folds are used for comparison/selection. The 30 pending source-derived candidates and 25 unreviewed synthetic families were not evaluated.",
        "",
        f"## Verdict\n\n**{verdict}**",
        "",
    ])
    return "\n".join(lines)


def run_experiment5(output_dir: Path = DEFAULT_OUTPUT_DIR) -> dict[str, Any]:
    protected_before = _protected_inventory()
    validated = validate_inputs()
    ensure_new_output_directory(output_dir)
    population_manifest, fold_manifest = build_manifests(validated)
    population_manifest.to_csv(output_dir / "population_manifest.csv", index=False)
    population_manifest.to_csv(output_dir / "experiment5_population_manifest.csv", index=False)
    fold_manifest.to_csv(output_dir / "fold_manifest.csv", index=False)
    fold_manifest.to_csv(output_dir / "experiment5_fold_manifest.csv", index=False)
    write_json(output_dir / "input_hashes.json", validated["hashes"])
    write_json(output_dir / "experiment5_input_hashes.json", validated["hashes"])
    protocol = {
        "experiment": 5,
        "title": "Leakage-safe combined real + synthetic development evaluation",
        "random_seed": RANDOM_STATE,
        "fold_count": N_FOLDS,
        "class_order": LABEL_ORDER,
        "real_split": "Exact Evaluation_Fold and Combined_Group from Experiment 4 Arm A frozen baseline manifest",
        "synthetic_split": "StratifiedKFold over approved family IDs; all variants contained in family fold",
        "arms": list(ARMS),
        "selection_thresholds": {"combined_accuracy": "> 0.80", "combined_macro_f1": "> 0.80", "synthetic_accuracy": "> 0.80", "maximum_real_accuracy_decrease": 0.01},
        "final_prospective_test_authorized": False,
        "model_promotion_authorized": False,
        "git": _git_info(),
    }
    write_json(output_dir / "protocol.json", protocol)
    write_json(output_dir / "experiment5_protocol.json", protocol)

    arm_results = {arm: run_arm(arm, validated["baseline"], validated["approved"], output_dir / arm) for arm in ARMS}
    validate_identical_oof_populations(arm_results)
    reproduction = validate_arm_a_reproduction(arm_results["arm_a"]["oof"]["real"], validated["arm_a_reference"])

    comparisons_dir = output_dir / "comparisons"
    comparisons_dir.mkdir()
    comparison_summaries: dict[str, Any] = {}
    for arm in ARMS[1:]:
        comparison_summaries[arm] = {}
        for population in ("real", "synthetic", "combined"):
            comparison, rows = paired_comparison(arm_results["arm_a"]["oof"][population], arm_results[arm]["oof"][population])
            comparison_summaries[arm][population] = comparison
            rows.to_csv(comparisons_dir / f"{arm}_vs_arm_a_{population}.csv", index=False)
        write_json(comparisons_dir / f"{arm}_vs_arm_a_summary.json", comparison_summaries[arm])

    selection = select_candidate(arm_results)
    protected_after = _protected_inventory()
    if protected_before != protected_after:
        changed = sorted(set(protected_before) ^ set(protected_after) | {path for path in set(protected_before) & set(protected_after) if protected_before[path] != protected_after[path]})
        raise RuntimeError(f"Protected source/model/prior-result artifacts changed during Experiment 5: {changed[:10]}")
    summary = {
        "experiment": 5,
        "development_evaluation_only": True,
        "discovery": validated["discovery"],
        "arm_a_reproduction": reproduction,
        "arms": {arm: arm_results[arm]["metrics"] for arm in ARMS},
        "paired_comparisons": comparison_summaries,
        "selection": selection,
        "leakage_checks": {
            "real_combined_groups_contained": True,
            "synthetic_families_contained": True,
            "train_evaluation_overlap": False,
            "identical_oof_populations_across_arms": True,
        },
        "hash_preservation": {"status": "PASS", "protected_file_count": len(protected_before)},
        "model_promoted": False,
    }
    write_json(output_dir / "experiment5_summary.json", summary)
    (output_dir / "experiment5_report.md").write_text(_make_report(summary, comparison_summaries), encoding="utf-8")
    return summary


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-dir", type=Path, default=DEFAULT_OUTPUT_DIR)
    args = parser.parse_args()
    summary = run_experiment5(args.output_dir.resolve())
    print(json.dumps({"output_dir": str(args.output_dir.resolve()), "selection": summary["selection"]}, indent=2))


if __name__ == "__main__":
    main()
