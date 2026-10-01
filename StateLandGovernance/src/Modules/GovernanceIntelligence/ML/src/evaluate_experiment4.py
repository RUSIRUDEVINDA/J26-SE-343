"""
evaluate_experiment4.py
-----------------------
EXPERIMENT 4 — Synthetic Augmentation Comparison.

Entry point supports an explicit --arm flag.
This task implements --arm a only.

Arm A (Baseline — Cleaned Text, No Augmentation)
-------------------------------------------------
Reproduces Experiment 3 Arm B:
  BoilerplateStripper → TfidfVectorizer → LogisticRegression
  using frozen Experiment 2/3 Combined_Group fold assignments,
  on the 200 "Existing baseline" rows selected from the 530-row
  candidate dataset.

No synthetic or source-derived rows enter training or evaluation.

Arm B (to be implemented in a future task).

Inputs
------
  Candidate dataset   : ML/data/state_land_complaints_development_530.csv
  Frozen fold reference: results/experiment2_source_text/out_of_fold_predictions.csv
  Reproduction ref     : results/experiment3_boilerplate_removal/exp3b_cleaned_text/oof_predictions.csv

Outputs (Arm A)
---------------
results/experiment4_synthetic_augmentation/
  ├── arm_a/
  │   ├── oof_predictions.csv
  │   ├── metrics.json
  │   ├── per_class_metrics.csv
  │   ├── confusion_matrix.csv
  │   └── fold_metrics.csv
  ├── baseline_fold_manifest.csv
  ├── reproduction_check.json
  └── run_metadata.json

Interpretive limitations
------------------------
These are development cross-validation results on the 200 baseline records.
Frozen fold assignments are replayed, not freshly generated.
Results are not untouched held-out test results.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import logging
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import numpy as np
import pandas as pd
from sklearn.metrics import (
    accuracy_score,
    confusion_matrix,
    f1_score,
    precision_score,
    recall_score,
)

# Ensure ML/src is on path regardless of launch directory
_SRC_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(_SRC_DIR))

import config
from config import (
    ID_COLUMN,
    LABEL_ORDER,
    TARGET_COLUMN,
    TEXT_COLUMN,
    GROUP_COLUMN,
)
from boilerplate_transformer import (
    BOILERPLATE_PHRASES,
    BoilerplateStripper,
    build_pipeline_with_cleaner,
    strip_research_boilerplate,
)

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s — %(message)s",
)
logger = logging.getLogger("evaluate_experiment4")

# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------

CANDIDATE_CSV_PATH = (
    config.ML_ROOT / "data" / "state_land_complaints_development_530.csv"
)
BASELINE_ORIGIN = "Existing baseline"

EXP2_OOF_PATH = (
    config.RESULTS_DIR / "experiment2_source_text" / "out_of_fold_predictions.csv"
)
EXP3B_OOF_PATH = (
    config.RESULTS_DIR
    / "experiment3_boilerplate_removal"
    / "exp3b_cleaned_text"
    / "oof_predictions.csv"
)

EXP4_OUTPUT_NAME = "experiment4_synthetic_augmentation"

# Columns required in the frozen fold reference.
# True_Label is mandatory so that label agreement can be verified before training.
FOLD_REF_REQUIRED_COLS = {ID_COLUMN, "Combined_Group", "Fold", "True_Label"}

# Columns required in the reproduction reference
REPRO_REF_REQUIRED_COLS = {ID_COLUMN, "True_Label", "Predicted_Label", "Fold"}

# Expected probability column names in the reproduction reference (OOF files).
# These are derived from LABEL_ORDER using the same sanitisation used when
# writing OOF CSVs.  They must be present in the reference for a valid
# probability comparison.
def _prob_col(label: str) -> str:
    return f"prob_{label.replace('/', '_').replace(' ', '_')}"

EXPECTED_PROB_COLS: list[str] = [_prob_col(lbl) for lbl in LABEL_ORDER]

# Probability comparison tolerance.
#
# Both the Arm A OOF and the Experiment 3 Arm B reference store individual
# probability values rounded to 4 decimal places (e.g. round(p, 4)).
#
# Rounding error per stored value:
#   |stored - true| <= 0.5 × 10^-4 = 0.00005
#
# Maximum absolute difference between two stored 4-dp values that represent
# the same underlying float:
#   |stored_a - stored_b| <= 2 × 0.00005 = 0.0001
#
# PROB_TOLERANCE = 0.0001 is the tightest correct tolerance for comparing
# two 4-dp OOF files produced from the same model.  Values above this
# threshold indicate genuine structural divergence, not rounding.
# The comparison uses np.abs() (absolute difference, rtol=0).
PROB_TOLERANCE: float = 0.0001

# Row-sum tolerance for a single 4-decimal-place probability distribution.
#
# With 4 class probabilities each rounded to 4dp, worst-case row-sum error:
#   4 × 0.00005 = 0.0002
#
# We use 0.00021 (one unit of the fifth decimal place above worst case)
# to accept valid 4-dp distributions without false positives.
PROB_ROW_SUM_TOLERANCE: float = 0.00021


# ---------------------------------------------------------------------------
# Utilities
# ---------------------------------------------------------------------------

def sha256_file(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def get_git_info() -> dict[str, Any]:
    """Retrieve current commit hash and working-tree dirty status.

    Note: git revision alone does not capture uncommitted code changes.
    working_tree_clean=False indicates the reported commit does not fully
    describe the running code.
    """
    try:
        commit = subprocess.check_output(
            ["git", "rev-parse", "HEAD"], stderr=subprocess.DEVNULL, text=True
        ).strip()
        status = subprocess.check_output(
            ["git", "status", "--porcelain"], stderr=subprocess.DEVNULL, text=True
        ).strip()
        return {
            "commit": commit,
            "working_tree_clean": len(status) == 0,
            "note": (
                "git revision alone does not capture uncommitted code. "
                "working_tree_clean=False means the reported commit may not "
                "describe the running code."
            ),
        }
    except Exception as exc:
        return {"commit": "unknown", "working_tree_clean": False, "error": str(exc)}


def _dir_is_occupied(d: Path) -> bool:
    """Return True if directory exists and contains at least one entry."""
    return d.exists() and any(d.iterdir())


def resolve_exp4_output_dir(base_results_dir: Path) -> Path:
    """
    Resolve Experiment 4 output directory (automatic-path mode only).

    Any non-empty destination (including interrupted runs) is considered
    occupied. Selection order:
      1. Canonical base directory (experiment4_synthetic_augmentation/).
      2. Lowest numbered _run<N> variant that is empty or absent.

    When output_dir is supplied explicitly by the caller, this function is
    NOT used.  An explicit nonempty directory is rejected before any writing.
    """
    target = base_results_dir / EXP4_OUTPUT_NAME
    if not _dir_is_occupied(target):
        target.mkdir(parents=True, exist_ok=True)
        return target
    run_num = 1
    while True:
        candidate = base_results_dir / f"{EXP4_OUTPUT_NAME}_run{run_num}"
        if not _dir_is_occupied(candidate):
            candidate.mkdir(parents=True, exist_ok=True)
            return candidate
        run_num += 1


# ---------------------------------------------------------------------------
# Per-class metrics (replicating Exp3 convention exactly)
# ---------------------------------------------------------------------------

def compute_per_class_metrics(
    y_true: list[str], y_pred: list[str]
) -> dict[str, dict[str, Any]]:
    """
    Compute Support, TP, FP, FN, TN, Precision, Recall, F1, FPR per class
    using one-vs-rest counts from the confusion matrix.

    Zero-denominator handling
    -------------------------
    When a denominator is zero the metric value is recorded as the string
    "undefined" with a "reason" field explaining the denominator. This keeps
    the per-class dict distinguishable from sklearn's zero_division=0
    convention (which silently returns 0.0). The macro average of any metric
    that has one or more undefined class values is also recorded as
    "undefined" with a "reason" field and a list of the affected classes.
    """
    cm = confusion_matrix(y_true, y_pred, labels=LABEL_ORDER)
    n = int(cm.sum())
    results: dict[str, dict[str, Any]] = {}

    for i, label in enumerate(LABEL_ORDER):
        tp = int(cm[i, i])
        fp = int(cm[:, i].sum() - tp)
        fn = int(cm[i, :].sum() - tp)
        tn = int(n - tp - fp - fn)
        support = tp + fn

        def _divide(num: int, denom: int, metric_name: str):
            if denom == 0:
                return {"value": "undefined", "reason": f"{metric_name}: denominator=0 (denom={denom})"}
            return round(num / denom, 4)

        prec = _divide(tp, tp + fp, "Precision")
        rec  = _divide(tp, tp + fn, "Recall")
        f1   = _divide(2 * tp, 2 * tp + fp + fn, "F1")
        fpr  = _divide(fp, fp + tn, "FPR")

        # Preserve the reason string from _divide when the metric is undefined,
        # so that saved output distinguishes zero-denominator cases from valid results.
        results[label] = {
            "Support": support,
            "TP": tp, "FP": fp, "FN": fn, "TN": tn,
            "Precision": prec if isinstance(prec, float) else prec["value"],
            "Precision_defined": isinstance(prec, float),
            "Precision_reason": None if isinstance(prec, float) else prec["reason"],
            "Recall": rec if isinstance(rec, float) else rec["value"],
            "Recall_defined": isinstance(rec, float),
            "Recall_reason": None if isinstance(rec, float) else rec["reason"],
            "F1": f1 if isinstance(f1, float) else f1["value"],
            "F1_defined": isinstance(f1, float),
            "F1_reason": None if isinstance(f1, float) else f1["reason"],
            "FPR": fpr if isinstance(fpr, float) else fpr["value"],
            "FPR_defined": isinstance(fpr, float),
            "FPR_reason": None if isinstance(fpr, float) else fpr["reason"],
        }

    return results


def _safe_macro(
    per_class: dict[str, dict[str, Any]], metric_key: str
) -> dict[str, Any]:
    """
    Compute macro average of a per-class metric.
    If any class has an undefined value, return undefined with affected classes listed.
    """
    undefined_classes = [
        lbl for lbl in LABEL_ORDER
        if not per_class[lbl].get(f"{metric_key}_defined", True)
    ]
    if undefined_classes:
        return {
            "value": "undefined",
            "reason": f"macro_{metric_key}: one or more classes have undefined {metric_key}",
            "undefined_classes": undefined_classes,
        }
    vals = [per_class[lbl][metric_key] for lbl in LABEL_ORDER]
    return {"value": round(float(np.mean(vals)), 4)}


# ---------------------------------------------------------------------------
# Input validation
# ---------------------------------------------------------------------------

def _require_file(path: Path, label: str) -> None:
    """Raise FileNotFoundError with a clear message if path does not exist."""
    if not path.exists():
        raise FileNotFoundError(
            f"Required {label} not found: {path}. "
            "This file is a mandatory input; the script cannot proceed without it."
        )


def _require_columns(df: pd.DataFrame, required: set[str], source: str) -> None:
    """Raise ValueError if any required column is absent."""
    missing = required - set(df.columns)
    if missing:
        raise ValueError(
            f"Required column(s) missing from {source}: {sorted(missing)}. "
            f"Available: {sorted(df.columns)}"
        )


def load_and_filter_baseline(candidate_path: Path) -> pd.DataFrame:
    """
    Load the 530-row candidate dataset, select "Existing baseline" rows,
    and return a validated 200-row DataFrame.

    Raises FileNotFoundError or ValueError for any structural problem.
    """
    _require_file(candidate_path, "candidate dataset")

    logger.info("Loading candidate dataset from '%s'", candidate_path)
    raw = pd.read_csv(candidate_path)

    if "Record_Origin" not in raw.columns:
        raise ValueError(
            f"Column 'Record_Origin' missing from candidate dataset. "
            f"Available: {sorted(raw.columns)}"
        )

    df = raw[raw["Record_Origin"] == BASELINE_ORIGIN].copy()
    logger.info(
        "Selected %d '%s' rows from %d total rows.",
        len(df), BASELINE_ORIGIN, len(raw),
    )

    # Guard: no synthetic or source-derived rows may enter
    non_baseline = raw[raw["Record_Origin"] != BASELINE_ORIGIN]
    if len(df) + len(non_baseline) != len(raw):
        raise ValueError("Row count mismatch after origin filter — dataset may have unexpected origins.")

    # Mandatory column checks
    required = {ID_COLUMN, TEXT_COLUMN, TARGET_COLUMN, GROUP_COLUMN}
    _require_columns(df, required, "baseline subset")

    # Exactly 200 unique, non-blank IDs
    null_ids  = df[ID_COLUMN].isna().sum()
    blank_ids = (df[ID_COLUMN].fillna("").str.strip() == "").sum()
    if null_ids > 0 or blank_ids > 0:
        raise ValueError(
            f"Baseline Research_ID has {null_ids} nulls and {blank_ids} blanks."
        )
    n_unique = df[ID_COLUMN].nunique()
    if n_unique < 1:
        raise ValueError(
            f"Baseline subset has no records (0 unique Research_IDs). "
            f"Check that the candidate CSV contains '{BASELINE_ORIGIN}' rows."
        )

    # Null/blank text
    null_text  = df[TEXT_COLUMN].isna().sum()
    blank_text = (df[TEXT_COLUMN].fillna("").str.strip() == "").sum()
    if null_text > 0 or blank_text > 0:
        raise ValueError(
            f"Baseline text has {null_text} nulls and {blank_text} blanks."
        )

    # Labels
    null_labels = df[TARGET_COLUMN].isna().sum()
    if null_labels > 0:
        raise ValueError(f"Baseline labels has {null_labels} null values.")
    invalid = set(df[TARGET_COLUMN].unique()) - set(LABEL_ORDER)
    if invalid:
        raise ValueError(f"Baseline contains invalid labels: {invalid}")

    # Source_Group_ID
    null_groups = df[GROUP_COLUMN].isna().sum()
    if null_groups > 0:
        raise ValueError(f"Baseline Source_Group_ID has {null_groups} null values.")

    logger.info("Baseline subset validated: 200 rows, all required fields present.")
    return df


def validate_fold_reference(
    baseline_df: pd.DataFrame,
    exp2_oof: pd.DataFrame,
) -> dict[str, int]:
    """
    Validate the frozen fold reference (Experiment 2 OOF file) against the
    baseline DataFrame and return a Research_ID → Fold mapping.

    True_Label is mandatory so that label agreement can be verified before
    any fold is fitted.  Combined_Group must be non-null and non-blank for
    every baseline record.  All checks raise ValueError on failure.
    """
    # FOLD_REF_REQUIRED_COLS now includes True_Label — mandatory.
    _require_columns(exp2_oof, FOLD_REF_REQUIRED_COLS, "Experiment 2 OOF")

    baseline_ids = set(baseline_df[ID_COLUMN].tolist())
    ref_ids      = set(exp2_oof[ID_COLUMN].tolist())

    # Complete ID-set equality — both missing and extra IDs are rejected.
    missing = baseline_ids - ref_ids
    if missing:
        raise ValueError(
            f"{len(missing)} baseline IDs not found in fold reference: "
            f"{sorted(missing)[:10]}"
        )

    extra = ref_ids - baseline_ids
    if extra:
        raise ValueError(
            f"{len(extra)} IDs in fold reference not in baseline: "
            f"{sorted(extra)[:10]}"
        )

    # Unique IDs in reference
    dup = exp2_oof[ID_COLUMN].duplicated().sum()
    if dup > 0:
        raise ValueError(f"Fold reference contains {dup} duplicate Research_IDs.")

    # Fold values exactly 1–5
    fold_vals = set(exp2_oof["Fold"].unique().tolist())
    if fold_vals != {1, 2, 3, 4, 5}:
        raise ValueError(f"Fold reference fold values are not exactly {{1–5}}: {fold_vals}")

    # True label agreement (mandatory — True_Label is now in FOLD_REF_REQUIRED_COLS)
    base_label_map = baseline_df.set_index(ID_COLUMN)[TARGET_COLUMN].to_dict()
    ref_label_map  = exp2_oof.set_index(ID_COLUMN)["True_Label"].to_dict()
    label_mm = [rid for rid in baseline_ids if base_label_map.get(rid) != ref_label_map.get(rid)]
    if label_mm:
        raise ValueError(
            f"{len(label_mm)} baseline records have true labels that differ from "
            f"the fold reference: {sorted(label_mm)[:10]}"
        )
    logger.info("True labels agree between baseline and fold reference for all %d records.", len(baseline_ids))

    # Build fold map
    fold_map = dict(zip(exp2_oof[ID_COLUMN], exp2_oof["Fold"]))

    # Non-null, non-blank Combined_Group — checked before the multi-fold test.
    baseline_subset = exp2_oof[exp2_oof[ID_COLUMN].isin(baseline_ids)].copy()
    null_cg  = baseline_subset["Combined_Group"].isna().sum()
    blank_cg = (baseline_subset["Combined_Group"].fillna("").str.strip() == "").sum()
    if null_cg > 0 or blank_cg > 0:
        raise ValueError(
            f"Fold reference has {null_cg} null and {blank_cg} blank Combined_Group "
            f"values for baseline IDs. Every record must have a non-blank Combined_Group."
        )

    # Combined_Group: each group in exactly one fold
    cg_map = dict(zip(exp2_oof[ID_COLUMN], exp2_oof["Combined_Group"]))
    cg_to_folds: dict[str, set] = {}
    for rid in baseline_ids:
        cg = cg_map[rid]
        cg_to_folds.setdefault(cg, set()).add(fold_map[rid])
    multi = {cg: folds for cg, folds in cg_to_folds.items() if len(folds) > 1}
    if multi:
        raise ValueError(
            f"{len(multi)} Combined_Group(s) span multiple evaluation folds — "
            f"frozen fold assignment violated. Affected groups: "
            f"{dict(list(multi.items())[:5])}"
        )

    logger.info(
        "Fold reference validated: %d IDs, folds 1–5, %d Combined_Groups, all in single folds.",
        len(baseline_ids), len(cg_to_folds),
    )
    return fold_map


def verify_baseline_vs_original(
    baseline_df: pd.DataFrame,
    original_path: Path,
) -> dict[str, Any]:
    """
    Join baseline subset against the original 200-row dataset by Research_ID
    and verify Canonical_English_Text, ML_Label_4Class, and Source_Group_ID
    match for every row.

    Returns a dict summarising the comparison results.
    Raises ValueError if the original file is missing or any field mismatches.
    """
    _require_file(original_path, "original 200-row dataset")

    orig = pd.read_csv(original_path)
    orig_ids = set(orig[ID_COLUMN].tolist())
    base_ids = set(baseline_df[ID_COLUMN].tolist())

    if orig_ids != base_ids:
        only_orig = sorted(orig_ids - base_ids)
        only_base = sorted(base_ids - orig_ids)
        raise ValueError(
            f"Research_ID sets differ. Only in original: {only_orig[:5]}. "
            f"Only in baseline: {only_base[:5]}."
        )

    merged = orig[[ID_COLUMN, TEXT_COLUMN, TARGET_COLUMN, GROUP_COLUMN]].merge(
        baseline_df[[ID_COLUMN, TEXT_COLUMN, TARGET_COLUMN, GROUP_COLUMN]].rename(
            columns={
                TEXT_COLUMN: "c_text",
                TARGET_COLUMN: "c_label",
                GROUP_COLUMN: "c_sgid",
            }
        ),
        on=ID_COLUMN, how="inner", validate="one_to_one",
    )

    text_mm  = (merged[TEXT_COLUMN] != merged["c_text"]).sum()
    label_mm = (merged[TARGET_COLUMN] != merged["c_label"]).sum()
    sgid_mm  = (merged[GROUP_COLUMN] != merged["c_sgid"]).sum()

    if text_mm > 0 or label_mm > 0 or sgid_mm > 0:
        raise ValueError(
            f"Baseline vs original field mismatches: "
            f"text={text_mm}, label={label_mm}, source_group_id={sgid_mm}. "
            f"Baseline rows must match the original dataset exactly."
        )

    logger.info(
        "Baseline vs original: 200 rows joined one-to-one; "
        "Canonical_English_Text, ML_Label_4Class, Source_Group_ID all match."
    )
    return {
        "joined_rows": len(merged),
        "text_mismatches": int(text_mm),
        "label_mismatches": int(label_mm),
        "source_group_id_mismatches": int(sgid_mm),
        "description": "Field values compared by Research_ID; identical compared field values confirmed.",
    }


# ---------------------------------------------------------------------------
# Arm A CV runner (mirrors Exp3 run_cv_arm)
# ---------------------------------------------------------------------------

def run_arm_a(
    arm_dir: Path,
    df: pd.DataFrame,
    fold_map: dict[str, int],
    cg_map: dict[str, str],
) -> dict[str, Any]:
    """
    Execute Arm A: BoilerplateStripper → TfidfVectorizer → LogisticRegression
    on the 200 baseline records using frozen Experiment 2/3 Combined_Group
    fold assignments.

    This replicates Experiment 3 Arm B exactly.
    Folds are replayed from the frozen fold map — no new splitting is performed.

    Probability columns are mapped using the fitted classifier's classes_
    attribute, not an assumed column order.
    """
    arm_dir.mkdir(parents=True, exist_ok=True)

    rids   = df[ID_COLUMN].tolist()
    y      = np.array(df[TARGET_COLUMN].tolist())
    X_raw  = np.array(df[TEXT_COLUMN].tolist())
    groups = np.array([cg_map[rid] for rid in rids])

    # Apply boilerplate cleaning to produce X for Arm A (matches Exp3 Arm B)
    X = np.array([strip_research_boilerplate(t) for t in X_raw])

    # Build fold splits from frozen fold assignments
    unique_folds = sorted(set(fold_map.values()))
    splits: list[tuple[np.ndarray, np.ndarray]] = []
    for fold_idx in unique_folds:
        test_idx  = np.array([i for i, rid in enumerate(rids) if fold_map.get(rid) == fold_idx])
        train_idx = np.array([i for i in range(len(rids)) if i not in set(test_idx.tolist())])
        splits.append((train_idx, test_idx))

    # Validate: every baseline record in exactly one test fold
    all_test = []
    for _, test_idx in splits:
        all_test.extend(test_idx.tolist())
    if len(all_test) != len(df):
        raise AssertionError(
            f"Arm A test index count {len(all_test)} != dataset size {len(df)}"
        )
    if len(set(all_test)) != len(df):
        raise AssertionError("Arm A: some records appear in multiple test folds.")

    # Validate Combined_Group: no train/test overlap within any fold
    for fold_idx, (train_idx, test_idx) in enumerate(splits, start=1):
        trn_cg = set(groups[train_idx])
        tst_cg = set(groups[test_idx])
        overlap = trn_cg & tst_cg
        if overlap:
            raise AssertionError(
                f"Arm A Fold {fold_idx}: Combined_Group overlap detected: {overlap}"
            )

    oof_records: list[dict[str, Any]] = []
    fold_results: list[dict[str, Any]] = []

    for fold_idx, (train_idx, test_idx) in enumerate(splits, start=1):
        X_train, X_test = X[train_idx], X[test_idx]
        y_train, y_test = y[train_idx], y[test_idx]

        # Fresh pipeline per fold — TF-IDF fitted on training rows only
        pipeline = build_pipeline_with_cleaner()
        pipeline.fit(X_train, y_train)
        y_pred  = pipeline.predict(X_test)
        y_prob  = pipeline.predict_proba(X_test)

        # Map probability columns using classifier's classes_ (not assumed order).
        # If a class is absent from training rows in this fold it will be missing
        # from clf.classes_; fill its probability column with 0.0.
        clf_classes: list[str] = pipeline.named_steps["clf"].classes_.tolist()
        missing_classes = [c for c in LABEL_ORDER if c not in clf_classes]

        f_acc      = round(float(accuracy_score(y_test, y_pred)), 4)
        f_macro_f1 = round(float(f1_score(y_test, y_pred, average="macro",
                                           labels=LABEL_ORDER, zero_division=0)), 4)
        f_wt_f1    = round(float(f1_score(y_test, y_pred, average="weighted",
                                           labels=LABEL_ORDER, zero_division=0)), 4)

        logger.info(
            "[Arm A] Fold %d/%d — train=%d, test=%d — Acc=%.4f, Macro-F1=%.4f",
            fold_idx, len(unique_folds), len(train_idx), len(test_idx),
            f_acc, f_macro_f1,
        )

        test_class_counts = {lbl: int((y_test == lbl).sum()) for lbl in LABEL_ORDER}
        fold_results.append({
            "Fold": fold_idx,
            "Train_Size": len(train_idx),
            "Test_Size": len(test_idx),
            "Accuracy": f_acc,
            "Macro_F1": f_macro_f1,
            "Weighted_F1": f_wt_f1,
            **{
                f"Test_{lbl.replace('/', '_').replace(' ', '_')}": cnt
                for lbl, cnt in test_class_counts.items()
            },
        })

        for i, row_idx in enumerate(test_idx):
            # Build probability dict using actual classifier class order.
            # Classes unseen in this fold's training data are filled with 0.0
            # (not present in clf.classes_; their probabilities are undefined).
            proba_dict = {
                f"prob_{cls.replace('/', '_').replace(' ', '_')}": round(float(y_prob[i, j]), 4)
                for j, cls in enumerate(clf_classes)
            }
            for missing_cls in missing_classes:
                col = f"prob_{missing_cls.replace('/', '_').replace(' ', '_')}"
                proba_dict[col] = 0.0
            oof_records.append({
                "Research_ID":   rids[row_idx],
                "Combined_Group": groups[row_idx],
                "True_Label":    y[row_idx],
                "Predicted_Label": y_pred[i],
                "Fold":          fold_idx,
                **proba_dict,
            })

    # Sort OOF in original dataset order
    oof_df = pd.DataFrame(oof_records)
    id_order = {rid: idx for idx, rid in enumerate(rids)}
    oof_df["_sort_key"] = oof_df["Research_ID"].map(id_order)
    oof_df = oof_df.sort_values("_sort_key").drop(columns=["_sort_key"])

    y_true_oof = oof_df["True_Label"].tolist()
    y_pred_oof = oof_df["Predicted_Label"].tolist()

    overall_acc     = round(float(accuracy_score(y_true_oof, y_pred_oof)), 4)
    overall_macro_f1 = round(float(f1_score(y_true_oof, y_pred_oof, average="macro",
                                             labels=LABEL_ORDER, zero_division=0)), 4)
    overall_wt_f1   = round(float(f1_score(y_true_oof, y_pred_oof, average="weighted",
                                             labels=LABEL_ORDER, zero_division=0)), 4)
    overall_macro_p  = round(float(precision_score(y_true_oof, y_pred_oof, average="macro",
                                                    labels=LABEL_ORDER, zero_division=0)), 4)
    overall_macro_r  = round(float(recall_score(y_true_oof, y_pred_oof, average="macro",
                                                 labels=LABEL_ORDER, zero_division=0)), 4)
    misclassified   = int((np.array(y_true_oof) != np.array(y_pred_oof)).sum())

    per_class = compute_per_class_metrics(y_true_oof, y_pred_oof)
    cm        = confusion_matrix(y_true_oof, y_pred_oof, labels=LABEL_ORDER)

    logger.info(
        "[Arm A] OOF Pooled — Acc=%.4f, Macro-F1=%.4f, Weighted-F1=%.4f, Errors=%d/200",
        overall_acc, overall_macro_f1, overall_wt_f1, misclassified,
    )

    # Save arm artifacts
    oof_df.to_csv(arm_dir / "oof_predictions.csv", index=False)

    per_class_rows = [{
        "Label": lbl,
        "Support": m["Support"], "TP": m["TP"], "FP": m["FP"],
        "FN": m["FN"], "TN": m["TN"],
        "Precision": m["Precision"], "Recall": m["Recall"],
        "F1": m["F1"], "FPR": m["FPR"],
    } for lbl, m in per_class.items()]
    pd.DataFrame(per_class_rows).to_csv(arm_dir / "per_class_metrics.csv", index=False)

    cm_df = pd.DataFrame(cm, index=LABEL_ORDER, columns=LABEL_ORDER)
    cm_df.to_csv(arm_dir / "confusion_matrix.csv")

    pd.DataFrame(fold_results).to_csv(arm_dir / "fold_metrics.csv", index=False)

    # Build macro values with undefined handling
    macro_precision_out = _safe_macro(per_class, "Precision")
    macro_recall_out    = _safe_macro(per_class, "Recall")
    macro_f1_out        = _safe_macro(per_class, "F1")

    metrics = {
        "Accuracy": overall_acc,
        "Macro_F1_sklearn": overall_macro_f1,
        "Macro_F1_from_per_class": macro_f1_out,
        "Weighted_F1": overall_wt_f1,
        "Macro_Precision_sklearn": overall_macro_p,
        "Macro_Precision_from_per_class": macro_precision_out,
        "Macro_Recall_sklearn": overall_macro_r,
        "Macro_Recall_from_per_class": macro_recall_out,
        "Misclassified": misclassified,
        "note_zero_division": (
            "sklearn metrics use zero_division=0 (undefined → 0.0). "
            "per_class metrics record 'undefined' when denominator is 0. "
            "Macro averages from per_class may differ from sklearn macro values "
            "when any class has an undefined metric."
        ),
    }
    with open(arm_dir / "metrics.json", "w", encoding="utf-8") as f:
        json.dump(metrics, f, indent=2)

    return {
        "overall_metrics": metrics,
        "per_class_metrics": per_class,
        "fold_results": fold_results,
        "confusion_matrix": cm.tolist(),
        "oof_df": oof_df,
    }


# ---------------------------------------------------------------------------
# Reproduction comparison
# ---------------------------------------------------------------------------

def _validate_prob_matrix(
    prob_df: pd.DataFrame,
    source_label: str,
    expected_cols: list[str],
) -> np.ndarray:
    """
    Validate a DataFrame of probability columns and return a float64 numpy
    array aligned to expected_cols column order.

    Validation rules (all mandatory — any failure raises ValueError):
      - All expected_cols must be present.
      - Values must be numeric and finite (no NaN, no ±Inf).
      - All values must lie in [0, 1].
      - Each row sum must lie within PROB_ROW_SUM_TOLERANCE of 1.0.
        For 4 probabilities each stored to 4dp, worst-case row-sum error is
        4 × 0.00005 = 0.0002; PROB_ROW_SUM_TOLERANCE (0.00021) accepts this.
      - The array is returned with columns ordered as expected_cols regardless
        of the DataFrame column order (explicit alignment by name).

    Returns
    -------
    np.ndarray, shape (n_rows, n_classes), dtype float64
        Columns ordered as expected_cols.
    """
    missing = [c for c in expected_cols if c not in prob_df.columns]
    if missing:
        raise ValueError(
            f"{source_label}: expected probability column(s) missing: {missing}. "
            f"Available columns: {sorted(prob_df.columns.tolist())}"
        )

    arr = prob_df[expected_cols].to_numpy(dtype=float)  # NaN → float nan

    if not np.isfinite(arr).all():
        n_nan = int(np.isnan(arr).sum())
        n_inf = int(np.isinf(arr).sum())
        raise ValueError(
            f"{source_label}: probability matrix contains non-finite values "
            f"(NaN={n_nan}, Inf={n_inf}). "
            "Missing or infinity values are not acceptable probability inputs."
        )

    if (arr < 0).any() or (arr > 1).any():
        out_of_range = int(((arr < 0) | (arr > 1)).sum())
        raise ValueError(
            f"{source_label}: {out_of_range} probability value(s) outside [0, 1]."
        )

    row_sums = arr.sum(axis=1)
    bad_rows = np.where(np.abs(row_sums - 1.0) > PROB_ROW_SUM_TOLERANCE)[0]
    if len(bad_rows) > 0:
        raise ValueError(
            f"{source_label}: {len(bad_rows)} row(s) have probability sums outside "
            f"[1 ± {PROB_ROW_SUM_TOLERANCE}]. "
            f"Row indices (up to 5): {bad_rows[:5].tolist()}. "
            f"Sums (up to 5): {row_sums[bad_rows[:5]].tolist()}. "
            "Each row must be a valid probability distribution."
        )

    return arr


def run_reproduction_check(
    arm_a_oof: pd.DataFrame,
    repro_path: Path,
    output_path: Path,
) -> dict[str, Any]:
    """
    Compare new Arm A OOF predictions against saved Experiment 3 Arm B OOF.

    PASS requires ALL of:
      1. Identical unique Research_ID sets
      2. Zero true-label mismatches
      3. Zero fold mismatches
      4. Zero predicted-label mismatches
      5. Successful probability comparison within PROB_TOLERANCE
         (probabilities validated for finiteness, [0,1], and row sums;
          columns aligned explicitly by class name, not positional order)

    The reference file must contain all EXPECTED_PROB_COLS.  Missing,
    partial, NaN or infinite probability inputs produce INVALID_INPUT, not
    PASS or UNRESOLVED.

    Probability note
    ----------------
    The Experiment 3 Arm B reference stores probabilities rounded to 4
    decimal places.  PROB_TOLERANCE (0.005) is set to accept 4-dp rounding
    differences without claiming full-precision equality.  Reporting must
    describe this accurately — these are saved-rounded-value comparisons,
    not full-precision bit-for-bit comparisons.
    """
    _require_file(repro_path, "reproduction reference (Experiment 3 Arm B)")

    ref_df = pd.read_csv(repro_path)

    # Required structural columns (True_Label, Fold, Predicted_Label, Research_ID)
    _require_columns(ref_df, REPRO_REF_REQUIRED_COLS, "Experiment 3 Arm B OOF")

    # Expected probability columns are also mandatory in the reference.
    _require_columns(
        ref_df,
        set(EXPECTED_PROB_COLS),
        "Experiment 3 Arm B OOF (probability columns)",
    )

    # ------------------------------------------------------------------ #
    # Step 1: ID-set equality
    # ------------------------------------------------------------------ #
    new_ids = set(arm_a_oof["Research_ID"].tolist())
    ref_ids = set(ref_df["Research_ID"].tolist())
    if new_ids != ref_ids:
        raise ValueError(
            f"ID sets differ between Arm A OOF and reproduction reference. "
            f"Only in new: {sorted(new_ids - ref_ids)[:5]}. "
            f"Only in ref: {sorted(ref_ids - new_ids)[:5]}."
        )

    merged = arm_a_oof.merge(
        ref_df.add_suffix("_ref").rename(columns={"Research_ID_ref": "Research_ID"}),
        on="Research_ID",
        how="inner",
        validate="one_to_one",
    )

    # ------------------------------------------------------------------ #
    # Step 2: True label agreement
    # ------------------------------------------------------------------ #
    true_mm   = merged[merged["True_Label"] != merged["True_Label_ref"]]
    true_label_mm_ids = true_mm["Research_ID"].tolist()

    # ------------------------------------------------------------------ #
    # Step 3: Fold agreement
    # ------------------------------------------------------------------ #
    fold_mm   = merged[merged["Fold"] != merged["Fold_ref"]]
    fold_mm_ids = fold_mm["Research_ID"].tolist()

    # ------------------------------------------------------------------ #
    # Step 4: Predicted label agreement
    # ------------------------------------------------------------------ #
    pred_mm   = merged[merged["Predicted_Label"] != merged["Predicted_Label_ref"]]
    pred_mm_ids = pred_mm["Research_ID"].tolist()

    # ------------------------------------------------------------------ #
    # Step 5: Probability comparison — aligned explicitly by class name
    # ------------------------------------------------------------------ #
    # new OOF must also have the expected prob columns (sanity check)
    _require_columns(
        arm_a_oof,
        set(EXPECTED_PROB_COLS),
        "Arm A OOF (probability columns)",
    )

    prob_validation_error: str | None = None
    max_prob_diff: float | None = None  # None = not yet computed
    prob_within_tolerance: bool = False

    try:
        new_probs = _validate_prob_matrix(merged, "Arm A OOF",
                                          EXPECTED_PROB_COLS)
        ref_prob_cols_in_merged = [c + "_ref" for c in EXPECTED_PROB_COLS]
        # Rename temporarily to validate
        ref_prob_sub = merged[ref_prob_cols_in_merged].rename(
            columns={c + "_ref": c for c in EXPECTED_PROB_COLS}
        )
        ref_probs = _validate_prob_matrix(ref_prob_sub, "Experiment 3 Arm B OOF",
                                          EXPECTED_PROB_COLS)
        max_prob_diff = float(np.abs(new_probs - ref_probs).max())
        prob_within_tolerance = max_prob_diff <= PROB_TOLERANCE
    except ValueError as exc:
        prob_validation_error = str(exc)

    # ------------------------------------------------------------------ #
    # Determine reproduction status
    # All five conditions must pass for PASS.
    # Distinguish INVALID_INPUT (prob validation failure) from UNRESOLVED
    # (valid inputs but value differences).
    # ------------------------------------------------------------------ #
    n_true_mm = len(true_label_mm_ids)
    n_fold_mm = len(fold_mm_ids)
    n_pred_mm = len(pred_mm_ids)

    if prob_validation_error is not None:
        reproduction_status = "INVALID_INPUT"
    elif n_true_mm > 0 or n_fold_mm > 0 or n_pred_mm > 0 or not prob_within_tolerance:
        reproduction_status = "UNRESOLVED"
    else:
        reproduction_status = "PASS"

    # Reference metrics
    ref_acc = round(float(accuracy_score(
        merged["True_Label_ref"], merged["Predicted_Label_ref"]
    )), 4)
    ref_macro_f1 = round(float(f1_score(
        merged["True_Label_ref"], merged["Predicted_Label_ref"],
        average="macro", labels=LABEL_ORDER, zero_division=0
    )), 4)
    new_acc = round(float(accuracy_score(
        merged["True_Label"], merged["Predicted_Label"]
    )), 4)
    new_macro_f1 = round(float(f1_score(
        merged["True_Label"], merged["Predicted_Label"],
        average="macro", labels=LABEL_ORDER, zero_division=0
    )), 4)

    result: dict[str, Any] = {
        "reproduction_status": reproduction_status,
        "description": (
            "Comparison of Experiment 4 Arm A vs saved Experiment 3 Arm B by Research_ID. "
            f"Probability tolerance: {PROB_TOLERANCE} "
            f"(accounts for 4-decimal-place rounding in saved reference; "
            f"row-sum tolerance: {PROB_ROW_SUM_TOLERANCE}). "
            "Probabilities aligned by class name, not column position."
        ),
        "pass_conditions": {
            "identical_id_sets": True,  # always true at this point (raises if not)
            "zero_true_label_mismatches": n_true_mm == 0,
            "zero_fold_mismatches": n_fold_mm == 0,
            "zero_predicted_label_mismatches": n_pred_mm == 0,
            "probabilities_valid_and_within_tolerance": prob_within_tolerance,
            "prob_validation_error": prob_validation_error,
        },
        "prediction_mismatches": {
            "count": n_pred_mm,
            "ids": pred_mm_ids,
        },
        "true_label_mismatches": {
            "count": n_true_mm,
            "ids": true_label_mm_ids,
        },
        "fold_mismatches": {
            "count": n_fold_mm,
            "ids": fold_mm_ids,
        },
        "probability_columns_compared": EXPECTED_PROB_COLS,
        "max_abs_probability_diff": (
            round(max_prob_diff, 6) if max_prob_diff is not None else None
        ),
        "probability_tolerance": PROB_TOLERANCE,
        "probability_row_sum_tolerance": PROB_ROW_SUM_TOLERANCE,
        "probabilities_within_tolerance": prob_within_tolerance,
        "probability_equality_note": (
            "The reference stores probabilities rounded to 4 decimal places. "
            "Max-diff comparison reflects saved-rounded-value differences, "
            "not full-precision floating-point equality."
        ),
        "new_arm_a": {"Accuracy": new_acc, "Macro_F1": new_macro_f1},
        "reference_exp3b": {"Accuracy": ref_acc, "Macro_F1": ref_macro_f1},
        "metric_deltas": {
            "Accuracy": round(new_acc - ref_acc, 4),
            "Macro_F1": round(new_macro_f1 - ref_macro_f1, 4),
        },
    }

    if reproduction_status == "INVALID_INPUT":
        result["investigation_note"] = (
            "Probability inputs failed validation. "
            f"Error: {prob_validation_error} "
            "Fix invalid inputs before interpreting the reproduction status."
        )
    elif reproduction_status == "UNRESOLVED":
        reasons = []
        if n_true_mm > 0:
            reasons.append(f"{n_true_mm} true-label mismatch(es)")
        if n_fold_mm > 0:
            reasons.append(f"{n_fold_mm} fold mismatch(es)")
        if n_pred_mm > 0:
            reasons.append(f"{n_pred_mm} predicted-label mismatch(es)")
        if not prob_within_tolerance and max_prob_diff is not None:
            reasons.append(
                f"max |prob diff|={max_prob_diff:.6f} exceeds tolerance {PROB_TOLERANCE}"
            )
        result["investigation_note"] = (
            f"Reproduction check UNRESOLVED: {'; '.join(reasons)}. "
            "Model settings were not changed. "
            "Investigate before reporting Arm A as a clean reproduction."
        )

    with open(output_path, "w", encoding="utf-8") as f:
        json.dump(result, f, indent=2)

    logger.info(
        "Reproduction check: %s — true_mm=%d fold_mm=%d pred_mm=%d max_prob_diff=%s",
        reproduction_status, n_true_mm, n_fold_mm, n_pred_mm,
        f"{max_prob_diff:.6f}" if max_prob_diff is not None else "N/A",
    )
    return result


# ---------------------------------------------------------------------------
# Fold manifest
# ---------------------------------------------------------------------------

def save_fold_manifest(
    baseline_df: pd.DataFrame,
    fold_map: dict[str, int],
    cg_map: dict[str, str],
    output_path: Path,
) -> pd.DataFrame:
    """
    Save the baseline-only fold manifest for Arm A.
    Records which baseline IDs are in each fold's evaluation set.
    """
    rows = []
    for _, row in baseline_df.iterrows():
        rid = row[ID_COLUMN]
        rows.append({
            "Research_ID": rid,
            "Combined_Group": cg_map[rid],
            "Evaluation_Fold": fold_map[rid],
            "In_Arm_A_Training": True,
            "In_Arm_A_Evaluation": True,
            "Arm_A_Notes": "Baseline record; appears in training for all folds except Evaluation_Fold.",
            "Synthetic_Families_In_Training": "N/A — Arm A has no synthetic augmentation.",
        })
    manifest = pd.DataFrame(rows)
    manifest.to_csv(output_path, index=False)
    logger.info("Baseline fold manifest saved to '%s'", output_path)
    return manifest


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def run_experiment4_arm_a(output_dir: Path | None = None) -> dict[str, Any]:
    """Execute Experiment 4 Arm A (cleaned-text baseline, no augmentation)."""
    logger.info("=== Component 4 GovernanceIntelligence — Experiment 4 Arm A ===")
    logger.info("Arm A: BoilerplateStripper + TF-IDF + LR, baseline records only.")
    logger.info("Reproduction reference: Experiment 3 Arm B.")

    # --- Resolve output directory ---
    if output_dir is None:
        output_dir = resolve_exp4_output_dir(config.RESULTS_DIR)
    else:
        # An explicitly supplied directory must be empty (or not yet created).
        # This prevents accidentally overwriting a previous run.
        output_dir = Path(output_dir)
        if _dir_is_occupied(output_dir):
            raise ValueError(
                f"Explicit output directory is not empty: {output_dir}. "
                "Supply an empty or non-existent directory, or omit output_dir "
                "to use automatic run numbering."
            )
        output_dir.mkdir(parents=True, exist_ok=True)
    arm_a_dir = output_dir / "arm_a"

    # --- Load and validate inputs ---
    # 1. Candidate dataset → baseline rows
    df = load_and_filter_baseline(CANDIDATE_CSV_PATH)
    # Production enforcement: the baseline evaluation population must be exactly 200.
    n_baseline = df[ID_COLUMN].nunique()
    if n_baseline != 200:
        raise ValueError(
            f"Expected exactly 200 unique baseline Research_IDs in the candidate dataset; "
            f"got {n_baseline}. Verify the candidate CSV contains the correct 200 baseline records."
        )
    candidate_sha256 = sha256_file(CANDIDATE_CSV_PATH)

    # 2. Original 200-row dataset comparison
    orig_path = config.DATASET_PATH
    baseline_check = verify_baseline_vs_original(df, orig_path)
    orig_sha256 = sha256_file(orig_path) if orig_path.exists() else "unavailable"

    # 3. Frozen fold reference (Experiment 2 OOF)
    _require_file(EXP2_OOF_PATH, "Experiment 2 OOF (frozen fold reference)")
    exp2_oof = pd.read_csv(EXP2_OOF_PATH)
    exp2_sha256 = sha256_file(EXP2_OOF_PATH)
    fold_map = validate_fold_reference(df, exp2_oof)

    # Join Combined_Group onto df
    cg_map = dict(zip(exp2_oof[ID_COLUMN], exp2_oof["Combined_Group"]))
    df = df.copy()
    df["Combined_Group"] = df[ID_COLUMN].map(cg_map)
    n_null_cg = df["Combined_Group"].isna().sum()
    if n_null_cg > 0:
        raise ValueError(f"{n_null_cg} baseline records have null Combined_Group after join.")

    # 4. Reproduction reference (Experiment 3 Arm B OOF)
    #    Validate structural columns, expected prob columns, and fold agreement
    #    with the frozen Experiment 2 fold assignments \u2014 all before any fitting.
    _require_file(EXP3B_OOF_PATH, "Experiment 3 Arm B OOF (reproduction reference)")
    exp3b_sha256 = sha256_file(EXP3B_OOF_PATH)
    exp3b_oof = pd.read_csv(EXP3B_OOF_PATH)
    _require_columns(exp3b_oof, REPRO_REF_REQUIRED_COLS, "Experiment 3 Arm B OOF")
    _require_columns(exp3b_oof, set(EXPECTED_PROB_COLS), "Experiment 3 Arm B OOF (probability columns)")

    # Verify Experiment 3 Arm B fold assignments match frozen Experiment 2 folds
    # for all baseline IDs.  Mismatch would indicate a reference file from a
    # different run or a corrupted artifact.
    exp3b_fold_map = dict(zip(exp3b_oof[ID_COLUMN], exp3b_oof["Fold"]))
    exp3b_ids = set(exp3b_oof[ID_COLUMN].tolist())
    baseline_ids_set = set(df[ID_COLUMN].tolist())
    if exp3b_ids != baseline_ids_set:
        only_exp3b = sorted(exp3b_ids - baseline_ids_set)[:5]
        only_base  = sorted(baseline_ids_set - exp3b_ids)[:5]
        raise ValueError(
            f"Experiment 3 Arm B OOF ID set does not match baseline IDs. "
            f"Only in Exp3B: {only_exp3b}. Only in baseline: {only_base}."
        )
    fold_mismatches_exp3b = [
        rid for rid in baseline_ids_set
        if exp3b_fold_map.get(rid) != fold_map.get(rid)
    ]
    if fold_mismatches_exp3b:
        raise ValueError(
            f"{len(fold_mismatches_exp3b)} Experiment 3 Arm B fold assignment(s) "
            f"differ from frozen Experiment 2 folds: {sorted(fold_mismatches_exp3b)[:10]}. "
            "The reference file must use the same fold assignments as the frozen Exp2 folds."
        )
    logger.info(
        "Experiment 3 Arm B fold assignments verified against frozen Experiment 2 folds: "
        "all %d IDs agree.", len(baseline_ids_set)
    )


    logger.info("All required input files present and validated.")

    # --- Save baseline fold manifest ---
    manifest_path = output_dir / "baseline_fold_manifest.csv"
    save_fold_manifest(df, fold_map, cg_map, manifest_path)

    # --- Run Arm A ---
    arm_a_results = run_arm_a(
        arm_dir=arm_a_dir,
        df=df,
        fold_map=fold_map,
        cg_map=cg_map,
    )

    # --- Reproduction check ---
    repro_path   = output_dir / "reproduction_check.json"
    repro_result = run_reproduction_check(
        arm_a_oof=arm_a_results["oof_df"],
        repro_path=EXP3B_OOF_PATH,
        output_path=repro_path,
    )

    # --- Run metadata ---
    import sklearn

    # Record actual pipeline parameters from get_params() — not a hardcoded dict.
    # An unfitted pipeline is used here because get_params() is available before
    # fitting and reflects the construction-time configuration.
    _ref_pipeline = build_pipeline_with_cleaner()

    def _safe_json(val: Any) -> Any:
        """
        Convert a value to a JSON-safe type.

        Handles scalar primitives, tuples (→ lists), lists, and dicts
        recursively.  sklearn estimator objects, numpy types, and any other
        unknown types are converted to repr strings so the metadata remains
        informative without raising a serialisation error.
        """
        import math
        if val is None or isinstance(val, bool):
            return val
        if isinstance(val, int):
            return val
        if isinstance(val, float):
            return val if math.isfinite(val) else str(val)
        if isinstance(val, str):
            return val
        if isinstance(val, tuple):
            return [_safe_json(v) for v in val]
        if isinstance(val, list):
            return [_safe_json(v) for v in val]
        if isinstance(val, dict):
            return {k: _safe_json(v) for k, v in val.items()}
        # sklearn estimators, numpy dtypes, type objects, etc. → repr string
        return repr(val)

    raw_params = _ref_pipeline.get_params(deep=True)
    pipeline_params_json = {k: _safe_json(v) for k, v in sorted(raw_params.items())}


    run_metadata = {
        "experiment": "Experiment 4 Arm A",
        "description": (
            "Cleaned-text TF-IDF + LR on 200 baseline records, "
            "frozen Experiment 2/3 fold assignments replayed. "
            "No synthetic or source-derived records in training or evaluation. "
            "Reproduction reference: Experiment 3 Arm B."
        ),
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "important_caveat": (
            "Development cross-validation results on 200 baseline records. "
            "Frozen folds are replayed, not freshly generated. "
            "Not untouched held-out test results."
        ),
        "input_files": {
            "candidate_dataset": {
                "path": str(CANDIDATE_CSV_PATH),
                "sha256": candidate_sha256,
                "baseline_rows_selected": 200,
                "origin_filter": BASELINE_ORIGIN,
            },
            "original_dataset": {
                "path": str(orig_path),
                "sha256": orig_sha256,
            },
            "frozen_fold_reference": {
                "path": str(EXP2_OOF_PATH),
                "sha256": exp2_sha256,
                "description": "Experiment 2 OOF — Combined_Group and Fold columns used as frozen inputs.",
            },
            "reproduction_reference": {
                "path": str(EXP3B_OOF_PATH),
                "sha256": exp3b_sha256,
                "description": "Experiment 3 Arm B OOF — used for prediction-level comparison only.",
            },
        },
        "baseline_vs_original": baseline_check,
        "pipeline": {
            "steps": [s for s, _ in _ref_pipeline.steps],
            "factory": "boilerplate_transformer.build_pipeline_with_cleaner()",
            "boilerplate_phrases": list(BOILERPLATE_PHRASES),
            "params_from_get_params": pipeline_params_json,
            "note": "Parameters recorded from pipeline.get_params(); not a separate hardcoded dict.",
        },
        "fold_replay": {
            "source": str(EXP2_OOF_PATH),
            "description": "Exact Experiment 2/3 Combined_Group fold assignments replayed. No new splitting performed.",
            "unique_folds": [1, 2, 3, 4, 5],
            "fold_counts": {
                str(f): int(sum(1 for v in fold_map.values() if v == f))
                for f in range(1, 6)
            },
        },
        "synthetic_records": {
            "in_training": 0,
            "in_evaluation": 0,
            "note": "Arm A excludes all synthetic records. Arm B (not yet implemented) will add reviewed eligible families.",
        },
        "source_derived_records": {
            "in_training": 0,
            "in_evaluation": 0,
            "note": "Source-derived records are excluded from Experiment 4.",
        },
        "environment": {
            "python_version": sys.version,
            "sklearn_version": sklearn.__version__,
            "pandas_version": pd.__version__,
            "numpy_version": np.__version__,
            "git": get_git_info(),
        },
        "output_directory": str(output_dir),
        "reproduction_check": repro_result,
        "arm_a_metrics": arm_a_results["overall_metrics"],
    }

    with open(output_dir / "run_metadata.json", "w", encoding="utf-8") as f:
        json.dump(run_metadata, f, indent=2)

    logger.info("Experiment 4 Arm A complete. Artifacts written to '%s'", output_dir)
    return {
        "output_dir": output_dir,
        "arm_a_results": arm_a_results,
        "reproduction_check": repro_result,
        "run_metadata": run_metadata,
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser(
        description="Experiment 4: Synthetic Augmentation Comparison"
    )
    parser.add_argument(
        "--arm",
        choices=["a"],
        default="a",
        help="Which arm to run. Currently only 'a' is implemented.",
    )
    args = parser.parse_args()

    if args.arm == "a":
        result = run_experiment4_arm_a()
        m = result["arm_a_results"]["overall_metrics"]
        r = result["reproduction_check"]
        out = result["output_dir"]

        print("\n" + "=" * 70)
        print("EXPERIMENT 4 ARM A — COMPLETE")
        print("=" * 70)
        print(f"  Output directory       : {out}")
        print(f"  Accuracy               : {m['Accuracy']:.4f}")
        print(f"  Macro-F1 (sklearn)     : {m['Macro_F1_sklearn']:.4f}")
        print(f"  Weighted-F1            : {m['Weighted_F1']:.4f}")
        print(f"  Misclassified          : {m['Misclassified']} / 200")
        print()
        print(f"  Reproduction vs Exp 3 Arm B: {r['reproduction_status']}")
        print(f"  Prediction mismatches  : {r['prediction_mismatches']['count']}")
        print(f"  Max |prob diff|        : {r['max_abs_probability_diff']:.6f}")
        print(f"  Accuracy delta         : {r['metric_deltas']['Accuracy']}")
        print(f"  Macro-F1 delta         : {r['metric_deltas']['Macro_F1']}")
        if r.get("investigation_note"):
            print(f"\n  NOTE: {r['investigation_note']}")
        print()
        print("  CAUTION: Development CV results only. Frozen folds replayed.")
        print("           Not untouched held-out test results.")
        print("=" * 70)
