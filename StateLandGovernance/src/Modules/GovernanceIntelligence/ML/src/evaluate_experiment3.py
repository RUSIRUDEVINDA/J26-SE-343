"""
evaluate_experiment3.py
-----------------------
EXPERIMENT 3 — Conservative Boilerplate Removal Preprocessing Comparison.

Purpose
-------
Measures whether removing the three exact research boilerplate phrases from
the 19 affected records improves overall classification accuracy and macro-F1,
using the same fold assignments as Experiment 2.

This is a controlled two-arm comparison:
  Arm A (Baseline Rerun)  — TF-IDF + LR, raw text, Experiment 2 folds
  Arm B (Cleaned Text)    — BoilerplateStripper + TF-IDF + LR, cleaned text,
                            same Experiment 2 folds

Both arms use:
  - The identical Experiment 2 fold assignments (from saved out_of_fold_predictions.csv)
  - The same model settings (frozen TF-IDF + LR config from config.py)
  - StratifiedGroupKFold grouping enforced via saved Combined_Group values

Preserves
---------
  - Original dataset and labels
  - Experiment 2 artifacts
  - Baseline (Experiment 1) artifacts
  - Deployable saved model

Outputs
-------
results/experiment3_boilerplate_removal/
  ├── exp3a_baseline_rerun/
  │   ├── oof_predictions.csv
  │   ├── per_class_metrics.csv
  │   ├── confusion_matrix.csv
  │   └── fold_metrics.csv
  ├── exp3b_cleaned_text/
  │   ├── oof_predictions.csv
  │   ├── per_class_metrics.csv
  │   ├── confusion_matrix.csv
  │   └── fold_metrics.csv
  ├── experiment3_comparison.json
  └── preprocessing_audit.csv

Interpretive limitations
------------------------
These are development cross-validation results, not untouched held-out test results.
The boilerplate removal does not correct label ambiguity or establish incident identity.
Any improvement should be interpreted as a provisional signal, not a final conclusion.
"""

from __future__ import annotations

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

# Ensure ML/src is on path
_SRC_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(_SRC_DIR))

import config
from config import (
    CV_N_SPLITS,
    DATASET_PATH,
    ID_COLUMN,
    LABEL_ORDER,
    LR_CONFIG,
    RANDOM_STATE,
    TARGET_COLUMN,
    TEXT_COLUMN,
    TFIDF_CONFIG,
)
from data_loader import load_and_validate
from model import build_pipeline
from boilerplate_transformer import (
    BOILERPLATE_PHRASES,
    build_pipeline_with_cleaner,
    strip_research_boilerplate,
)

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s — %(message)s",
)
logger = logging.getLogger("evaluate_experiment3")


# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------

EXP2_OOF_PATH = config.RESULTS_DIR / "experiment2_source_text" / "out_of_fold_predictions.csv"
EXP3_OUTPUT_NAME = "experiment3_boilerplate_removal"


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def get_git_info() -> dict[str, Any]:
    """Retrieve current commit hash and working tree dirty status."""
    try:
        commit = subprocess.check_output(
            ["git", "rev-parse", "HEAD"], stderr=subprocess.DEVNULL, text=True
        ).strip()
        status = subprocess.check_output(
            ["git", "status", "--porcelain"], stderr=subprocess.DEVNULL, text=True
        ).strip()
        return {"commit": commit, "working_tree_clean": len(status) == 0}
    except Exception as e:
        return {"commit": "unknown", "working_tree_clean": False, "error": str(e)}


def _dir_is_occupied(d: Path) -> bool:
    """Return True if directory exists and contains at least one entry."""
    return d.exists() and any(d.iterdir())


def resolve_output_dir(base_results_dir: Path) -> Path:
    """
    Resolve Experiment 3 output directory.

    A directory is considered occupied if it exists and contains any files
    or subdirectories — even if the run did not complete (i.e. no
    experiment3_comparison.json is required).  This preserves partial runs
    from interrupted or failed executions.

    Selection order:
      1. If the canonical base directory (experiment3_boilerplate_removal/) is
         empty or absent, use it.
      2. Otherwise, find the lowest numbered _run<N> directory that is
         empty or absent and use that.
    """
    target = base_results_dir / EXP3_OUTPUT_NAME
    if not _dir_is_occupied(target):
        target.mkdir(parents=True, exist_ok=True)
        return target
    run_num = 1
    while True:
        candidate = base_results_dir / f"{EXP3_OUTPUT_NAME}_run{run_num}"
        if not _dir_is_occupied(candidate):
            candidate.mkdir(parents=True, exist_ok=True)
            return candidate
        run_num += 1


def compute_per_class_metrics(
    y_true: list[str], y_pred: list[str]
) -> dict[str, dict[str, Any]]:
    """Compute Support, TP, FP, FN, TN, Precision, Recall, F1, FPR per class."""
    cm = confusion_matrix(y_true, y_pred, labels=LABEL_ORDER)
    n = cm.sum()
    results: dict[str, dict[str, Any]] = {}

    for i, label in enumerate(LABEL_ORDER):
        tp = int(cm[i, i])
        fp = int(cm[:, i].sum() - tp)
        fn = int(cm[i, :].sum() - tp)
        tn = int(n - tp - fp - fn)
        support = tp + fn

        precision = tp / (tp + fp) if (tp + fp) > 0 else 0.0
        recall = tp / (tp + fn) if (tp + fn) > 0 else 0.0
        f1 = (2 * tp) / (2 * tp + fp + fn) if (2 * tp + fp + fn) > 0 else 0.0
        fpr = fp / (fp + tn) if (fp + tn) > 0 else 0.0

        results[label] = {
            "Support": support,
            "TP": tp,
            "FP": fp,
            "FN": fn,
            "TN": tn,
            "Precision": round(precision, 4),
            "Recall": round(recall, 4),
            "F1": round(f1, 4),
            "FPR": round(fpr, 4),
        }

    return results


def run_cv_arm(
    arm_name: str,
    arm_dir: Path,
    df: pd.DataFrame,
    fold_map: dict[str, int],
    text_column_values: np.ndarray,
    pipeline_factory,
) -> dict[str, Any]:
    """
    Execute a single evaluation arm using frozen Experiment 2 fold assignments.

    Parameters
    ----------
    arm_name : str
        Human-readable label for this arm.
    arm_dir : Path
        Directory to write arm-specific artifacts.
    df : pd.DataFrame
        Dataset with Research_ID, Combined_Group, labels.
    fold_map : dict[str, int]
        Mapping from Research_ID to its Experiment 2 fold index (1-indexed).
    text_column_values : np.ndarray of str
        Text values for each row (may be raw or cleaned, depending on arm).
    pipeline_factory : callable
        Callable returning a fresh, unfitted sklearn Pipeline.

    Returns
    -------
    dict with overall_metrics, per_class_metrics, fold_results, oof_df.
    """
    arm_dir.mkdir(parents=True, exist_ok=True)
    rids = df[ID_COLUMN].tolist()
    y = np.array(df[TARGET_COLUMN].tolist())
    X = text_column_values
    groups = np.array(df["Combined_Group"].tolist())

    # Build fold assignments from frozen fold_map
    unique_folds = sorted(set(fold_map.values()))
    splits: list[tuple[np.ndarray, np.ndarray]] = []
    for fold_idx in unique_folds:
        test_idx = np.array([i for i, rid in enumerate(rids) if fold_map.get(rid) == fold_idx])
        train_idx = np.array([i for i in range(len(rids)) if i not in set(test_idx.tolist())])
        splits.append((train_idx, test_idx))

    # Validate pre-fitting: all records covered, each exactly once
    all_test_indices = []
    for _, test_idx in splits:
        all_test_indices.extend(test_idx.tolist())
    assert len(all_test_indices) == len(df), (
        f"[{arm_name}] Test index count {len(all_test_indices)} != dataset size {len(df)}"
    )
    assert len(set(all_test_indices)) == len(df), (
        f"[{arm_name}] Some records appear in multiple test folds."
    )

    # Validate Combined_Group integrity: no train/test overlap
    for fold_idx, (train_idx, test_idx) in enumerate(splits, start=1):
        trn_cg = set(groups[train_idx])
        tst_cg = set(groups[test_idx])
        cg_overlap = trn_cg.intersection(tst_cg)
        assert len(cg_overlap) == 0, (
            f"[{arm_name}] Fold {fold_idx}: Combined_Group overlap detected: {cg_overlap}"
        )

    # Run CV folds
    oof_records: list[dict[str, Any]] = []
    fold_results: list[dict[str, Any]] = []

    for fold_idx, (train_idx, test_idx) in enumerate(splits, start=1):
        X_train, X_test = X[train_idx], X[test_idx]
        y_train, y_test = y[train_idx], y[test_idx]

        pipeline = pipeline_factory()
        pipeline.fit(X_train, y_train)
        y_pred = pipeline.predict(X_test)
        y_prob = pipeline.predict_proba(X_test)

        clf_classes = pipeline.named_steps["clf"].classes_.tolist()

        f_acc = round(float(accuracy_score(y_test, y_pred)), 4)
        f_macro_f1 = round(float(f1_score(y_test, y_pred, average="macro", labels=LABEL_ORDER, zero_division=0)), 4)
        f_wt_f1 = round(float(f1_score(y_test, y_pred, average="weighted", labels=LABEL_ORDER, zero_division=0)), 4)

        logger.info(
            "[%s] Fold %d/%d — train=%d, test=%d — Acc=%.4f, Macro-F1=%.4f",
            arm_name, fold_idx, len(unique_folds), len(train_idx), len(test_idx),
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
            **{f"Test_{lbl.replace('/', '_').replace(' ', '_')}": cnt
               for lbl, cnt in test_class_counts.items()},
        })

        for i, row_idx in enumerate(test_idx):
            proba_dict = {
                f"prob_{cls.replace('/', '_').replace(' ', '_')}": round(float(y_prob[i, j]), 4)
                for j, cls in enumerate(clf_classes)
            }
            oof_records.append({
                "Research_ID": rids[row_idx],
                "Combined_Group": groups[row_idx],
                "True_Label": y[row_idx],
                "Predicted_Label": y_pred[i],
                "Fold": fold_idx,
                **proba_dict,
            })

    # Assemble OOF dataframe in original dataset order
    oof_df = pd.DataFrame(oof_records)
    id_order = {rid: idx for idx, rid in enumerate(rids)}
    oof_df["_sort_key"] = oof_df["Research_ID"].map(id_order)
    oof_df = oof_df.sort_values("_sort_key").drop(columns=["_sort_key"])

    y_true_oof = oof_df["True_Label"].tolist()
    y_pred_oof = oof_df["Predicted_Label"].tolist()

    overall_acc = round(float(accuracy_score(y_true_oof, y_pred_oof)), 4)
    overall_macro_f1 = round(float(f1_score(y_true_oof, y_pred_oof, average="macro", labels=LABEL_ORDER, zero_division=0)), 4)
    overall_wt_f1 = round(float(f1_score(y_true_oof, y_pred_oof, average="weighted", labels=LABEL_ORDER, zero_division=0)), 4)
    overall_macro_p = round(float(precision_score(y_true_oof, y_pred_oof, average="macro", labels=LABEL_ORDER, zero_division=0)), 4)
    overall_macro_r = round(float(recall_score(y_true_oof, y_pred_oof, average="macro", labels=LABEL_ORDER, zero_division=0)), 4)
    misclassified = int((np.array(y_true_oof) != np.array(y_pred_oof)).sum())

    per_class = compute_per_class_metrics(y_true_oof, y_pred_oof)
    cm = confusion_matrix(y_true_oof, y_pred_oof, labels=LABEL_ORDER)

    logger.info(
        "[%s] OOF Pooled — Acc=%.4f, Macro-F1=%.4f, Weighted-F1=%.4f, Errors=%d/200",
        arm_name, overall_acc, overall_macro_f1, overall_wt_f1, misclassified,
    )

    # Write arm artifacts
    oof_df.to_csv(arm_dir / "oof_predictions.csv", index=False)
    per_class_rows = [{"Label": lbl, **m} for lbl, m in per_class.items()]
    pd.DataFrame(per_class_rows).to_csv(arm_dir / "per_class_metrics.csv", index=False)
    cm_df = pd.DataFrame(cm, index=LABEL_ORDER, columns=LABEL_ORDER)
    cm_df.to_csv(arm_dir / "confusion_matrix.csv")
    pd.DataFrame(fold_results).to_csv(arm_dir / "fold_metrics.csv", index=False)

    return {
        "overall_metrics": {
            "Accuracy": overall_acc,
            "Macro_Precision": overall_macro_p,
            "Macro_Recall": overall_macro_r,
            "Macro_F1": overall_macro_f1,
            "Weighted_F1": overall_wt_f1,
            "Misclassified": misclassified,
        },
        "per_class_metrics": per_class,
        "fold_results": fold_results,
        "confusion_matrix": cm.tolist(),
        "oof_df": oof_df,
    }


# ---------------------------------------------------------------------------
# Preprocessing Audit
# ---------------------------------------------------------------------------

def build_preprocessing_audit(df: pd.DataFrame) -> pd.DataFrame:
    """
    Construct a record-level preprocessing audit CSV showing:
    - Which records were changed by boilerplate removal
    - Original text, cleaned text
    - Boilerplate phrases found (which were present)
    - Arm A and Arm B predictions will be joined after evaluation
    """
    rows = []
    for _, row in df.iterrows():
        original = str(row[TEXT_COLUMN])
        cleaned = strip_research_boilerplate(original)
        was_changed = original != cleaned
        phrases_found = [p for p in BOILERPLATE_PHRASES if p in original]
        rows.append({
            "Research_ID": row[ID_COLUMN],
            "True_Label": row[TARGET_COLUMN],
            "Was_Changed_By_Cleaning": was_changed,
            "Phrases_Found_Count": len(phrases_found),
            "Original_Text": original,
            "Cleaned_Text": cleaned,
        })
    return pd.DataFrame(rows)


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def run_experiment3(output_dir: Path | None = None) -> dict[str, Any]:
    """Execute Experiment 3 two-arm boilerplate removal comparison."""
    logger.info("=== Component 4 GovernanceIntelligence — Experiment 3 Two-Arm Comparison ===")

    # 1. Load dataset
    df = load_and_validate()
    logger.info("Loaded validated dataset: %d rows.", len(df))
    dataset_sha256 = hashlib.sha256(DATASET_PATH.read_bytes()).hexdigest()

    # 2. Load Experiment 2 fold assignments
    if not EXP2_OOF_PATH.exists():
        raise FileNotFoundError(
            f"Experiment 2 OOF file not found: {EXP2_OOF_PATH}. "
            "Run evaluate_experiment2.py first."
        )
    exp2_oof = pd.read_csv(EXP2_OOF_PATH)
    logger.info("Loaded Experiment 2 OOF file: %d records.", len(exp2_oof))

    # Validate fold map completeness
    fold_map: dict[str, int] = dict(zip(exp2_oof[ID_COLUMN], exp2_oof["Fold"]))
    rids_in_dataset = set(df[ID_COLUMN].tolist())
    rids_in_foldmap = set(fold_map.keys())

    missing_from_foldmap = rids_in_dataset - rids_in_foldmap
    extra_in_foldmap = rids_in_foldmap - rids_in_dataset

    if missing_from_foldmap:
        raise ValueError(
            f"{len(missing_from_foldmap)} dataset records not found in Experiment 2 fold map: "
            f"{sorted(missing_from_foldmap)}"
        )
    if extra_in_foldmap:
        logger.warning(
            "%d records in Experiment 2 fold map not found in current dataset (ignored): %s",
            len(extra_in_foldmap), sorted(extra_in_foldmap),
        )

    # Join Combined_Group from Experiment 2 manifest
    df = df.merge(
        exp2_oof[[ID_COLUMN, "Combined_Group"]].drop_duplicates(subset=[ID_COLUMN]),
        on=ID_COLUMN,
        how="left",
    )
    n_missing_cg = df["Combined_Group"].isna().sum()
    if n_missing_cg > 0:
        raise ValueError(f"{n_missing_cg} records are missing Combined_Group after merge.")

    logger.info("Fold assignments loaded. Unique folds: %s", sorted(set(fold_map.values())))

    # 3. Resolve output directory
    if output_dir is None:
        output_dir = resolve_output_dir(config.RESULTS_DIR)
    output_dir.mkdir(parents=True, exist_ok=True)

    arm_a_dir = output_dir / "exp3a_baseline_rerun"
    arm_b_dir = output_dir / "exp3b_cleaned_text"

    # 4. Prepare text arrays
    X_raw = np.array(df[TEXT_COLUMN].tolist())
    X_cleaned = np.array([strip_research_boilerplate(t) for t in df[TEXT_COLUMN].tolist()])

    n_changed = int((X_raw != X_cleaned).sum())
    logger.info("Records changed by boilerplate removal: %d / %d", n_changed, len(df))

    # 5. Run Arm A — Baseline Rerun (raw text, Experiment 2 folds)
    logger.info("--- Running Arm A: Baseline Rerun (raw text) ---")
    arm_a_results = run_cv_arm(
        arm_name="Arm A Baseline Rerun",
        arm_dir=arm_a_dir,
        df=df,
        fold_map=fold_map,
        text_column_values=X_raw,
        pipeline_factory=build_pipeline,
    )

    # 6. Run Arm B — Cleaned Text
    logger.info("--- Running Arm B: Cleaned Text ---")
    arm_b_results = run_cv_arm(
        arm_name="Arm B Cleaned Text",
        arm_dir=arm_b_dir,
        df=df,
        fold_map=fold_map,
        text_column_values=X_cleaned,
        pipeline_factory=build_pipeline_with_cleaner,
    )

    # 7. Build and save preprocessing audit CSV
    audit_df = build_preprocessing_audit(df)
    # Join Arm A and Arm B predictions
    arm_a_oof = arm_a_results["oof_df"].rename(
        columns={"Predicted_Label": "Arm_A_Predicted", "Fold": "Fold"}
    )[["Research_ID", "Arm_A_Predicted"]]
    arm_b_oof = arm_b_results["oof_df"].rename(
        columns={"Predicted_Label": "Arm_B_Predicted"}
    )[["Research_ID", "Arm_B_Predicted"]]

    audit_df = audit_df.merge(arm_a_oof, on="Research_ID", how="left")
    audit_df = audit_df.merge(arm_b_oof, on="Research_ID", how="left")
    audit_df["Arm_A_Correct"] = audit_df["True_Label"] == audit_df["Arm_A_Predicted"]
    audit_df["Arm_B_Correct"] = audit_df["True_Label"] == audit_df["Arm_B_Predicted"]
    audit_df["Cleaning_Helped"] = (~audit_df["Arm_A_Correct"]) & audit_df["Arm_B_Correct"]
    audit_df["Cleaning_Hurt"] = audit_df["Arm_A_Correct"] & (~audit_df["Arm_B_Correct"])
    audit_df.to_csv(output_dir / "preprocessing_audit.csv", index=False)

    # 8. Compute deltas
    a_m = arm_a_results["overall_metrics"]
    b_m = arm_b_results["overall_metrics"]
    delta = {
        "Accuracy": round(b_m["Accuracy"] - a_m["Accuracy"], 4),
        "Macro_F1": round(b_m["Macro_F1"] - a_m["Macro_F1"], 4),
        "Weighted_F1": round(b_m["Weighted_F1"] - a_m["Weighted_F1"], 4),
        "Misclassified": b_m["Misclassified"] - a_m["Misclassified"],
    }

    # Compute delta per class
    per_class_delta: dict[str, dict[str, Any]] = {}
    for label in LABEL_ORDER:
        a_pc = arm_a_results["per_class_metrics"][label]
        b_pc = arm_b_results["per_class_metrics"][label]
        per_class_delta[label] = {
            "F1_delta": round(b_pc["F1"] - a_pc["F1"], 4),
            "Precision_delta": round(b_pc["Precision"] - a_pc["Precision"], 4),
            "Recall_delta": round(b_pc["Recall"] - a_pc["Recall"], 4),
            "Arm_A_F1": a_pc["F1"],
            "Arm_B_F1": b_pc["F1"],
        }

    # Changed-records analysis — compute transitions per population
    # Population A: all records
    overall_helped = int(audit_df["Cleaning_Helped"].sum())
    overall_hurt   = int(audit_df["Cleaning_Hurt"].sum())

    # Population B: records whose text was changed by boilerplate removal
    changed_mask = audit_df["Was_Changed_By_Cleaning"]
    changed_records = audit_df[changed_mask]
    changed_arm_a_correct = int(changed_records["Arm_A_Correct"].sum())
    changed_arm_b_correct = int(changed_records["Arm_B_Correct"].sum())
    changed_helped = int(changed_records["Cleaning_Helped"].sum())
    changed_hurt   = int(changed_records["Cleaning_Hurt"].sum())

    # Population C: records whose text was NOT changed
    unchanged_records = audit_df[~changed_mask]
    unchanged_arm_a_correct = int(unchanged_records["Arm_A_Correct"].sum())
    unchanged_arm_b_correct = int(unchanged_records["Arm_B_Correct"].sum())
    unchanged_helped = int(unchanged_records["Cleaning_Helped"].sum())
    unchanged_hurt   = int(unchanged_records["Cleaning_Hurt"].sum())

    # 9. Historical metrics reference
    import sklearn
    hist_exp2 = {
        "source": "results/experiment2_source_text/experiment2_metrics.json",
        "strategy": "StratifiedGroupKFold (Combined_Group)",
        "Accuracy": 0.7250,
        "Macro_F1": 0.7242,
        "Misclassified": 55,
        "note": "Original Experiment 2 run (saved artifact, not re-run here)",
    }
    exp2_metrics_path = config.RESULTS_DIR / "experiment2_source_text" / "experiment2_metrics.json"
    if exp2_metrics_path.exists():
        try:
            with open(exp2_metrics_path, "r", encoding="utf-8") as f:
                exp2_data = json.load(f)
            hist_exp2.update({
                "Accuracy": exp2_data.get("overall_metrics", {}).get("Accuracy", 0.7250),
                "Macro_F1": exp2_data.get("overall_metrics", {}).get("Macro_F1", 0.7242),
                "Misclassified": exp2_data.get("overall_metrics", {}).get("Misclassified", 55),
            })
        except Exception:
            pass

    # 10. Build comparison summary JSON
    summary: dict[str, Any] = {
        "experiment_name": "Experiment 3 — Conservative Boilerplate Removal Preprocessing Comparison",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "important_caveat": (
            "These are development cross-validation results using Experiment 2 fold assignments. "
            "They are not untouched held-out test results. Any improvement is a provisional signal only."
        ),
        "dataset": {
            "path": str(DATASET_PATH),
            "sha256": dataset_sha256,
            "row_count": len(df),
            "classes": LABEL_ORDER,
        },
        "environment": {
            "python_version": sys.version,
            "sklearn_version": sklearn.__version__,
            "pandas_version": pd.__version__,
            "numpy_version": np.__version__,
            "git": get_git_info(),
        },
        "boilerplate_removal": {
            "phrases_removed": list(BOILERPLATE_PHRASES),
            "method": "Exact literal string replacement only. No regex, no broad semantic matching.",
            "records_affected": n_changed,
            "records_unchanged": len(df) - n_changed,
        },
        "fold_reuse": {
            "source": str(EXP2_OOF_PATH),
            "description": "Exact Experiment 2 fold assignments reused. No new grouping or splitting performed.",
            "unique_folds": sorted(set(fold_map.values())),
        },
        "arm_a_baseline_rerun": {
            "description": "Baseline rerun: same TF-IDF + LR pipeline as Experiment 2, raw text, Experiment 2 folds.",
            "pipeline": "TfidfVectorizer → LogisticRegression",
            "overall_metrics": a_m,
            "per_class_metrics": arm_a_results["per_class_metrics"],
        },
        "arm_b_cleaned_text": {
            "description": "Cleaned text: BoilerplateStripper + TF-IDF + LR, cleaned text, same Experiment 2 folds.",
            "pipeline": "BoilerplateStripper → TfidfVectorizer → LogisticRegression",
            "overall_metrics": b_m,
            "per_class_metrics": arm_b_results["per_class_metrics"],
        },
        "delta_b_minus_a": delta,
        "per_class_delta": per_class_delta,
        "all_records_analysis": {
            "total": len(audit_df),
            "arm_a_correct": int(audit_df["Arm_A_Correct"].sum()),
            "arm_b_correct": int(audit_df["Arm_B_Correct"].sum()),
            "cleaning_helped_count": overall_helped,
            "cleaning_hurt_count": overall_hurt,
        },
        "changed_records_analysis": {
            "total_changed": n_changed,
            "arm_a_correct_on_changed": changed_arm_a_correct,
            "arm_b_correct_on_changed": changed_arm_b_correct,
            "cleaning_helped_count": changed_helped,
            "cleaning_hurt_count": changed_hurt,
        },
        "unchanged_records_analysis": {
            "total_unchanged": len(unchanged_records),
            "arm_a_correct_on_unchanged": unchanged_arm_a_correct,
            "arm_b_correct_on_unchanged": unchanged_arm_b_correct,
            "cleaning_helped_count": unchanged_helped,
            "cleaning_hurt_count": unchanged_hurt,
        },
        "historical_reference": {
            "experiment_2_original": hist_exp2,
            "experiment_1_baseline": {
                "source": "Historical record (Experiment 1 results/baseline_metrics.json)",
                "strategy": "StratifiedGroupKFold (Source_Group_ID only)",
                "Accuracy": 0.7450,
                "Macro_F1": 0.7402,
                "Misclassified": 51,
            },
        },
        "output_directory": str(output_dir),
    }

    with open(output_dir / "experiment3_comparison.json", "w", encoding="utf-8") as f:
        json.dump(summary, f, indent=2)

    logger.info("Experiment 3 complete. Artifacts written to '%s'", output_dir)
    return summary


if __name__ == "__main__":
    summary = run_experiment3()

    a_m = summary["arm_a_baseline_rerun"]["overall_metrics"]
    b_m = summary["arm_b_cleaned_text"]["overall_metrics"]
    d = summary["delta_b_minus_a"]
    bp = summary["boilerplate_removal"]
    changed = summary["changed_records_analysis"]

    print("\n" + "=" * 70)
    print("EXPERIMENT 3 — BOILERPLATE REMOVAL COMPARISON COMPLETE")
    print("=" * 70)
    print(f"  Dataset rows                  : {summary['dataset']['row_count']}")
    print(f"  Records changed by cleaning   : {bp['records_affected']} / {summary['dataset']['row_count']}")
    print(f"  Records unchanged             : {bp['records_unchanged']}")
    print()
    print("  Boilerplate phrases removed (exact literal only):")
    for p in bp["phrases_removed"]:
        print(f"    • {p}")
    print()
    print(f"  {'Metric':<20} {'Arm A (Baseline)':<20} {'Arm B (Cleaned)':<20} {'Delta (B-A)'}")
    print(f"  {'-'*75}")
    print(f"  {'Accuracy':<20} {a_m['Accuracy']:<20.4f} {b_m['Accuracy']:<20.4f} {d['Accuracy']:+.4f}")
    print(f"  {'Macro-F1':<20} {a_m['Macro_F1']:<20.4f} {b_m['Macro_F1']:<20.4f} {d['Macro_F1']:+.4f}")
    print(f"  {'Weighted-F1':<20} {a_m['Weighted_F1']:<20.4f} {b_m['Weighted_F1']:<20.4f} {d['Weighted_F1']:+.4f}")
    print(f"  {'Misclassified':<20} {a_m['Misclassified']:<20} {b_m['Misclassified']:<20} {d['Misclassified']:+d}")
    print()
    print("  Changed-Record Analysis (19 boilerplate-affected records only):")
    print(f"    Arm A correct : {changed['arm_a_correct_on_changed']} / {changed['total_changed']}")
    print(f"    Arm B correct : {changed['arm_b_correct_on_changed']} / {changed['total_changed']}")
    print(f"    Cleaning helped (A wrong -> B correct) : {changed['cleaning_helped_count']}")
    print(f"    Cleaning hurt  (A correct -> B wrong)  : {changed['cleaning_hurt_count']}")
    print()
    print("  Per-Class Delta (Macro-F1, Arm B - Arm A):")
    for label, delta_pc in summary["per_class_delta"].items():
        print(
            f"    {label[:38]:<38} "
            f"A={delta_pc['Arm_A_F1']:.4f}  B={delta_pc['Arm_B_F1']:.4f}  d={delta_pc['F1_delta']:+.4f}"
        )
    print()
    print("  Historical Reference:")
    h1 = summary["historical_reference"]["experiment_1_baseline"]
    h2 = summary["historical_reference"]["experiment_2_original"]
    print(f"    Experiment 1 (Source_Group_ID):  Accuracy={h1['Accuracy']}, Macro-F1={h1['Macro_F1']}")
    print(f"    Experiment 2 (Combined_Group):   Accuracy={h2['Accuracy']}, Macro-F1={h2['Macro_F1']}")
    print(f"    Experiment 3 Arm A (Rerun):      Accuracy={a_m['Accuracy']:.4f}, Macro-F1={a_m['Macro_F1']:.4f}")
    print(f"    Experiment 3 Arm B (Cleaned):    Accuracy={b_m['Accuracy']:.4f}, Macro-F1={b_m['Macro_F1']:.4f}")
    print()
    print("  CAUTION: Development CV results only. Not untouched held-out test results.")
    print("=" * 70)
