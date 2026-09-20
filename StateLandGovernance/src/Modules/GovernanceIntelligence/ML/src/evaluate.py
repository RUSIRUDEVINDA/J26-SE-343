"""
evaluate.py
-----------
EXPERIMENT 1 — Source-Group-Aware Baseline.

Group-aware cross-validation evaluation for the Component 4
GovernanceIntelligence text-classification ML module.

This is the preserved original baseline. Its output artefacts (below) are
not overwritten by later experiments. See evaluate_experiment2.py for the
template-aware Combined_Group evaluation (Experiment 2) — that is a
separate script writing to a separate results/ subdirectory, not a
modification of this one.

Evaluation methodology
----------------------
* StratifiedGroupKFold(n_splits=5, shuffle=True, random_state=42)
* groups = Source_Group_ID  (prevents leakage from the same audit source
  only — does not detect cross-document template-sharing; see Experiment 2
  for that check)
* Each fold builds a fresh pipeline via model.build_pipeline()
* TF-IDF is fitted only on the training portion of each fold
* Out-of-fold (OOF) predictions are collected across all folds
* A DummyClassifier (most_frequent strategy) is also evaluated for comparison

Metrics computed
----------------
For each class: Precision, Recall, F1, FPR
Aggregate: Accuracy, Macro-Precision, Macro-Recall, Macro-F1, Weighted-F1
Confusion matrix with the fixed label order from config.LABEL_ORDER

Output artefacts
----------------
results/baseline_metrics.json
results/fold_metrics.csv
results/per_class_metrics.csv
results/confusion_matrix.csv
results/confusion_matrix.png
results/out_of_fold_predictions.csv
results/misclassified_cases.csv

Privacy note
------------
out_of_fold_predictions.csv contains Research_ID and complaint text.
This is acceptable because it is a research artefact and does not
contain officer PII or sensitive financial records.
misclassified_cases.csv is a subset of OOF predictions and follows
the same privacy constraints.
"""

from __future__ import annotations

import json
import logging
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import numpy as np
import pandas as pd
from sklearn.dummy import DummyClassifier
from sklearn.metrics import (
    accuracy_score,
    confusion_matrix,
    f1_score,
    precision_score,
    recall_score,
)
from sklearn.model_selection import StratifiedGroupKFold

# Matplotlib backend must be set before importing pyplot
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

# Add src/ to path
sys.path.insert(0, str(Path(__file__).resolve().parent))
from config import (
    BASELINE_METRICS_JSON,
    CONFUSION_MATRIX_CSV,
    CONFUSION_MATRIX_PNG,
    CV_N_SPLITS,
    DATASET_PATH,
    FOLD_METRICS_CSV,
    GROUP_COLUMN,
    ID_COLUMN,
    LABEL_ORDER,
    LR_CONFIG,
    MISCLASSIFIED_CSV,
    OOF_PREDICTIONS_CSV,
    PER_CLASS_METRICS_CSV,
    RANDOM_STATE,
    RESULTS_DIR,
    TARGET_COLUMN,
    TEXT_COLUMN,
    TFIDF_CONFIG,
)
from data_loader import get_feature_arrays, get_class_distribution, load_and_validate
from model import build_pipeline

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s — %(message)s",
)
logger = logging.getLogger(__name__)


# ---------------------------------------------------------------------------
# Metric helpers
# ---------------------------------------------------------------------------

def _compute_per_class_metrics(
    y_true: list[str], y_pred: list[str]
) -> dict[str, dict[str, float]]:
    """
    Compute Precision, Recall, F1, FPR for each class.

    FPR = FP / (FP + TN)   [NOT 1 - Precision]

    Returns a dict keyed by class label (in LABEL_ORDER).
    """
    cm = confusion_matrix(y_true, y_pred, labels=LABEL_ORDER)
    n  = cm.sum()
    results: dict[str, dict[str, float]] = {}

    for i, label in enumerate(LABEL_ORDER):
        tp = int(cm[i, i])
        fp = int(cm[:, i].sum() - tp)
        fn = int(cm[i, :].sum() - tp)
        tn = int(n - tp - fp - fn)

        precision = tp / (tp + fp) if (tp + fp) > 0 else 0.0
        recall    = tp / (tp + fn) if (tp + fn) > 0 else 0.0
        f1        = (2 * tp) / (2 * tp + fp + fn) if (2 * tp + fp + fn) > 0 else 0.0
        fpr       = fp / (fp + tn) if (fp + tn) > 0 else 0.0

        results[label] = {
            "TP"       : tp,
            "FP"       : fp,
            "FN"       : fn,
            "TN"       : tn,
            "Precision": round(precision, 4),
            "Recall"   : round(recall, 4),
            "F1"       : round(f1, 4),
            "FPR"      : round(fpr, 4),
        }

    return results


def _save_confusion_matrix_png(
    cm: np.ndarray, path: Path
) -> None:
    """Save a colour-mapped confusion matrix plot."""
    fig, ax = plt.subplots(figsize=(8, 7))
    im = ax.imshow(cm, interpolation="nearest", cmap="Blues")
    plt.colorbar(im, ax=ax)

    # Short class names for display
    short_labels = [
        "Admin/Proc/Int",
        "Revenue/Enf",
        "Unauth Alloc",
        "Env Misuse",
    ]
    tick_marks = range(len(short_labels))
    ax.set_xticks(list(tick_marks))
    ax.set_yticks(list(tick_marks))
    ax.set_xticklabels(short_labels, rotation=25, ha="right", fontsize=9)
    ax.set_yticklabels(short_labels, fontsize=9)

    thresh = cm.max() / 2.0
    for i in range(cm.shape[0]):
        for j in range(cm.shape[1]):
            ax.text(
                j, i, str(cm[i, j]),
                ha="center", va="center",
                color="white" if cm[i, j] > thresh else "black",
                fontsize=12,
            )

    ax.set_xlabel("Predicted label", fontsize=11)
    ax.set_ylabel("True label", fontsize=11)
    ax.set_title(
        "Component 4 GovernanceIntelligence — Confusion Matrix\n"
        "(TF-IDF + Logistic Regression, StratifiedGroupKFold OOF)",
        fontsize=10,
    )
    plt.tight_layout()
    plt.savefig(path, dpi=150, bbox_inches="tight")
    plt.close(fig)
    logger.info("Confusion matrix plot saved to '%s'", path)


# ---------------------------------------------------------------------------
# Main evaluation
# ---------------------------------------------------------------------------

def run_evaluation(df: pd.DataFrame) -> dict[str, Any]:
    """
    Run StratifiedGroupKFold cross-validation and collect all metrics.

    Parameters
    ----------
    df : pd.DataFrame
        Validated dataset from data_loader.load_and_validate().

    Returns
    -------
    dict
        Complete evaluation results (also written to RESULTS_DIR).
    """
    RESULTS_DIR.mkdir(parents=True, exist_ok=True)

    X_list, y_list, groups_list = get_feature_arrays(df)
    X      = np.array(X_list)
    y      = np.array(y_list)
    groups = np.array(groups_list)
    ids    = df[ID_COLUMN].tolist()

    # Preserve alignment with original dataframe index
    sgkf = StratifiedGroupKFold(
        n_splits=CV_N_SPLITS, shuffle=True, random_state=RANDOM_STATE
    )

    oof_records: list[dict] = []
    fold_records: list[dict] = []

    logger.info(
        "Starting StratifiedGroupKFold evaluation: %d folds, %d records, %d groups",
        CV_N_SPLITS, len(X), len(set(groups_list)),
    )

    for fold_idx, (train_idx, test_idx) in enumerate(
        sgkf.split(X, y, groups), start=1
    ):
        X_train, X_test = X[train_idx], X[test_idx]
        y_train, y_test = y[train_idx], y[test_idx]

        # Build a completely fresh pipeline for this fold
        pipeline = build_pipeline()
        pipeline.fit(X_train, y_train)

        y_pred = pipeline.predict(X_test)
        y_prob = pipeline.predict_proba(X_test)

        fold_acc     = round(float(accuracy_score(y_test, y_pred)), 4)
        fold_macro_f1= round(float(f1_score(y_test, y_pred, average="macro", labels=LABEL_ORDER)), 4)
        fold_wt_f1   = round(float(f1_score(y_test, y_pred, average="weighted", labels=LABEL_ORDER)), 4)

        logger.info(
            "Fold %d/%d — train=%d, test=%d — Accuracy=%.4f, Macro-F1=%.4f",
            fold_idx, CV_N_SPLITS, len(train_idx), len(test_idx),
            fold_acc, fold_macro_f1,
        )

        fold_records.append({
            "Fold"          : fold_idx,
            "Train_Size"    : len(train_idx),
            "Test_Size"     : len(test_idx),
            "Accuracy"      : fold_acc,
            "Macro_F1"      : fold_macro_f1,
            "Weighted_F1"   : fold_wt_f1,
        })

        # Class names for proba columns (from pipeline's classifier)
        clf_classes = pipeline.named_steps["clf"].classes_.tolist()

        for i, row_idx in enumerate(test_idx):
            proba_dict = {
                f"prob_{cls.replace('/', '_').replace(' ', '_')}": round(float(y_prob[i, j]), 4)
                for j, cls in enumerate(clf_classes)
            }
            oof_records.append({
                "Research_ID"           : ids[row_idx],
                "Canonical_English_Text": X[row_idx],
                "True_Label"            : y[row_idx],
                "Predicted_Label"       : y_pred[i],
                "Source_Group_ID"       : groups[row_idx],
                "Fold"                  : fold_idx,
                **proba_dict,
            })

    # Assemble OOF dataframe (sort by original dataset order)
    oof_df = pd.DataFrame(oof_records)
    id_order = {rid: idx for idx, rid in enumerate(ids)}
    oof_df["_sort_key"] = oof_df["Research_ID"].map(id_order)
    oof_df = oof_df.sort_values("_sort_key").drop(columns=["_sort_key"])

    y_true_oof = oof_df["True_Label"].tolist()
    y_pred_oof = oof_df["Predicted_Label"].tolist()

    # --- Overall metrics ---
    overall_acc   = round(float(accuracy_score(y_true_oof, y_pred_oof)), 4)
    overall_macro_f1 = round(float(f1_score(y_true_oof, y_pred_oof, average="macro", labels=LABEL_ORDER)), 4)
    overall_wt_f1    = round(float(f1_score(y_true_oof, y_pred_oof, average="weighted", labels=LABEL_ORDER)), 4)
    overall_macro_p  = round(float(precision_score(y_true_oof, y_pred_oof, average="macro", labels=LABEL_ORDER)), 4)
    overall_macro_r  = round(float(recall_score(y_true_oof, y_pred_oof, average="macro", labels=LABEL_ORDER)), 4)
    misclassified    = int((np.array(y_true_oof) != np.array(y_pred_oof)).sum())

    logger.info(
        "Overall OOF — Accuracy=%.4f, Macro-F1=%.4f, Weighted-F1=%.4f, Misclassified=%d",
        overall_acc, overall_macro_f1, overall_wt_f1, misclassified,
    )

    # --- Per-class metrics ---
    per_class = _compute_per_class_metrics(y_true_oof, y_pred_oof)

    # --- Confusion matrix ---
    cm = confusion_matrix(y_true_oof, y_pred_oof, labels=LABEL_ORDER)

    # --- DummyClassifier baseline ---
    dummy_oof_preds: list[str] = []
    dummy_oof_true:  list[str] = []

    for fold_idx, (train_idx, test_idx) in enumerate(
        sgkf.split(X, y, groups), start=1
    ):
        dummy = DummyClassifier(strategy="most_frequent", random_state=RANDOM_STATE)
        dummy.fit(X[train_idx], y[train_idx])
        dummy_preds = dummy.predict(X[test_idx])
        dummy_oof_preds.extend(dummy_preds.tolist())
        dummy_oof_true.extend(y[test_idx].tolist())

    dummy_acc     = round(float(accuracy_score(dummy_oof_true, dummy_oof_preds)), 4)
    dummy_macro_f1= round(float(f1_score(dummy_oof_true, dummy_oof_preds, average="macro",
                                          labels=LABEL_ORDER, zero_division=0)), 4)
    dummy_wt_f1   = round(float(f1_score(dummy_oof_true, dummy_oof_preds, average="weighted",
                                          labels=LABEL_ORDER, zero_division=0)), 4)

    logger.info(
        "DummyClassifier baseline — Accuracy=%.4f, Macro-F1=%.4f",
        dummy_acc, dummy_macro_f1,
    )

    # -----------------------------------------------------------------------
    # Save artefacts
    # -----------------------------------------------------------------------

    # 1. OOF predictions CSV
    oof_df.to_csv(OOF_PREDICTIONS_CSV, index=False)
    logger.info("OOF predictions saved to '%s'", OOF_PREDICTIONS_CSV)

    # 2. Misclassified cases CSV
    mis_df = oof_df[oof_df["True_Label"] != oof_df["Predicted_Label"]].copy()
    mis_df.to_csv(MISCLASSIFIED_CSV, index=False)
    logger.info("Misclassified cases (%d) saved to '%s'", len(mis_df), MISCLASSIFIED_CSV)

    # 3. Fold metrics CSV
    fold_df = pd.DataFrame(fold_records)
    fold_df.to_csv(FOLD_METRICS_CSV, index=False)
    logger.info("Fold metrics saved to '%s'", FOLD_METRICS_CSV)

    # 4. Per-class metrics CSV
    per_class_rows = [
        {"Label": label, **metrics}
        for label, metrics in per_class.items()
    ]
    per_class_df = pd.DataFrame(per_class_rows)
    per_class_df.to_csv(PER_CLASS_METRICS_CSV, index=False)
    logger.info("Per-class metrics saved to '%s'", PER_CLASS_METRICS_CSV)

    # 5. Confusion matrix CSV
    cm_df = pd.DataFrame(cm, index=LABEL_ORDER, columns=LABEL_ORDER)
    cm_df.to_csv(CONFUSION_MATRIX_CSV)
    logger.info("Confusion matrix CSV saved to '%s'", CONFUSION_MATRIX_CSV)

    # 6. Confusion matrix PNG
    _save_confusion_matrix_png(cm, CONFUSION_MATRIX_PNG)

    # 7. Summary JSON
    import sklearn
    summary: dict[str, Any] = {
        "generated_at"         : datetime.now(timezone.utc).isoformat(),
        "dataset_path"         : str(DATASET_PATH),
        "row_count"            : len(df),
        "class_distribution"   : get_class_distribution(df),
        "unique_group_count"   : int(df[GROUP_COLUMN].nunique()),
        "sklearn_version"      : sklearn.__version__,
        "python_version"       : sys.version,
        "label_order"          : LABEL_ORDER,
        "tfidf_config"         : {
            **TFIDF_CONFIG,
            "ngram_range": list(TFIDF_CONFIG["ngram_range"]),
        },
        "lr_config"            : LR_CONFIG,
        "cv_config"            : {
            "strategy"    : "StratifiedGroupKFold",
            "n_splits"    : CV_N_SPLITS,
            "shuffle"     : True,
            "random_state": RANDOM_STATE,
            "group_field" : GROUP_COLUMN,
        },
        "fold_results"         : fold_records,
        "overall_metrics"      : {
            "Accuracy"        : overall_acc,
            "Macro_Precision" : overall_macro_p,
            "Macro_Recall"    : overall_macro_r,
            "Macro_F1"        : overall_macro_f1,
            "Weighted_F1"     : overall_wt_f1,
            "Misclassified"   : misclassified,
        },
        "per_class_metrics"    : per_class,
        "confusion_matrix"     : cm.tolist(),
        "dummy_baseline"       : {
            "strategy"   : "most_frequent",
            "Accuracy"   : dummy_acc,
            "Macro_F1"   : dummy_macro_f1,
            "Weighted_F1": dummy_wt_f1,
        },
        "historical_reference" : {
            "note"       : "Previous Colab run values for comparison only. Not reproduced exactly.",
            "Accuracy"   : 0.7450,
            "Macro_F1"   : 0.7402,
            "Weighted_F1": 0.7402,
            "Misclassified": 51,
        },
    }

    with open(BASELINE_METRICS_JSON, "w", encoding="utf-8") as f:
        json.dump(summary, f, indent=2)
    logger.info("Baseline metrics JSON saved to '%s'", BASELINE_METRICS_JSON)

    return summary


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------

if __name__ == "__main__":
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s [%(levelname)s] %(name)s — %(message)s",
    )

    logger.info("=== Component 4 GovernanceIntelligence — Evaluation ===")
    df = load_and_validate()
    summary = run_evaluation(df)

    print("\n" + "=" * 60)
    print("EVALUATION COMPLETE")
    print("=" * 60)
    print(f"  Dataset rows       : {summary['row_count']}")
    print(f"  Unique groups      : {summary['unique_group_count']}")
    print()
    print("  Per-fold results:")
    for fold in summary["fold_results"]:
        print(
            f"    Fold {fold['Fold']}: "
            f"train={fold['Train_Size']}, test={fold['Test_Size']}, "
            f"Acc={fold['Accuracy']:.4f}, Macro-F1={fold['Macro_F1']:.4f}"
        )
    print()
    m = summary["overall_metrics"]
    print(f"  Overall Accuracy   : {m['Accuracy']:.4f}")
    print(f"  Macro-Precision    : {m['Macro_Precision']:.4f}")
    print(f"  Macro-Recall       : {m['Macro_Recall']:.4f}")
    print(f"  Macro-F1           : {m['Macro_F1']:.4f}")
    print(f"  Weighted-F1        : {m['Weighted_F1']:.4f}")
    print(f"  Misclassified      : {m['Misclassified']}")
    print()
    print("  Per-class metrics:")
    for label, metrics in summary["per_class_metrics"].items():
        print(
            f"    {label[:35]:<35} "
            f"P={metrics['Precision']:.4f}  R={metrics['Recall']:.4f}  "
            f"F1={metrics['F1']:.4f}  FPR={metrics['FPR']:.4f}"
        )
    print()
    d = summary["dummy_baseline"]
    print(f"  DummyClassifier (most_frequent):")
    print(f"    Accuracy   : {d['Accuracy']:.4f}")
    print(f"    Macro-F1   : {d['Macro_F1']:.4f}")
    print(f"    Weighted-F1: {d['Weighted_F1']:.4f}")
    print()
    print("  Confusion matrix (TF-IDF + LR, OOF):")
    cm_df = pd.read_csv(CONFUSION_MATRIX_CSV, index_col=0)
    print(cm_df.to_string())
    print()
    h = summary["historical_reference"]
    print(f"  Historical reference (previous Colab run):")
    print(f"    Accuracy   : {h['Accuracy']}")
    print(f"    Macro-F1   : {h['Macro_F1']}")
    print()
    print(f"  Artefacts written to: {RESULTS_DIR}")
    print("=" * 60)
