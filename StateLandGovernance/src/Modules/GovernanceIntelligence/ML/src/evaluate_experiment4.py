"""
evaluate_experiment4.py
-----------------------
EXPERIMENT 4 — Synthetic Augmentation Comparison.

Entry point supports an explicit --arm flag and, for Arm B, a --review-file
path and an optional --dry-run flag.

Arm A (Baseline — Cleaned Text, No Augmentation)
-------------------------------------------------
Reproduces Experiment 3 Arm B:
  BoilerplateStripper → TfidfVectorizer → LogisticRegression
  using frozen Experiment 2/3 Combined_Group fold assignments,
  on the 200 "Existing baseline" rows selected from the 530-row
  candidate dataset.

No synthetic or source-derived rows enter training or evaluation.

Arm B (Synthetic Augmentation)
-------------------------------
Same pipeline as Arm A.  For each fold, eligible reviewed synthetic
families whose related baseline records are NOT in the evaluation set
are appended to the training data only.  Evaluation is always on the
identical 200 baseline OOF records.

Eligibility requires Review_Decision = Approved or Label_Change with a
valid four-class Approved_Label, nonblank Reviewer, parseable Review_Date,
and Variant_Consistency in {Consistent, Minor_variation}.

Dry-run mode validates inputs and produces the per-fold manifest without
fitting any model.

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
from review_loader import (
    FamilyReview,
    RELATED_IDS_DELIMITER,
    build_fold_manifest,
    load_and_validate_reviews,
    load_and_validate_reviews_from_bytes,
    validate_related_baseline_ids,
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
# Selected absolute comparison tolerance for two independently stored 4-dp
# values that represent the same underlying probability:
#   PROB_TOLERANCE = 0.0001
#
# This is the tightest correct tolerance for comparing two 4-dp OOF files
# produced from the same model.  Values above this threshold indicate
# genuine structural divergence, not rounding.
# The comparison uses np.abs() (absolute difference, rtol=0).
PROB_TOLERANCE: float = 0.0001

# Row-sum tolerance for a single 4-decimal-place probability distribution.
#
# With 4 class probabilities each rounded to 4dp, worst-case row-sum error:
#   4 × 0.00005 = 0.0002
#
# We add a numerical allowance of 0.00001 (one ten-thousandth of the row)
# to accept valid 4-dp distributions without false positives:
#   0.0002 + 0.00001 = 0.00021
#
# 0.00001 is a practical numerical allowance, not one ULP in the IEEE
# floating-point sense.
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
    Both the Arm A OOF and the Experiment 3 Arm B reference store probabilities
    rounded to 4 decimal places.  PROB_TOLERANCE (0.0001) is the maximum
    absolute difference between two saved 4-dp representations of the same
    underlying float (2 × 0.00005).  Differences above 0.0001 indicate genuine
    structural divergence.  The comparison uses np.abs() with no relative
    component (rtol=0).  Reporting must describe this accurately — these are
    saved-rounded-value comparisons, not full-precision floating-point equality.
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
            "(absolute difference, rtol=0; equal to the maximum difference between two "
            "saved 4-dp representations of the same float: 2 × 0.00005 = 0.0001). "
            f"Row-sum tolerance: {PROB_ROW_SUM_TOLERANCE} "
            "(4 × 0.00005 worst-case row-sum error + 1 ULP buffer). "
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
            "Both the Arm A OOF and the Experiment 3 Arm B reference store probabilities "
            "rounded to 4 decimal places. Max-diff is an absolute comparison (rtol=0). "
            "A max-diff of 0.0 means the two files store the same rounded 4-dp values. "
            "A nonzero max-diff up to 0.0001 is within the expected rounding band for "
            "two 4-dp rounded representations of the same float. "
            "This is a saved-rounded-value comparison, not full-precision floating-point equality."
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
# Arm B CV runner (synthetic augmentation)
# ---------------------------------------------------------------------------

def run_arm_b(
    arm_dir: Path,
    baseline_df: pd.DataFrame,
    fold_map: dict[str, int],
    cg_map: dict[str, str],
    reviews: list[FamilyReview],
    candidate_df: pd.DataFrame,
    arm_label: str = "Arm B",
) -> dict[str, Any]:
    """
    Execute Arm B: same pipeline as Arm A, with eligible reviewed synthetic
    families appended to each fold's training data.

    Evaluation is always on the 200 baseline OOF records only.
    Synthetic and source-derived records never enter evaluation.

    For each fold:
      - baseline training rows: baseline records not in the evaluation fold
      - eligible synthetic families: those whose Related_Baseline_IDs do not
        map to a baseline record in the evaluation set for this fold
      - all three variants of each eligible family are appended in deterministic
        family/variant order (family_id ASC, then V1, V2, V3)
      - TF-IDF is fitted on training rows only (baseline + synthetic)
      - evaluation is performed on baseline test rows only

    Notes
    -----
    class_weight='balanced' is inherited from the existing pipeline.  Adding
    synthetic records will change the automatically computed per-class weights
    even though the configuration string is unchanged.

    Source-derived records (SRC-* prefix) are excluded regardless of review.
    Review metadata, labels, IDs, and source metadata are never included
    in text features.

    Raises
    ------
    ValueError
        If no eligible synthetic families are available for any fold when
        actual training is requested (not dry-run).  Dry-run is handled
        by the caller before entering this function.
    """
    arm_dir.mkdir(parents=True, exist_ok=True)

    SYNTHETIC_ORIGIN = "New synthetic scenario"
    SOURCE_DERIVED_ORIGIN = "New source-derived candidate"

    # Build lookup: Research_ID -> (text, effective_label) for each eligible family
    # from the candidate dataset (source of truth for texts)
    cand_text_map: dict[str, str] = {
        row[ID_COLUMN]: row[TEXT_COLUMN]
        for _, row in candidate_df.iterrows()
        if row.get("Record_Origin") == SYNTHETIC_ORIGIN
    }

    # Index eligible reviews by family_id
    eligible_reviews = [r for r in reviews if r.eligible]

    # Build lookup: family_id -> sorted [rid_v1, rid_v2, rid_v3]
    # from the family review (deterministic order)
    family_ids_map: dict[str, list[str]] = {
        r.family_id: r.research_ids  # already in V1,V2,V3 order from pack
        for r in eligible_reviews
    }
    family_label_map: dict[str, str] = {
        r.family_id: r.effective_label  # type: ignore[assignment]
        for r in eligible_reviews
    }

    # Map baseline Related_IDs -> CombinedGroup -> evaluation fold
    baseline_id_set = set(baseline_df[ID_COLUMN].tolist())
    id_to_cg = {rid: cg_map[rid] for rid in baseline_id_set if rid in cg_map}
    id_to_fold = {rid: fold_map[rid] for rid in baseline_id_set}
    cg_to_eval_fold: dict[str, int] = {}
    for rid, cg in id_to_cg.items():
        cg_to_eval_fold[cg] = id_to_fold[rid]

    rids   = baseline_df[ID_COLUMN].tolist()
    y      = np.array(baseline_df[TARGET_COLUMN].tolist())
    X_raw  = np.array(baseline_df[TEXT_COLUMN].tolist())
    groups = np.array([cg_map[rid] for rid in rids])

    # Apply boilerplate cleaning to baseline texts
    X = np.array([strip_research_boilerplate(t) for t in X_raw])

    unique_folds = sorted(set(fold_map.values()))
    splits: list[tuple[np.ndarray, np.ndarray]] = []
    for fold_idx in unique_folds:
        test_idx  = np.array([i for i, rid in enumerate(rids) if fold_map.get(rid) == fold_idx])
        train_idx = np.array([i for i in range(len(rids)) if i not in set(test_idx.tolist())])
        splits.append((train_idx, test_idx))

    # Validate baseline fold integrity (same checks as Arm A)
    all_test = []
    for _, test_idx in splits:
        all_test.extend(test_idx.tolist())
    if len(all_test) != len(baseline_df):
        raise AssertionError(
            f"{arm_label} test index count {len(all_test)} != dataset size {len(baseline_df)}"
        )
    if len(set(all_test)) != len(baseline_df):
        raise AssertionError(f"{arm_label}: some baseline records appear in multiple test folds.")

    for fold_idx, (train_idx, test_idx) in enumerate(splits, start=1):
        trn_cg = set(groups[train_idx])
        tst_cg = set(groups[test_idx])
        if trn_cg & tst_cg:
            raise AssertionError(
                f"{arm_label} Fold {fold_idx}: baseline Combined_Group overlap detected."
            )

    oof_records: list[dict[str, Any]] = []
    fold_results: list[dict[str, Any]] = []
    fold_augmentation_summary: list[dict[str, Any]] = []

    for fold_idx, (train_idx, test_idx) in enumerate(splits, start=1):
        # Determine evaluation CombinedGroups for this fold
        eval_cgs = set(groups[test_idx])

        # Determine eligible families for this fold:
        # exclude any family whose Related_Baseline_IDs map to an evaluation CG
        fold_eligible: list[FamilyReview] = []
        fold_excluded_families: list[str] = []
        for rev in eligible_reviews:
            related_eval_folds = set()
            for related_rid in rev.related_baseline_ids:
                cg = id_to_cg.get(related_rid)
                if cg is not None:
                    related_eval_folds.add(cg_to_eval_fold.get(cg, -1))
            if fold_idx in related_eval_folds:
                fold_excluded_families.append(rev.family_id)
            else:
                fold_eligible.append(rev)

        # Build augmentation training rows
        aug_texts: list[str] = []
        aug_labels: list[str] = []
        aug_family_ids: list[str] = []

        # Deterministic order: family_id ASC, then variants in V1/V2/V3 order
        for rev in sorted(fold_eligible, key=lambda r: r.family_id):
            for rid in rev.research_ids:
                raw_text = cand_text_map.get(rid, "")
                cleaned = strip_research_boilerplate(raw_text)
                aug_texts.append(cleaned)
                aug_labels.append(rev.effective_label)  # type: ignore[arg-type]
                aug_family_ids.append(rev.family_id)

        n_syn_families = len(fold_eligible)
        n_syn_records  = len(aug_texts)

        # Combine baseline training + synthetic augmentation
        X_base_train = X[train_idx]
        y_base_train = y[train_idx]

        if aug_texts:
            X_train = np.concatenate([X_base_train, np.array(aug_texts)])
            y_train = np.concatenate([y_base_train, np.array(aug_labels)])
        else:
            X_train = X_base_train
            y_train = y_base_train

        # Evaluation: baseline test rows only
        X_test = X[test_idx]
        y_test = y[test_idx]

        # Verify no synthetic text entered evaluation
        assert len(X_test) == len(test_idx), (
            f"{arm_label} Fold {fold_idx}: evaluation size mismatch."
        )

        pipeline = build_pipeline_with_cleaner()
        pipeline.fit(X_train, y_train)
        y_pred  = pipeline.predict(X_test)
        y_prob  = pipeline.predict_proba(X_test)

        clf_classes: list[str] = pipeline.named_steps["clf"].classes_.tolist()
        missing_classes = [c for c in LABEL_ORDER if c not in clf_classes]

        f_acc      = round(float(accuracy_score(y_test, y_pred)), 4)
        f_macro_f1 = round(float(f1_score(y_test, y_pred, average="macro",
                                           labels=LABEL_ORDER, zero_division=0)), 4)
        f_wt_f1    = round(float(f1_score(y_test, y_pred, average="weighted",
                                           labels=LABEL_ORDER, zero_division=0)), 4)

        logger.info(
            "[%s] Fold %d/%d — base_train=%d, syn_records=%d, test=%d — "
            "Acc=%.4f, Macro-F1=%.4f",
            arm_label, fold_idx, len(unique_folds),
            len(train_idx), n_syn_records, len(test_idx),
            f_acc, f_macro_f1,
        )

        test_class_counts = {lbl: int((y_test == lbl).sum()) for lbl in LABEL_ORDER}
        fold_results.append({
            "Fold": fold_idx,
            "Baseline_Train_Size": int(len(train_idx)),
            "Synthetic_Families": n_syn_families,
            "Synthetic_Records": n_syn_records,
            "Test_Size": len(test_idx),
            "Accuracy": f_acc,
            "Macro_F1": f_macro_f1,
            "Weighted_F1": f_wt_f1,
            **{
                f"Test_{lbl.replace('/', '_').replace(' ', '_')}": cnt
                for lbl, cnt in test_class_counts.items()
            },
        })

        fold_augmentation_summary.append({
            "Fold": fold_idx,
            "Eligible_Families": n_syn_families,
            "Eligible_Records": n_syn_records,
            "Excluded_Families": len(fold_excluded_families),
            "Excluded_Families_IDs": "; ".join(sorted(fold_excluded_families)),
        })

        for i, row_idx in enumerate(test_idx):
            proba_dict = {
                f"prob_{cls.replace('/', '_').replace(' ', '_')}": round(float(y_prob[i, j]), 4)
                for j, cls in enumerate(clf_classes)
            }
            for missing_cls in missing_classes:
                col = f"prob_{missing_cls.replace('/', '_').replace(' ', '_')}"
                proba_dict[col] = 0.0
            oof_records.append({
                "Research_ID":     rids[row_idx],
                "Combined_Group":  groups[row_idx],
                "True_Label":      y[row_idx],
                "Predicted_Label": y_pred[i],
                "Fold":            fold_idx,
                **proba_dict,
            })

    oof_df = pd.DataFrame(oof_records)
    id_order = {rid: idx for idx, rid in enumerate(rids)}
    oof_df["_sort_key"] = oof_df["Research_ID"].map(id_order)
    oof_df = oof_df.sort_values("_sort_key").drop(columns=["_sort_key"])

    y_true_oof = oof_df["True_Label"].tolist()
    y_pred_oof = oof_df["Predicted_Label"].tolist()

    overall_acc      = round(float(accuracy_score(y_true_oof, y_pred_oof)), 4)
    overall_macro_f1 = round(float(f1_score(y_true_oof, y_pred_oof, average="macro",
                                             labels=LABEL_ORDER, zero_division=0)), 4)
    overall_wt_f1    = round(float(f1_score(y_true_oof, y_pred_oof, average="weighted",
                                             labels=LABEL_ORDER, zero_division=0)), 4)
    overall_macro_p  = round(float(precision_score(y_true_oof, y_pred_oof, average="macro",
                                                    labels=LABEL_ORDER, zero_division=0)), 4)
    overall_macro_r  = round(float(recall_score(y_true_oof, y_pred_oof, average="macro",
                                                 labels=LABEL_ORDER, zero_division=0)), 4)
    misclassified    = int((np.array(y_true_oof) != np.array(y_pred_oof)).sum())

    per_class = compute_per_class_metrics(y_true_oof, y_pred_oof)
    cm        = confusion_matrix(y_true_oof, y_pred_oof, labels=LABEL_ORDER)

    logger.info(
        "[%s] OOF Pooled — Acc=%.4f, Macro-F1=%.4f, Weighted-F1=%.4f, Errors=%d/200",
        arm_label, overall_acc, overall_macro_f1, overall_wt_f1, misclassified,
    )

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
        "fold_augmentation_summary": fold_augmentation_summary,
        "confusion_matrix": cm.tolist(),
        "oof_df": oof_df,
    }


def build_paired_comparison(
    arm_a_oof: pd.DataFrame,
    arm_b_oof: pd.DataFrame,
) -> tuple[dict[str, Any], pd.DataFrame]:
    """
    Build a paired prediction comparison between Arm A and Arm B.

    Both OOF DataFrames must have identical baseline Research_ID sets and
    matching True_Label and Fold values.  Research_IDs must be nonblank
    and unique.  Alignment is by Research_ID, not row position.

    Required columns: Research_ID, True_Label, Predicted_Label, Fold.

    Returns
    -------
    (summary_dict, transitions_df)

    summary_dict keys
    -----------------
    total_records, arm_a_correct, arm_b_correct,
    helped (= A_wrong_B_correct), hurt (= A_correct_B_wrong),
    net_gain (= helped − hurt = arm_b_correct − arm_a_correct),
    both_correct, both_wrong_same_error, both_wrong_different_error,
    both_wrong_any, label_changes

    transitions_df columns
    ----------------------
    Research_ID, True_Label, Arm_A_Pred, Arm_B_Pred,
    Arm_A_Correct, Arm_B_Correct, Transition, Fold
    """
    REQUIRED_COLS = {"Research_ID", "True_Label", "Predicted_Label", "Fold"}
    errors: list[str] = []

    for arm_label, df in (("Arm A", arm_a_oof), ("Arm B", arm_b_oof)):
        missing = REQUIRED_COLS - set(df.columns)
        if missing:
            errors.append(
                f"{arm_label} OOF missing required columns: {sorted(missing)}"
            )
    if errors:
        raise ValueError(
            "build_paired_comparison: missing required columns:\n"
            + "\n".join(f"  - {e}" for e in errors)
        )

    # Validate Research_IDs: nonblank, unique
    for arm_label, df in (("Arm A", arm_a_oof), ("Arm B", arm_b_oof)):
        blank_ids = df["Research_ID"].isna().sum() + (
            df["Research_ID"].fillna("").str.strip() == ""
        ).sum()
        if blank_ids > 0:
            errors.append(
                f"{arm_label} OOF has {blank_ids} blank/null Research_ID value(s)."
            )
        dup_ids = df["Research_ID"].duplicated().sum()
        if dup_ids > 0:
            errors.append(
                f"{arm_label} OOF has {dup_ids} duplicate Research_ID value(s)."
            )
    if errors:
        raise ValueError(
            "build_paired_comparison: Research_ID integrity failures:\n"
            + "\n".join(f"  - {e}" for e in errors)
        )

    # Identical ID sets
    a_ids = set(arm_a_oof["Research_ID"].tolist())
    b_ids = set(arm_b_oof["Research_ID"].tolist())
    if a_ids != b_ids:
        only_a = sorted(a_ids - b_ids)[:5]
        only_b = sorted(b_ids - a_ids)[:5]
        raise ValueError(
            f"Arm A and Arm B OOF Research_ID sets differ. "
            f"Only in A: {only_a}. Only in B: {only_b}."
        )

    # Merge by Research_ID (not by row position)
    merged = arm_a_oof[["Research_ID", "True_Label", "Predicted_Label", "Fold"]].rename(
        columns={"Predicted_Label": "Arm_A_Pred"}
    ).merge(
        arm_b_oof[["Research_ID", "True_Label", "Predicted_Label", "Fold"]].rename(
            columns={"Predicted_Label": "Arm_B_Pred",
                     "True_Label": "True_Label_B",
                     "Fold": "Fold_B"}
        ),
        on="Research_ID", how="inner", validate="one_to_one",
    )

    # True_Label must agree by Research_ID
    label_mm = merged[merged["True_Label"] != merged["True_Label_B"]]
    if len(label_mm) > 0:
        raise ValueError(
            f"True_Label disagrees between Arm A and Arm B OOF for "
            f"{len(label_mm)} Research_ID(s): "
            f"{label_mm['Research_ID'].tolist()[:5]}"
        )

    # Fold must agree by Research_ID
    fold_mm = merged[merged["Fold"] != merged["Fold_B"]]
    if fold_mm is not None and len(fold_mm) > 0:
        raise ValueError(
            f"Fold assignment disagrees between Arm A and Arm B OOF for "
            f"{len(fold_mm)} Research_ID(s): "
            f"{fold_mm['Research_ID'].tolist()[:5]}"
        )

    merged = merged.drop(columns=["True_Label_B", "Fold_B"])

    # Validate label vocabulary for both arms
    label_vocab = set(LABEL_ORDER)
    for arm_label, pred_col in (("Arm A", "Arm_A_Pred"), ("Arm B", "Arm_B_Pred")):
        invalid = set(merged[pred_col].tolist()) - label_vocab
        if invalid:
            raise ValueError(
                f"{arm_label} OOF Predicted_Label contains values outside the "
                f"four-class taxonomy: {sorted(invalid)}"
            )
    invalid_true = set(merged["True_Label"].tolist()) - label_vocab
    if invalid_true:
        raise ValueError(
            f"OOF True_Label contains values outside the four-class taxonomy: "
            f"{sorted(invalid_true)}"
        )

    merged["Arm_A_Correct"] = merged["True_Label"] == merged["Arm_A_Pred"]
    merged["Arm_B_Correct"] = merged["True_Label"] == merged["Arm_B_Pred"]

    def _transition(row: pd.Series) -> str:
        a_ok = row["Arm_A_Correct"]
        b_ok = row["Arm_B_Correct"]
        a_pred = row["Arm_A_Pred"]
        b_pred = row["Arm_B_Pred"]
        if not a_ok and b_ok:
            return "A_wrong_B_correct"
        if a_ok and not b_ok:
            return "A_correct_B_wrong"
        if a_ok and b_ok:
            return "both_correct"
        # both wrong
        if a_pred != b_pred:
            return "both_wrong_different_error"
        return "both_wrong_same_error"

    merged["Transition"] = merged.apply(_transition, axis=1)

    helped = int((merged["Transition"] == "A_wrong_B_correct").sum())
    hurt   = int((merged["Transition"] == "A_correct_B_wrong").sum())
    both_correct = int((merged["Arm_A_Correct"] & merged["Arm_B_Correct"]).sum())
    both_wrong_any = int((~merged["Arm_A_Correct"] & ~merged["Arm_B_Correct"]).sum())
    label_changes = int((merged["Arm_A_Pred"] != merged["Arm_B_Pred"]).sum())

    # Verify arithmetic: net_gain = B_correct - A_correct
    a_correct_total = int(merged["Arm_A_Correct"].sum())
    b_correct_total = int(merged["Arm_B_Correct"].sum())
    assert b_correct_total - a_correct_total == helped - hurt, (
        f"Paired arithmetic check failed: "
        f"B_correct - A_correct = {b_correct_total - a_correct_total}, "
        f"helped - hurt = {helped - hurt}"
    )

    summary = {
        "total_records": len(merged),
        "arm_a_correct": a_correct_total,
        "arm_b_correct": b_correct_total,
        "helped": helped,
        "hurt": hurt,
        "net_gain": helped - hurt,
        "both_correct": both_correct,
        "both_wrong_any": both_wrong_any,
        "both_wrong_same_error": int((merged["Transition"] == "both_wrong_same_error").sum()),
        "both_wrong_different_error": int((merged["Transition"] == "both_wrong_different_error").sum()),
        "label_changes": label_changes,
        "note": (
            "helped = A_wrong_B_correct; hurt = A_correct_B_wrong. "
            "net_gain = helped - hurt = arm_b_correct - arm_a_correct. "
            "label_changes includes both_wrong_different_error transitions. "
            "Both arms evaluated on identical 200 baseline OOF records."
        ),
    }

    transitions_df = merged[["Research_ID", "True_Label", "Arm_A_Pred", "Arm_B_Pred",
                              "Arm_A_Correct", "Arm_B_Correct", "Transition", "Fold"]]
    return summary, transitions_df



# ---------------------------------------------------------------------------
# Arm A metric recomputation and validation
# ---------------------------------------------------------------------------

# Required comparison fields in a saved Arm A metrics.json.
_ARM_A_REQUIRED_METRICS_FIELDS: list[str] = [
    "Accuracy",
    "Macro_F1_sklearn",
    "Weighted_F1",
    "Misclassified",
]

# Rounding precision used when writing and comparing stored metrics
_METRICS_STORED_PRECISION: int = 4


def _recompute_arm_a_metrics(
    arm_a_oof: pd.DataFrame,
) -> dict[str, Any]:
    """
    Recompute Arm A summary metrics directly from OOF True_Label and
    Predicted_Label columns using the same definitions and precision as
    the Arm B runner.

    Raises ValueError if required columns are absent.
    """
    for col in ("True_Label", "Predicted_Label"):
        if col not in arm_a_oof.columns:
            raise ValueError(
                f"Cannot recompute Arm A metrics: column '{col}' missing from OOF DataFrame."
            )
    y_true = arm_a_oof["True_Label"].tolist()
    y_pred = arm_a_oof["Predicted_Label"].tolist()
    acc      = round(float(accuracy_score(y_true, y_pred)), _METRICS_STORED_PRECISION)
    macro_f1 = round(float(f1_score(y_true, y_pred, average="macro",
                                     labels=LABEL_ORDER, zero_division=0)),
                     _METRICS_STORED_PRECISION)
    wt_f1    = round(float(f1_score(y_true, y_pred, average="weighted",
                                     labels=LABEL_ORDER, zero_division=0)),
                     _METRICS_STORED_PRECISION)
    misclf   = int((np.array(y_true) != np.array(y_pred)).sum())
    return {
        "Accuracy": acc,
        "Macro_F1_sklearn": macro_f1,
        "Weighted_F1": wt_f1,
        "Misclassified": misclf,
    }


def _validate_arm_a_metrics_json(
    arm_a_run_dir: Path,
    recomputed: dict[str, Any],
) -> dict[str, Any]:
    """
    If a metrics.json exists in arm_a_run_dir/arm_a/, validate its required
    fields against the recomputed values.  Raise ValueError on any mismatch
    or missing required field.

    If metrics.json is absent, log a warning and return the recomputed values.
    The caller should not substitute zero for absent metrics.

    Returns the recomputed metrics (never the raw saved values, to avoid
    zero-default contamination).
    """
    metrics_path = arm_a_run_dir / "arm_a" / "metrics.json"
    if not metrics_path.exists():
        logger.warning(
            "Arm A metrics.json not found at '%s'. "
            "Using recomputed OOF metrics for comparison.",
            metrics_path,
        )
        return recomputed

    with open(metrics_path, encoding="utf-8") as f:
        saved = json.load(f)

    errors: list[str] = []
    for field in _ARM_A_REQUIRED_METRICS_FIELDS:
        if field not in saved:
            errors.append(f"Required field '{field}' missing from metrics.json.")
            continue
        saved_val = saved[field]
        recomp_val = recomputed[field]
        # Compare at stored precision
        if isinstance(recomp_val, float):
            if round(float(saved_val), _METRICS_STORED_PRECISION) != round(
                recomp_val, _METRICS_STORED_PRECISION
            ):
                errors.append(
                    f"metrics.json {field}={saved_val!r} disagrees with "
                    f"recomputed value {recomp_val!r} at {_METRICS_STORED_PRECISION}-dp precision."
                )
        elif saved_val != recomp_val:
            errors.append(
                f"metrics.json {field}={saved_val!r} disagrees with "
                f"recomputed value {recomp_val!r}."
            )

    if errors:
        raise ValueError(
            f"Arm A metrics.json validation failed ({len(errors)} issue(s)):\n"
            + "\n".join(f"  - {e}" for e in errors)
        )

    logger.info(
        "Arm A metrics.json validated against recomputed OOF values: PASS."
    )
    return recomputed


# ---------------------------------------------------------------------------
# Pipeline configuration comparison
# ---------------------------------------------------------------------------

# Parameters that must match between Arm A and Arm B runs.
# Derived from build_pipeline_with_cleaner().get_params(deep=True).
# Unstable values (dtype class repr, memory address strings) are excluded.
_PIPELINE_STEPS_KEY = "steps"       # list of step names from run_metadata
_PIPELINE_CLF_PARAMS = {
    "clf__C",
    "clf__class_weight",
    "clf__fit_intercept",
    "clf__max_iter",
    "clf__penalty",
    "clf__random_state",
    "clf__solver",
    "clf__tol",
}
_PIPELINE_TFIDF_PARAMS = {
    "tfidf__analyzer",
    "tfidf__binary",
    "tfidf__lowercase",
    "tfidf__max_df",
    "tfidf__max_features",
    "tfidf__min_df",
    "tfidf__ngram_range",
    "tfidf__norm",
    "tfidf__smooth_idf",
    "tfidf__stop_words",
    "tfidf__sublinear_tf",
}
_PIPELINE_CHECKED_PARAMS = _PIPELINE_CLF_PARAMS | _PIPELINE_TFIDF_PARAMS


def _verify_pipeline_config(
    arm_a_run_dir: Path,
    current_pipeline: Any,  # sklearn Pipeline
) -> None:
    """
    Compare the pipeline configuration recorded in Arm A's run_metadata.json
    against the current build_pipeline_with_cleaner() configuration.

    Raises ValueError if:
    - run_metadata.json is absent or lacks a 'pipeline' section;
    - step names differ;
    - any checked classifier or TF-IDF parameter differs.

    Notes
    -----
    Only stable, meaningful parameters are compared.  Unstable values
    such as class object repr strings (e.g. dtype) are excluded.
    No hardcoded parameter dictionary is introduced; the reference values
    are read from the saved metadata.
    """
    meta_path = arm_a_run_dir / "run_metadata.json"
    if not meta_path.exists():
        raise ValueError(
            f"Arm A run_metadata.json not found at '{meta_path}'. "
            "Cannot verify pipeline configuration without saved metadata."
        )

    with open(meta_path, encoding="utf-8") as f:
        saved_meta = json.load(f)

    pipeline_section = saved_meta.get("pipeline")
    if not pipeline_section:
        raise ValueError(
            f"Arm A run_metadata.json at '{meta_path}' has no 'pipeline' section. "
            "Stored metadata is insufficient for pipeline verification."
        )

    # --- Step names ---
    saved_steps = pipeline_section.get("steps", [])
    current_steps = [name for name, _ in current_pipeline.steps]
    if saved_steps != current_steps:
        raise ValueError(
            f"Pipeline step names differ.  Arm A saved: {saved_steps}.  "
            f"Current: {current_steps}.  Cannot compare arms with different pipelines."
        )

    # --- Parameter comparison ---
    saved_params = pipeline_section.get("params_from_get_params", {})
    if not saved_params:
        raise ValueError(
            f"Arm A run_metadata.json at '{meta_path}' has no "
            "'pipeline.params_from_get_params' section."
        )

    current_params = current_pipeline.get_params(deep=True)

    errors: list[str] = []
    for key in sorted(_PIPELINE_CHECKED_PARAMS):
        if key not in saved_params:
            errors.append(
                f"Parameter '{key}' not found in Arm A saved metadata "
                f"(params_from_get_params)."
            )
            continue
        if key not in current_params:
            errors.append(
                f"Parameter '{key}' not found in current pipeline."
            )
            continue

        saved_val = saved_params[key]
        current_val = current_params[key]

        # Normalise for comparison: tuples become lists in JSON
        if isinstance(current_val, tuple):
            current_val_cmp: Any = list(current_val)
        else:
            current_val_cmp = current_val

        if saved_val != current_val_cmp:
            errors.append(
                f"Parameter '{key}': Arm A saved {saved_val!r}, "
                f"current pipeline has {current_val_cmp!r}."
            )

    if errors:
        raise ValueError(
            f"Pipeline configuration mismatch between Arm A and current build_pipeline_with_cleaner() "
            f"({len(errors)} issue(s)).  Fitting Arm B would produce an incomparable result:\n"
            + "\n".join(f"  - {e}" for e in errors)
        )

    logger.info(
        "Pipeline configuration verification PASS: "
        "steps and %d checked parameters match Arm A saved metadata.",
        len(_PIPELINE_CHECKED_PARAMS),
    )



# ---------------------------------------------------------------------------
# Arm B manifest saver
# ---------------------------------------------------------------------------

def save_arm_b_fold_manifest(
    manifest_rows: list[dict[str, Any]],
    output_path: Path,
) -> None:
    """Save the per-fold augmentation manifest (all families, including excluded)."""
    pd.DataFrame(manifest_rows).to_csv(output_path, index=False)
    logger.info("Arm B fold manifest saved to '%s'", output_path)


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


def run_experiment4_arm_b(
    review_file: Path,
    arm_a_run_dir: Path | None = None,
    output_dir: Path | None = None,
    dry_run: bool = False,
) -> dict[str, Any]:
    """
    Execute Experiment 4 Arm B (synthetic augmentation comparison).

    Parameters
    ----------
    review_file : Path
        Path to the completed synthetic-family review pack CSV.
        The file is read exactly once; the same byte sequence is used for
        SHA-256 hashing, CSV parsing, validation, and the review snapshot.
    arm_a_run_dir : Path
        Explicit Arm A run directory to use as the comparison reference.
        Required for any non-dry-run with eligible families.  Automatic
        fallback to another run directory is not permitted; the caller must
        supply this path to ensure a reproducible, auditable comparison.
    output_dir : Path | None
        Explicit output directory (must be empty or not yet created).
        If None, automatic run-directory numbering is used.
    dry_run : bool
        If True, validate inputs and produce the manifest but do not fit
        any model or produce metrics.  arm_a_run_dir is not required for
        dry-run or zero-eligible-families paths.

    Raises
    ------
    ValueError
        If arm_a_run_dir is None when fitting would be attempted.
        If pipeline configuration in arm_a_run_dir/run_metadata.json differs
        from the current build_pipeline_with_cleaner() configuration.
        If the saved Arm A metrics.json disagrees with OOF-recomputed values.
        If compatibility with the specified Arm A run cannot be established.

    Notes
    -----
    Researcher review is not independent domain-expert governance validation.
    Results are development cross-validation on 200 baseline records.
    Not untouched held-out test results.
    """
    import sklearn

    label = "DRY-RUN" if dry_run else "Arm B"
    logger.info("=== Component 4 GovernanceIntelligence — Experiment 4 %s ===", label)

    # --- Resolve output directory ---
    if output_dir is None:
        output_dir = resolve_exp4_output_dir(config.RESULTS_DIR)
    else:
        output_dir = Path(output_dir)
        if _dir_is_occupied(output_dir):
            raise ValueError(
                f"Explicit output directory is not empty: {output_dir}. "
                "Supply an empty or non-existent directory, or omit output_dir "
                "to use automatic run numbering."
            )
        output_dir.mkdir(parents=True, exist_ok=True)

    arm_b_dir = output_dir / "arm_b"

    # --- Load and validate baseline ---
    df = load_and_filter_baseline(CANDIDATE_CSV_PATH)
    n_baseline = df[ID_COLUMN].nunique()
    if n_baseline != 200:
        raise ValueError(
            f"Expected exactly 200 unique baseline Research_IDs; got {n_baseline}."
        )
    candidate_sha256 = sha256_file(CANDIDATE_CSV_PATH)

    orig_path = config.DATASET_PATH
    baseline_check = verify_baseline_vs_original(df, orig_path)
    orig_sha256 = sha256_file(orig_path) if orig_path.exists() else "unavailable"

    _require_file(EXP2_OOF_PATH, "Experiment 2 OOF (frozen fold reference)")
    exp2_oof = pd.read_csv(EXP2_OOF_PATH)
    exp2_sha256 = sha256_file(EXP2_OOF_PATH)
    fold_map = validate_fold_reference(df, exp2_oof)

    cg_map = dict(zip(exp2_oof[ID_COLUMN], exp2_oof["Combined_Group"]))
    df = df.copy()
    df["Combined_Group"] = df[ID_COLUMN].map(cg_map)
    n_null_cg = df["Combined_Group"].isna().sum()
    if n_null_cg > 0:
        raise ValueError(f"{n_null_cg} baseline records have null Combined_Group after join.")

    baseline_ids_set = set(df[ID_COLUMN].tolist())

    # --- Load full candidate dataset (needed for synthetic texts) ---
    candidate_df = pd.read_csv(CANDIDATE_CSV_PATH)

    # --- Load and validate reviews (single read — same bytes for hash, parse, snapshot) ---
    review_file = Path(review_file)
    if not review_file.exists():
        raise FileNotFoundError(f"Review pack not found: {review_file}")
    review_raw_bytes = review_file.read_bytes()
    reviews, review_sha256 = load_and_validate_reviews_from_bytes(
        review_raw_bytes, candidate_df, source_description=str(review_file)
    )
    validate_related_baseline_ids(reviews, baseline_ids_set)

    n_eligible = sum(1 for r in reviews if r.eligible)
    n_total = len(reviews)

    # --- Build per-fold manifest ---
    manifest_rows = build_fold_manifest(reviews, df, fold_map, cg_map, id_column=ID_COLUMN)

    # Count per-fold eligible summaries
    fold_eligible_counts: dict[int, dict[str, int]] = {}
    for row in manifest_rows:
        if isinstance(row["Fold"], int) and row["Included"]:
            f = int(row["Fold"])
            fold_eligible_counts.setdefault(f, {"families": 0, "records": 0})
            fold_eligible_counts[f]["families"] += 1
            fold_eligible_counts[f]["records"] += 3  # always 3 variants

    zero_augmentation_folds = [
        f for f in sorted(set(fold_map.values()))
        if fold_eligible_counts.get(f, {}).get("families", 0) == 0
    ]

    logger.info(
        "Review summary: %d eligible families (out of %d total). "
        "Folds with zero eligible families: %s",
        n_eligible, n_total,
        zero_augmentation_folds if zero_augmentation_folds else "none",
    )

    # Save review snapshot — write the exact captured bytes, do not re-open the file
    review_snapshot_path = output_dir / "review_snapshot.csv"
    review_snapshot_path.write_bytes(review_raw_bytes)
    logger.info("Review snapshot saved to '%s'", review_snapshot_path)

    # Save manifest
    manifest_path = output_dir / "fold_manifest.csv"
    save_arm_b_fold_manifest(manifest_rows, manifest_path)

    # --- Dry-run exit point ---
    if dry_run or n_eligible == 0:
        dry_run_status = "DRY_RUN" if dry_run else "NO_ELIGIBLE_FAMILIES"
        if not dry_run and n_eligible == 0:
            logger.warning(
                "Zero eligible synthetic families. "
                "Actual Arm B training requires at least one approved family. "
                "Delivering dry-run artifacts only."
            )

        # Compute label distribution of eligible families
        label_dist: dict[str, int] = {}
        for rev in reviews:
            if rev.eligible and rev.effective_label:
                label_dist[rev.effective_label] = label_dist.get(rev.effective_label, 0) + 3

        dry_run_result: dict[str, Any] = {
            "status": dry_run_status,
            "output_dir": str(output_dir),
            "review_file": str(review_file),
            "review_sha256": review_sha256,
            "candidate_sha256": candidate_sha256,
            "total_families_in_pack": n_total,
            "eligible_families": n_eligible,
            "eligible_records": n_eligible * 3,
            "zero_augmentation_folds": zero_augmentation_folds,
            "eligible_label_distribution": label_dist,
            "fold_eligible_counts": {str(k): v for k, v in fold_eligible_counts.items()},
            "note": (
                "Dry-run: input validation and manifest generation only. "
                "No model was fitted. No metrics were produced. "
                if dry_run else
                "Zero eligible families: no augmentation possible. "
                "Manifest generated. No model was fitted."
            ),
            "caution": (
                "Researcher review is not independent domain-expert governance validation. "
                "Development cross-validation results only. Not held-out test results."
            ),
        }
        dry_run_meta_path = output_dir / "run_metadata.json"
        with open(dry_run_meta_path, "w", encoding="utf-8") as f:
            json.dump(dry_run_result, f, indent=2)
        logger.info("Dry-run/no-augmentation metadata saved to '%s'", dry_run_meta_path)
        return dry_run_result

    # --- Arm A compatibility check ---
    # Require an explicit arm_a_run_dir — automatic fallback is not permitted.
    if arm_a_run_dir is None:
        raise ValueError(
            "arm_a_run_dir must be supplied explicitly for a non-dry-run with eligible families. "
            "Automatic fallback to another run directory is not permitted to ensure "
            "an auditable, reproducible comparison."
        )
    arm_a_run_dir = Path(arm_a_run_dir)
    if not arm_a_run_dir.exists():
        raise ValueError(
            f"Arm A run directory does not exist: {arm_a_run_dir}"
        )
    arm_a_oof_path = arm_a_run_dir / "arm_a" / "oof_predictions.csv"
    _require_file(arm_a_oof_path, f"Arm A OOF from {arm_a_run_dir}")
    arm_a_sha256 = sha256_file(arm_a_oof_path)

    arm_a_oof_loaded = pd.read_csv(arm_a_oof_path)
    # Verify ID sets, labels, folds against current baseline
    arm_a_ids = set(arm_a_oof_loaded["Research_ID"].tolist())
    if arm_a_ids != baseline_ids_set:
        raise ValueError(
            f"Arm A OOF Research_ID set does not match current baseline IDs. "
            f"Only in Arm A: {sorted(arm_a_ids - baseline_ids_set)[:5]}. "
            f"Only in baseline: {sorted(baseline_ids_set - arm_a_ids)[:5]}."
        )
    # Label agreement
    base_label_map = df.set_index(ID_COLUMN)[TARGET_COLUMN].to_dict()
    arm_a_label_map = arm_a_oof_loaded.set_index("Research_ID")["True_Label"].to_dict()
    label_mm = [rid for rid in baseline_ids_set if base_label_map.get(rid) != arm_a_label_map.get(rid)]
    if label_mm:
        raise ValueError(
            f"Arm A OOF true labels differ from current baseline for {len(label_mm)} records."
        )
    # Fold agreement
    arm_a_fold_map = arm_a_oof_loaded.set_index("Research_ID")["Fold"].to_dict()
    fold_mm = [rid for rid in baseline_ids_set if arm_a_fold_map.get(rid) != fold_map.get(rid)]
    if fold_mm:
        raise ValueError(
            f"Arm A OOF fold assignments differ from frozen Exp2 folds for "
            f"{len(fold_mm)} records."
        )

    # --- Recompute Arm A metrics from OOF before fitting Arm B ---
    arm_a_metrics_recomputed = _recompute_arm_a_metrics(arm_a_oof_loaded)
    # Validate against saved metrics.json (if present); raises on disagreement.
    arm_a_metrics_validated = _validate_arm_a_metrics_json(arm_a_run_dir, arm_a_metrics_recomputed)

    # --- Verify pipeline configuration matches Arm A ---
    _current_pipeline_ref = build_pipeline_with_cleaner()
    _verify_pipeline_config(arm_a_run_dir, _current_pipeline_ref)

    # Re-verify Arm A reproduction using corrected checker
    exp3b_sha256 = sha256_file(EXP3B_OOF_PATH)
    repro_recheck = run_reproduction_check(
        arm_a_oof=arm_a_oof_loaded,
        repro_path=EXP3B_OOF_PATH,
        output_path=output_dir / "arm_a_repro_recheck.json",
    )
    if repro_recheck["reproduction_status"] != "PASS":
        raise ValueError(
            f"Arm A reproduction re-check did not PASS (status: "
            f"{repro_recheck['reproduction_status']}). "
            "Comparison cannot proceed with an incompatible Arm A reference."
        )
    logger.info(
        "Arm A compatibility verified: IDs, labels, folds agree; "
        "pipeline config verified; metrics recomputed and validated; "
        "reproduction re-check: %s",
        repro_recheck["reproduction_status"],
    )

    # --- Run Arm B ---
    arm_b_results = run_arm_b(
        arm_dir=arm_b_dir,
        baseline_df=df,
        fold_map=fold_map,
        cg_map=cg_map,
        reviews=reviews,
        candidate_df=candidate_df,
    )

    # --- Paired comparison ---
    paired_summary, transitions_df = build_paired_comparison(
        arm_a_oof=arm_a_oof_loaded,
        arm_b_oof=arm_b_results["oof_df"],
    )
    transitions_df.to_csv(output_dir / "paired_prediction_changes.csv", index=False)

    # Arm A metrics: recomputed from OOF (validated above); never substitute zero
    arm_b_m = arm_b_results["overall_metrics"]
    arm_a_m = arm_a_metrics_validated  # always the recomputed values
    comparison: dict[str, Any] = {
        "arm_a_run_dir": str(arm_a_run_dir),
        "arm_b_run_dir": str(output_dir),
        "arm_a_metrics": arm_a_m,
        "arm_a_metrics_source": "recomputed from Arm A OOF True_Label/Predicted_Label",
        "arm_b_metrics": arm_b_m,
        "deltas_b_minus_a": {
            "Accuracy": round(
                arm_b_m["Accuracy"] - arm_a_m["Accuracy"], _METRICS_STORED_PRECISION
            ),
            "Macro_F1_sklearn": round(
                arm_b_m["Macro_F1_sklearn"] - arm_a_m["Macro_F1_sklearn"],
                _METRICS_STORED_PRECISION,
            ),
            "Weighted_F1": round(
                arm_b_m["Weighted_F1"] - arm_a_m["Weighted_F1"],
                _METRICS_STORED_PRECISION,
            ),
            "Misclassified": arm_b_m["Misclassified"] - arm_a_m["Misclassified"],
        },
        "paired_comparison": paired_summary,
        "augmentation_summary": arm_b_results["fold_augmentation_summary"],
        "interpretation_caution": (
            "These are development cross-validation results on 200 baseline records. "
            "Frozen folds are replayed. Not held-out test results. "
            "Researcher review is not independent domain-expert governance validation. "
            "Deltas reflect OOF prediction changes, not generalisation to new data."
        ),
    }
    with open(output_dir / "experiment4_comparison.json", "w", encoding="utf-8") as f:
        json.dump(comparison, f, indent=2)

    # --- Pipeline parameters ---
    _ref_pipeline = build_pipeline_with_cleaner()

    def _safe_json(val: Any) -> Any:
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
        return repr(val)

    raw_params = _ref_pipeline.get_params(deep=True)
    pipeline_params_json = {k: _safe_json(v) for k, v in sorted(raw_params.items())}

    # Eligibility label distribution
    eligible_label_dist: dict[str, int] = {}
    for rev in reviews:
        if rev.eligible and rev.effective_label:
            eligible_label_dist[rev.effective_label] = (
                eligible_label_dist.get(rev.effective_label, 0) + 3
            )

    run_metadata: dict[str, Any] = {
        "experiment": "Experiment 4 Arm B",
        "description": (
            "Cleaned-text TF-IDF + LR on 200 baseline records with reviewed synthetic "
            "augmentation in training only. Frozen Experiment 2/3 fold assignments replayed. "
            "Evaluation on identical 200 baseline OOF records as Arm A."
        ),
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "important_caveat": (
            "Development cross-validation results on 200 baseline records. "
            "Frozen folds are replayed, not freshly generated. "
            "Not untouched held-out test results. "
            "Researcher review is not independent professional governance validation."
        ),
        "input_files": {
            "candidate_dataset": {
                "path": str(CANDIDATE_CSV_PATH),
                "sha256": candidate_sha256,
                "baseline_rows_selected": 200,
                "origin_filter": BASELINE_ORIGIN,
            },
            "original_dataset": {"path": str(orig_path), "sha256": orig_sha256},
            "frozen_fold_reference": {
                "path": str(EXP2_OOF_PATH),
                "sha256": exp2_sha256,
            },
            "arm_a_oof_reference": {
                "path": str(arm_a_oof_path),
                "sha256": arm_a_sha256,
            },
            "review_file": {
                "path": str(review_file),
                "sha256": review_sha256,
            },
        },
        "review_summary": {
            "total_families_in_pack": n_total,
            "eligible_families": n_eligible,
            "eligible_records": n_eligible * 3,
            "eligible_label_distribution": eligible_label_dist,
        },
        "augmentation_note": (
            "class_weight='balanced' is retained from the existing pipeline. "
            "Adding synthetic records changes the automatically computed per-class weights "
            "even though the configuration string is unchanged."
        ),
        "baseline_vs_original": baseline_check,
        "pipeline": {
            "steps": [s for s, _ in _ref_pipeline.steps],
            "factory": "boilerplate_transformer.build_pipeline_with_cleaner()",
            "params_from_get_params": pipeline_params_json,
        },
        "fold_replay": {
            "source": str(EXP2_OOF_PATH),
            "description": "Frozen Experiment 2/3 Combined_Group fold assignments replayed.",
            "unique_folds": [1, 2, 3, 4, 5],
            "fold_counts": {
                str(f): int(sum(1 for v in fold_map.values() if v == f))
                for f in range(1, 6)
            },
        },
        "arm_a_compatibility": {
            "run_dir": str(arm_a_run_dir),
            "reproduction_recheck": repro_recheck["reproduction_status"],
            "label_mismatches": 0,
            "fold_mismatches": 0,
        },
        "environment": {
            "python_version": sys.version,
            "sklearn_version": sklearn.__version__,
            "pandas_version": pd.__version__,
            "numpy_version": np.__version__,
            "git": get_git_info(),
        },
        "output_directory": str(output_dir),
        "comparison_summary": comparison["deltas_b_minus_a"],
        "paired_comparison": paired_summary,
        "arm_b_metrics": arm_b_m,
    }

    with open(output_dir / "run_metadata.json", "w", encoding="utf-8") as f:
        json.dump(run_metadata, f, indent=2)

    logger.info("Experiment 4 Arm B complete. Artifacts written to '%s'", output_dir)
    return {
        "output_dir": output_dir,
        "arm_b_results": arm_b_results,
        "comparison": comparison,
        "paired_summary": paired_summary,
        "run_metadata": run_metadata,
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser(
        description="Experiment 4: Synthetic Augmentation Comparison"
    )
    parser.add_argument(
        "--arm",
        choices=["a", "b"],
        default="a",
        help="Which arm to run: 'a' (baseline, no augmentation) or 'b' (synthetic augmentation).",
    )
    parser.add_argument(
        "--review-file",
        type=Path,
        default=None,
        help=(
            "Path to the completed review pack CSV. Required for --arm b. "
            "If omitted with --arm b, uses the canonical candidate-data path."
        ),
    )
    parser.add_argument(
        "--arm-a-run-dir",
        type=Path,
        default=None,
        help=(
            "Explicit Arm A run directory for paired comparison (--arm b only). "
            "If omitted, the default verified run directory is used."
        ),
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        default=False,
        help=(
            "Validate inputs and produce the manifest only; do not fit models "
            "or produce metrics. Only applies to --arm b."
        ),
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

    elif args.arm == "b":
        # Resolve review file
        review_file = args.review_file
        if review_file is None:
            review_file = (
                config.ML_ROOT / "candidate-data" / "v1" / "synthetic_family_review_pack.csv"
            )
        review_file = Path(review_file)

        result_b = run_experiment4_arm_b(
            review_file=review_file,
            arm_a_run_dir=args.arm_a_run_dir,
            dry_run=args.dry_run,
        )

        print("\n" + "=" * 70)
        if args.dry_run or result_b.get("status") in ("DRY_RUN", "NO_ELIGIBLE_FAMILIES"):
            status = result_b.get("status", "DRY_RUN")
            print(f"EXPERIMENT 4 ARM B — {status}")
            print("=" * 70)
            print(f"  Output directory      : {result_b.get('output_dir', 'N/A')}")
            print(f"  Eligible families     : {result_b.get('eligible_families', 0)}")
            print(f"  Eligible records      : {result_b.get('eligible_records', 0)}")
            folds_zero = result_b.get("zero_augmentation_folds", [])
            if folds_zero:
                print(f"  Folds with 0 eligible : {folds_zero}")
            label_dist = result_b.get("eligible_label_distribution", {})
            if label_dist:
                print("  Eligible label distribution:")
                for lbl, cnt in sorted(label_dist.items()):
                    print(f"    {lbl}: {cnt} training record(s)")
            print()
            if result_b.get("eligible_families", 0) == 0:
                print("  Arm B implemented; actual training awaiting human review.")
            print(f"  Note: {result_b.get('note', '')}")
        else:
            cmp = result_b.get("comparison", {})
            deltas = cmp.get("deltas_b_minus_a", {})
            paired = result_b.get("paired_summary", {})
            print("EXPERIMENT 4 ARM B — COMPLETE")
            print("=" * 70)
            print(f"  Output directory      : {result_b.get('output_dir', 'N/A')}")
            arm_b_m = cmp.get("arm_b_metrics", {})
            print(f"  [Arm B] Accuracy      : {arm_b_m.get('Accuracy', 'N/A')}")
            print(f"  [Arm B] Macro-F1      : {arm_b_m.get('Macro_F1_sklearn', 'N/A')}")
            print(f"  [Arm B] Weighted-F1   : {arm_b_m.get('Weighted_F1', 'N/A')}")
            print(f"  [Arm B] Misclassified : {arm_b_m.get('Misclassified', 'N/A')} / 200")
            print()
            print(f"  Delta Accuracy        : {deltas.get('Accuracy', 'N/A')}")
            print(f"  Delta Macro-F1        : {deltas.get('Macro_F1_sklearn', 'N/A')}")
            print()
            print(f"  Helped (A wrong -> B correct)  : {paired.get('helped', 'N/A')}")
            print(f"  Hurt   (A correct -> B wrong)  : {paired.get('hurt', 'N/A')}")
            print(f"  Net gain                       : {paired.get('net_gain', 'N/A')}")
            print(f"  Both correct                   : {paired.get('both_correct', 'N/A')}")
            print(f"  Both wrong                     : {paired.get('both_wrong_any', 'N/A')}")
            print(f"  Label changes (any)            : {paired.get('label_changes', 'N/A')}")
        print()
        print("  CAUTION: Development CV results only. Frozen folds replayed.")
        print("           Not untouched held-out test results.")
        print("=" * 70)

