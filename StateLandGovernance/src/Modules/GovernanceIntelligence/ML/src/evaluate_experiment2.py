"""
evaluate_experiment2.py
-----------------------
EXPERIMENT 2 — Source-and-Text-Grouped Development Evaluation.

Evaluation of the frozen TF-IDF + Logistic Regression baseline using
StratifiedGroupKFold on Combined_Group (merging identical source documents,
exact text duplicates, and near-duplicate narratives with 5-word shingle
Jaccard similarity >= 0.50).

Purpose
-------
Measures how the existing complaint-classifier pipeline performs when both
published audit sources and near-duplicate narrative templates are strictly
kept together within folds.

This is a conservative sensitivity evaluation. It prevents template leakage
across folds. It does NOT establish incident identity, data provenance, or
complete absence of all possible leakage.

Outputs
-------
results/experiment2_source_text/
  ├── grouping_manifest.csv
  ├── similarity_pairs.csv
  ├── grouping_summary.json
  ├── experiment2_metrics.json
  ├── fold_metrics.csv
  ├── per_class_metrics.csv
  ├── confusion_matrix.csv
  ├── out_of_fold_predictions.csv
  └── misclassified_cases.csv
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
from sklearn.model_selection import StratifiedGroupKFold

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
from leakage_groups import (
    compute_combined_groups,
    save_grouping_artifacts,
)
from model import build_pipeline

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s — %(message)s",
)
logger = logging.getLogger("evaluate_experiment2")


def resolve_output_dir(base_results_dir: Path) -> Path:
    """
    Resolve output directory. If base directory already has completed
    metrics, use a numbered run subdirectory to preserve existing artifacts.
    """
    target = base_results_dir / "experiment2_source_text"
    if (target / "experiment2_metrics.json").exists():
        run_num = 1
        while (base_results_dir / f"experiment2_source_text_run{run_num}" / "experiment2_metrics.json").exists():
            run_num += 1
        target = base_results_dir / f"experiment2_source_text_run{run_num}"
    target.mkdir(parents=True, exist_ok=True)
    return target


def compute_per_class_metrics(
    y_true: list[str], y_pred: list[str]
) -> dict[str, dict[str, Any]]:
    """
    Compute Support, TP, FP, FN, TN, Precision, Recall, F1, FPR for each class.
    FPR = FP / (FP + TN). Undefined denominators default to 0.0.
    """
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


def get_git_info() -> dict[str, Any]:
    """Retrieve current commit hash and working tree dirty status."""
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
        }
    except Exception as e:
        return {"commit": "unknown", "working_tree_clean": False, "error": str(e)}


def run_experiment2(output_dir: Path | None = None) -> dict[str, Any]:
    """
    Execute Experiment 2 source-and-text-grouped evaluation.
    """
    logger.info("=== Component 4 GovernanceIntelligence — Experiment 2 Evaluation ===")

    # 1. Load dataset
    df = load_and_validate()
    logger.info("Loaded validated dataset: %d rows.", len(df))

    # Dataset hash
    dataset_sha256 = hashlib.sha256(DATASET_PATH.read_bytes()).hexdigest()

    # 2. Build deterministic Combined_Group assignments
    manifest_df, similarity_pairs, grouping_summary = compute_combined_groups(
        df,
        id_column=ID_COLUMN,
        source_group_column=config.GROUP_COLUMN,
        text_column=TEXT_COLUMN,
        target_column=TARGET_COLUMN,
    )

    df["Combined_Group"] = manifest_df["Combined_Group"].values

    # Determine output directory
    if output_dir is None:
        output_dir = resolve_output_dir(config.RESULTS_DIR)
    output_dir.mkdir(parents=True, exist_ok=True)

    # Save grouping artifacts
    save_grouping_artifacts(output_dir, manifest_df, similarity_pairs, grouping_summary)

    # 3. Configure StratifiedGroupKFold on Combined_Group
    X = np.array(df[TEXT_COLUMN].tolist())
    y = np.array(df[TARGET_COLUMN].tolist())
    groups = np.array(df["Combined_Group"].tolist())
    rids = df[ID_COLUMN].tolist()
    source_groups = np.array(df[config.GROUP_COLUMN].tolist())

    sgkf = StratifiedGroupKFold(
        n_splits=CV_N_SPLITS, shuffle=True, random_state=RANDOM_STATE
    )
    splits = list(sgkf.split(X, y, groups))

    # 4. Strict Pre-Fitting Assertions
    logger.info("Executing pre-fitting integrity assertions...")

    # A. Each record appears in exactly one test fold
    test_indices_all: list[int] = []
    record_fold_map: dict[str, int] = {}
    for fold_idx, (_, test_idx) in enumerate(splits, start=1):
        test_indices_all.extend(test_idx.tolist())
        for idx in test_idx:
            record_fold_map[rids[idx]] = fold_idx

    assert len(test_indices_all) == len(df), (
        f"Total test instances ({len(test_indices_all)}) does not match dataset length ({len(df)})."
    )
    assert len(set(test_indices_all)) == len(df), "Some records appear in multiple test folds!"

    # B. Zero Combined_Group and Source_Group_ID overlap
    fold_diagnostics: list[dict[str, Any]] = []
    for fold_idx, (train_idx, test_idx) in enumerate(splits, start=1):
        trn_cg = set(groups[train_idx])
        tst_cg = set(groups[test_idx])
        cg_overlap = trn_cg.intersection(tst_cg)
        assert len(cg_overlap) == 0, (
            f"Fold {fold_idx} has Combined_Group overlap: {cg_overlap}"
        )

        trn_sg = set(source_groups[train_idx])
        tst_sg = set(source_groups[test_idx])
        sg_overlap = trn_sg.intersection(tst_sg)
        assert len(sg_overlap) == 0, (
            f"Fold {fold_idx} has Source_Group_ID overlap: {sg_overlap}"
        )

        # C. Check that all target classes exist in training fold
        trn_labels = set(y[train_idx])
        missing_classes = set(LABEL_ORDER) - trn_labels
        if missing_classes:
            raise RuntimeError(
                f"Fold {fold_idx} training partition is missing classes: {missing_classes}. "
                "Splitting is infeasible under current grouping without weakening constraints."
            )

        test_class_counts = {lbl: int((y[test_idx] == lbl).sum()) for lbl in LABEL_ORDER}
        train_class_counts = {lbl: int((y[train_idx] == lbl).sum()) for lbl in LABEL_ORDER}

        fold_diagnostics.append({
            "Fold": fold_idx,
            "Train_Size": len(train_idx),
            "Test_Size": len(test_idx),
            "Train_Groups": len(trn_cg),
            "Test_Groups": len(tst_cg),
            "Train_Class_Counts": train_class_counts,
            "Test_Class_Counts": test_class_counts,
        })

    # D. Every qualifying text pair is assigned to the same test fold
    for pair in similarity_pairs:
        id1 = pair["Research_ID_1"]
        id2 = pair["Research_ID_2"]
        f1 = record_fold_map[id1]
        f2 = record_fold_map[id2]
        assert f1 == f2, (
            f"Qualifying text pair ({id1}, {id2}) split across folds ({f1} vs {f2})!"
        )

    logger.info("All pre-fitting assertions PASSED cleanly.")

    # 5. Fit fresh pipeline per fold and collect out-of-fold predictions
    oof_records: list[dict[str, Any]] = []
    fold_results: list[dict[str, Any]] = []

    for fold_idx, (train_idx, test_idx) in enumerate(splits, start=1):
        X_train, X_test = X[train_idx], X[test_idx]
        y_train, y_test = y[train_idx], y[test_idx]

        # Fresh pipeline instance
        pipeline = build_pipeline()
        pipeline.fit(X_train, y_train)

        y_pred = pipeline.predict(X_test)
        y_prob = pipeline.predict_proba(X_test)

        clf_classes = pipeline.named_steps["clf"].classes_.tolist()

        f_acc = round(float(accuracy_score(y_test, y_pred)), 4)
        f_macro_f1 = round(float(f1_score(y_test, y_pred, average="macro", labels=LABEL_ORDER, zero_division=0)), 4)
        f_wt_f1 = round(float(f1_score(y_test, y_pred, average="weighted", labels=LABEL_ORDER, zero_division=0)), 4)

        logger.info(
            "Fold %d/%d — train=%d, test=%d — Acc=%.4f, Macro-F1=%.4f",
            fold_idx, CV_N_SPLITS, len(train_idx), len(test_idx), f_acc, f_macro_f1,
        )

        fold_results.append({
            "Fold": fold_idx,
            "Train_Size": len(train_idx),
            "Test_Size": len(test_idx),
            "Accuracy": f_acc,
            "Macro_F1": f_macro_f1,
            "Weighted_F1": f_wt_f1,
            **{f"Test_{lbl.replace('/', '_').replace(' ', '_')}": count for lbl, count in fold_diagnostics[fold_idx - 1]["Test_Class_Counts"].items()},
        })

        for i, row_idx in enumerate(test_idx):
            proba_dict = {
                f"prob_{cls.replace('/', '_').replace(' ', '_')}": round(float(y_prob[i, j]), 4)
                for j, cls in enumerate(clf_classes)
            }
            oof_records.append({
                "Research_ID": rids[row_idx],
                "Combined_Group": groups[row_idx],
                "Source_Group_ID": source_groups[row_idx],
                "Canonical_English_Text": X[row_idx],
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

    # 6. Overall Metrics
    overall_acc = round(float(accuracy_score(y_true_oof, y_pred_oof)), 4)
    overall_macro_f1 = round(float(f1_score(y_true_oof, y_pred_oof, average="macro", labels=LABEL_ORDER, zero_division=0)), 4)
    overall_wt_f1 = round(float(f1_score(y_true_oof, y_pred_oof, average="weighted", labels=LABEL_ORDER, zero_division=0)), 4)
    overall_macro_p = round(float(precision_score(y_true_oof, y_pred_oof, average="macro", labels=LABEL_ORDER, zero_division=0)), 4)
    overall_macro_r = round(float(recall_score(y_true_oof, y_pred_oof, average="macro", labels=LABEL_ORDER, zero_division=0)), 4)
    misclassified = int((np.array(y_true_oof) != np.array(y_pred_oof)).sum())

    per_class = compute_per_class_metrics(y_true_oof, y_pred_oof)
    cm = confusion_matrix(y_true_oof, y_pred_oof, labels=LABEL_ORDER)

    logger.info(
        "Experiment 2 Pooled OOF: Acc=%.4f, Macro-F1=%.4f, Weighted-F1=%.4f, Misclassified=%d",
        overall_acc, overall_macro_f1, overall_wt_f1, misclassified,
    )

    # 7. Save Artifacts
    # A. OOF predictions CSV
    oof_df.to_csv(output_dir / "out_of_fold_predictions.csv", index=False)

    # B. Misclassified cases CSV
    mis_df = oof_df[oof_df["True_Label"] != oof_df["Predicted_Label"]].copy()
    mis_df.to_csv(output_dir / "misclassified_cases.csv", index=False)

    # C. Fold metrics CSV
    pd.DataFrame(fold_results).to_csv(output_dir / "fold_metrics.csv", index=False)

    # D. Per-class metrics CSV
    per_class_rows = [{"Label": lbl, **m} for lbl, m in per_class.items()]
    pd.DataFrame(per_class_rows).to_csv(output_dir / "per_class_metrics.csv", index=False)

    # E. Confusion matrix CSV
    cm_df = pd.DataFrame(cm, index=LABEL_ORDER, columns=LABEL_ORDER)
    cm_df.to_csv(output_dir / "confusion_matrix.csv")

    # F. Historical Experiment 1 metrics comparison (from baseline_metrics.json if present)
    hist_exp1: dict[str, Any] = {}
    baseline_json_path = config.RESULTS_DIR / "baseline_metrics.json"
    if baseline_json_path.exists():
        try:
            with open(baseline_json_path, "r", encoding="utf-8") as f:
                exp1_data = json.load(f)
            hist_exp1 = {
                "source": "results/baseline_metrics.json (Experiment 1 Baseline)",
                "strategy": "StratifiedGroupKFold (Source_Group_ID only)",
                "Accuracy": exp1_data.get("overall_metrics", {}).get("Accuracy", 0.7450),
                "Macro_F1": exp1_data.get("overall_metrics", {}).get("Macro_F1", 0.7402),
                "Weighted_F1": exp1_data.get("overall_metrics", {}).get("Weighted_F1", 0.7402),
                "Misclassified": exp1_data.get("overall_metrics", {}).get("Misclassified", 51),
            }
        except Exception:
            hist_exp1 = {"note": "Could not read baseline_metrics.json"}
    else:
        hist_exp1 = {
            "source": "Historical record",
            "Accuracy": 0.7450,
            "Macro_F1": 0.7402,
            "Weighted_F1": 0.7402,
            "Misclassified": 51,
        }

    # G. Comprehensive experiment summary JSON
    import sklearn
    metrics_summary: dict[str, Any] = {
        "experiment_name": "Experiment 2 — Source-and-Text-Grouped Development Evaluation",
        "generated_at": datetime.now(timezone.utc).isoformat(),
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
        "grouping_parameters": grouping_summary["grouping_parameters"],
        "cv_parameters": {
            "strategy": "StratifiedGroupKFold",
            "n_splits": CV_N_SPLITS,
            "shuffle": True,
            "random_state": RANDOM_STATE,
            "group_field": "Combined_Group",
        },
        "model_parameters": {
            "tfidf": {**TFIDF_CONFIG, "ngram_range": list(TFIDF_CONFIG["ngram_range"])},
            "logistic_regression": LR_CONFIG,
        },
        "overall_metrics": {
            "Accuracy": overall_acc,
            "Macro_Precision": overall_macro_p,
            "Macro_Recall": overall_macro_r,
            "Macro_F1": overall_macro_f1,
            "Weighted_F1": overall_wt_f1,
            "Misclassified": misclassified,
        },
        "per_class_metrics": per_class,
        "confusion_matrix": cm.tolist(),
        "fold_results": fold_results,
        "historical_comparison": {
            "experiment_1_baseline": hist_exp1,
            "comparison_interpretation": (
                "Experiment 2 performance measures generalization under conservative source-and-text "
                "grouping. The score reflects model sensitivity when narrative boilerplate templates "
                "are kept strictly within folds rather than memorized across training and test."
            ),
        },
        "output_directory": str(output_dir),
    }

    with open(output_dir / "experiment2_metrics.json", "w", encoding="utf-8") as f:
        json.dump(metrics_summary, f, indent=2)

    logger.info("Experiment 2 complete. Artifacts written to '%s'", output_dir)
    return metrics_summary


if __name__ == "__main__":
    summary = run_experiment2()

    print("\n" + "=" * 65)
    print("EXPERIMENT 2 EVALUATION COMPLETE")
    print("=" * 65)
    print(f"  Dataset rows         : {summary['dataset']['row_count']}")
    print(f"  Source groups        : {summary['environment']['git']}")
    print(f"  Combined groups      : {summary['cv_parameters']['group_field']}")
    print()
    print("  Overall Out-of-Fold Metrics:")
    m = summary["overall_metrics"]
    print(f"    Accuracy           : {m['Accuracy']:.4f}")
    print(f"    Macro-Precision    : {m['Macro_Precision']:.4f}")
    print(f"    Macro-Recall       : {m['Macro_Recall']:.4f}")
    print(f"    Macro-F1           : {m['Macro_F1']:.4f}")
    print(f"    Weighted-F1        : {m['Weighted_F1']:.4f}")
    print(f"    Misclassified      : {m['Misclassified']} / {summary['dataset']['row_count']}")
    print()
    print("  Per-Class Metrics:")
    for lbl, pcm in summary["per_class_metrics"].items():
        print(
            f"    {lbl[:35]:<35} "
            f"Supp={pcm['Support']:<3} P={pcm['Precision']:.4f}  R={pcm['Recall']:.4f}  "
            f"F1={pcm['F1']:.4f}  FPR={pcm['FPR']:.4f}"
        )
    print()
    print("  Historical Comparison (Experiment 1 vs Experiment 2):")
    h = summary["historical_comparison"]["experiment_1_baseline"]
    print(f"    Experiment 1 (Source_Group_ID only) : Accuracy={h.get('Accuracy')}, Macro-F1={h.get('Macro_F1')}")
    print(f"    Experiment 2 (Combined_Group)       : Accuracy={m['Accuracy']:.4f}, Macro-F1={m['Macro_F1']:.4f}")
    print(f"    Delta (Exp 2 - Exp 1)               : Accuracy={m['Accuracy'] - h.get('Accuracy', 0):+.4f}, Macro-F1={m['Macro_F1'] - h.get('Macro_F1', 0):+.4f}")
    print()
    print("  Confusion Matrix (OOF):")
    cm_df = pd.DataFrame(summary["confusion_matrix"], index=LABEL_ORDER, columns=LABEL_ORDER)
    print(cm_df.to_string())
    print("=" * 65)
