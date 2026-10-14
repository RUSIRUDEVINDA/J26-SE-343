"""Generate a non-overwriting, fully recomputed Experiment 4 report.

Historical experiment files are read-only inputs. Metrics and paired transitions
are recomputed from the two aligned 200-row OOF files; the saved paired CSV and
stored metrics are validation evidence, never sources of truth.
"""

from __future__ import annotations

import json
import math
import sys
from datetime import datetime, timezone
from pathlib import Path

import numpy as np
import pandas as pd
from sklearn.metrics import accuracy_score, confusion_matrix, f1_score, precision_score, recall_score

_SRC_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(_SRC_DIR))

import config
from config import ID_COLUMN, LABEL_ORDER

ML_ROOT = config.ML_ROOT
ARM_A_RUN = ML_ROOT / "results" / "experiment4_synthetic_augmentation_run2"
ARM_B_RUN = ML_ROOT / "results" / "experiment4_synthetic_augmentation_run5"
ARM_A_OOF = ARM_A_RUN / "arm_a" / "oof_predictions.csv"
ARM_A_METRICS = ARM_A_RUN / "arm_a" / "metrics.json"
ARM_A_RUN_META = ARM_A_RUN / "run_metadata.json"
ARM_A_FOLD_MANIFEST = ARM_A_RUN / "baseline_fold_manifest.csv"
ARM_B_OOF = ARM_B_RUN / "arm_b" / "oof_predictions.csv"
ARM_B_METRICS = ARM_B_RUN / "arm_b" / "metrics.json"
ARM_B_RUN_META = ARM_B_RUN / "run_metadata.json"
ARM_B_FOLD_MANIFEST = ARM_B_RUN / "fold_manifest.csv"
PAIRED_CSV = ARM_B_RUN / "paired_prediction_changes.csv"
OUTPUT_DIR = ML_ROOT / "results" / "experiment4_final_report_v2"
OUTPUT_PATH = OUTPUT_DIR / "experiment4_corrected_report_v2.json"

OOF_COLUMNS = {ID_COLUMN, "True_Label", "Predicted_Label", "Fold"}
PAIRED_COLUMNS = {
    ID_COLUMN,
    "True_Label",
    "Arm_A_Pred",
    "Arm_B_Pred",
    "Arm_A_Correct",
    "Arm_B_Correct",
    "Transition",
    "Fold",
}


def require_columns(frame: pd.DataFrame, required: set[str], source: str) -> None:
    missing = sorted(required - set(frame.columns))
    if missing:
        raise ValueError(f"{source} is missing required columns: {missing}")


def validate_oof(frame: pd.DataFrame, source: str, expected_rows: int = 200) -> pd.DataFrame:
    require_columns(frame, OOF_COLUMNS, source)
    if len(frame) != expected_rows:
        raise ValueError(f"{source} must contain exactly {expected_rows} rows, got {len(frame)}")
    ids = frame[ID_COLUMN]
    if ids.isna().any() or ids.astype(str).str.strip().eq("").any():
        raise ValueError(f"{source} contains blank Research_ID values")
    normalised_ids = ids.astype(str).str.strip()
    duplicates = normalised_ids[normalised_ids.duplicated(keep=False)].unique().tolist()
    if duplicates:
        raise ValueError(f"{source} contains duplicate Research_ID values: {duplicates[:10]}")

    allowed = set(LABEL_ORDER)
    for column in ("True_Label", "Predicted_Label"):
        invalid = sorted(set(frame[column].dropna()) - allowed)
        if frame[column].isna().any() or invalid:
            raise ValueError(f"{source} contains invalid {column} values: {invalid}")
    if frame["Fold"].isna().any():
        raise ValueError(f"{source} contains blank Fold values")

    validated = frame.copy()
    validated[ID_COLUMN] = normalised_ids
    return validated


def align_oof(arm_a: pd.DataFrame, arm_b: pd.DataFrame) -> pd.DataFrame:
    a = validate_oof(arm_a, "Arm A OOF")
    b = validate_oof(arm_b, "Arm B OOF")
    a_ids = set(a[ID_COLUMN])
    b_ids = set(b[ID_COLUMN])
    if a_ids != b_ids:
        raise ValueError(
            "Arm A and Arm B Research_ID sets differ: "
            f"only_a={sorted(a_ids - b_ids)[:10]}, only_b={sorted(b_ids - a_ids)[:10]}"
        )

    aligned = a[[ID_COLUMN, "True_Label", "Predicted_Label", "Fold"]].merge(
        b[[ID_COLUMN, "True_Label", "Predicted_Label", "Fold"]],
        on=ID_COLUMN,
        how="inner",
        validate="one_to_one",
        suffixes=("_A", "_B"),
    )
    label_mismatch = aligned["True_Label_A"] != aligned["True_Label_B"]
    fold_mismatch = aligned["Fold_A"] != aligned["Fold_B"]
    if label_mismatch.any() or fold_mismatch.any():
        raise ValueError(
            "Arm alignment failure: "
            f"true_label_mismatches={int(label_mismatch.sum())}, "
            f"fold_mismatches={int(fold_mismatch.sum())}"
        )
    return aligned.sort_values(ID_COLUMN).reset_index(drop=True)


def compute_overall(y_true, y_pred) -> dict:
    true_values = np.asarray(y_true)
    pred_values = np.asarray(y_pred)
    return {
        "Accuracy": float(accuracy_score(true_values, pred_values)),
        "Macro_Precision": float(
            precision_score(true_values, pred_values, average="macro", zero_division=0)
        ),
        "Macro_Recall": float(
            recall_score(true_values, pred_values, average="macro", zero_division=0)
        ),
        "Macro_F1": float(f1_score(true_values, pred_values, average="macro", zero_division=0)),
        "Weighted_F1": float(
            f1_score(true_values, pred_values, average="weighted", zero_division=0)
        ),
        "Misclassified": int((true_values != pred_values).sum()),
    }


def compute_per_class(y_true, y_pred, labels=LABEL_ORDER) -> list[dict]:
    true_values = np.asarray(y_true)
    pred_values = np.asarray(y_pred)
    rows = []
    for label in labels:
        true_mask = true_values == label
        pred_mask = pred_values == label
        tp = int((true_mask & pred_mask).sum())
        fp = int((~true_mask & pred_mask).sum())
        fn = int((true_mask & ~pred_mask).sum())
        tn = int((~true_mask & ~pred_mask).sum())
        precision_denominator = tp + fp
        recall_denominator = tp + fn
        f1_denominator = 2 * tp + fp + fn
        fpr_denominator = fp + tn
        rows.append(
            {
                "Label": label,
                "Support": int(true_mask.sum()),
                "TP": tp,
                "FP": fp,
                "FN": fn,
                "TN": tn,
                "Precision": tp / precision_denominator if precision_denominator else None,
                "Recall": tp / recall_denominator if recall_denominator else None,
                # When TP=0 and FP+FN>0 this correctly returns 0.0.
                # A wholly absent class has an explicit undefined (None) F1.
                "F1": (2 * tp) / f1_denominator if f1_denominator else None,
                "FPR": fp / fpr_denominator if fpr_denominator else None,
            }
        )
    return rows


def recompute_paired(aligned: pd.DataFrame) -> tuple[dict, pd.DataFrame]:
    paired = pd.DataFrame(
        {
            ID_COLUMN: aligned[ID_COLUMN],
            "True_Label": aligned["True_Label_A"],
            "Arm_A_Pred": aligned["Predicted_Label_A"],
            "Arm_B_Pred": aligned["Predicted_Label_B"],
            "Fold": aligned["Fold_A"],
        }
    )
    paired["Arm_A_Correct"] = paired["Arm_A_Pred"] == paired["True_Label"]
    paired["Arm_B_Correct"] = paired["Arm_B_Pred"] == paired["True_Label"]
    conditions = [
        paired["Arm_A_Correct"] & paired["Arm_B_Correct"],
        ~paired["Arm_A_Correct"] & paired["Arm_B_Correct"],
        paired["Arm_A_Correct"] & ~paired["Arm_B_Correct"],
        (paired["Arm_A_Pred"] == paired["Arm_B_Pred"]),
    ]
    choices = ["both_correct", "A_wrong_B_correct", "A_correct_B_wrong", "both_wrong_same_error"]
    paired["Transition"] = np.select(conditions, choices, default="both_wrong_different_error")

    counts = paired["Transition"].value_counts()
    summary = {
        "total_records": len(paired),
        "arm_a_correct": int(paired["Arm_A_Correct"].sum()),
        "arm_b_correct": int(paired["Arm_B_Correct"].sum()),
        "helped": int(counts.get("A_wrong_B_correct", 0)),
        "hurt": int(counts.get("A_correct_B_wrong", 0)),
        "both_correct": int(counts.get("both_correct", 0)),
        "both_wrong_same_error": int(counts.get("both_wrong_same_error", 0)),
        "both_wrong_different_error": int(counts.get("both_wrong_different_error", 0)),
        "label_changes": int((paired["Arm_A_Pred"] != paired["Arm_B_Pred"]).sum()),
    }
    summary["both_wrong_any"] = (
        summary["both_wrong_same_error"] + summary["both_wrong_different_error"]
    )
    summary["net_gain"] = summary["helped"] - summary["hurt"]
    if summary["net_gain"] != summary["arm_b_correct"] - summary["arm_a_correct"]:
        raise AssertionError("Paired transition arithmetic is inconsistent")
    return summary, paired


def validate_saved_paired(saved: pd.DataFrame, recomputed: pd.DataFrame) -> None:
    require_columns(saved, PAIRED_COLUMNS, "Saved paired comparison")
    if len(saved) != 200:
        raise ValueError(f"Saved paired comparison must contain 200 rows, got {len(saved)}")
    ids = saved[ID_COLUMN]
    if ids.isna().any() or ids.astype(str).str.strip().eq("").any() or ids.astype(str).duplicated().any():
        raise ValueError("Saved paired comparison contains blank or duplicate Research_ID values")

    columns = [
        ID_COLUMN,
        "True_Label",
        "Arm_A_Pred",
        "Arm_B_Pred",
        "Arm_A_Correct",
        "Arm_B_Correct",
        "Transition",
        "Fold",
    ]
    expected = recomputed[columns].sort_values(ID_COLUMN).reset_index(drop=True).copy()
    actual = saved[columns].sort_values(ID_COLUMN).reset_index(drop=True).copy()
    for column in columns:
        expected[column] = expected[column].astype(str).str.strip().str.lower()
        actual[column] = actual[column].astype(str).str.strip().str.lower()
    mismatch = (expected != actual).any(axis=1)
    if mismatch.any():
        bad_ids = recomputed.sort_values(ID_COLUMN).reset_index(drop=True).loc[mismatch, ID_COLUMN].tolist()
        raise ValueError(
            "Saved paired comparison disagrees with transitions recomputed from aligned OOF files "
            f"for IDs: {bad_ids[:10]}"
        )


def _read_required_json(path: Path, label: str) -> dict:
    if not path.is_file():
        raise FileNotFoundError(f"Required {label} not found: '{path}'")
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"{label} must be a JSON object")
    return value


def validate_stored_metrics(stored: dict, recomputed: dict, label: str) -> None:
    mapping = {
        "Accuracy": "Accuracy",
        "Macro_F1": "Macro_F1_sklearn",
        "Weighted_F1": "Weighted_F1",
        "Misclassified": "Misclassified",
    }
    for recomputed_key, stored_key in mapping.items():
        if stored_key not in stored:
            raise ValueError(f"{label} stored metrics are missing required field '{stored_key}'")
        actual = recomputed[recomputed_key]
        expected = stored[stored_key]
        if recomputed_key == "Misclassified":
            matches = actual == expected
        else:
            matches = math.isclose(float(actual), float(expected), rel_tol=0.0, abs_tol=5e-5)
        if not matches:
            raise ValueError(
                f"{label} {recomputed_key} disagrees with stored metrics: "
                f"recomputed={actual}, stored={expected}"
            )


def read_per_fold_training() -> dict:
    arm_a_meta = _read_required_json(ARM_A_RUN_META, "Arm A run metadata")
    arm_b_meta = _read_required_json(ARM_B_RUN_META, "Arm B run metadata")
    baseline = pd.read_csv(ARM_A_FOLD_MANIFEST)
    synthetic = pd.read_csv(ARM_B_FOLD_MANIFEST)
    require_columns(baseline, {ID_COLUMN, "Evaluation_Fold"}, "Arm A baseline fold manifest")
    require_columns(
        synthetic,
        {"Research_IDs", "Fold", "Included"},
        "Arm B synthetic fold manifest",
    )
    if len(baseline) != 200 or baseline[ID_COLUMN].nunique() != 200:
        raise ValueError("Arm A baseline fold manifest must contain 200 unique Research_ID values")

    metadata_fold_counts = arm_a_meta.get("fold_replay", {}).get("fold_counts")
    if not isinstance(metadata_fold_counts, dict):
        raise ValueError("Arm A run metadata is missing fold_replay.fold_counts")
    eligible_records = arm_b_meta.get("review_summary", {}).get("eligible_records")
    if not isinstance(eligible_records, int):
        raise ValueError("Arm B run metadata is missing review_summary.eligible_records")

    result = {}
    for fold in range(1, 6):
        eval_count = int((pd.to_numeric(baseline["Evaluation_Fold"]) == fold).sum())
        recorded_eval = metadata_fold_counts.get(str(fold))
        if recorded_eval != eval_count:
            raise ValueError(
                f"Fold {fold} evaluation count differs between metadata and manifest: "
                f"metadata={recorded_eval}, manifest={eval_count}"
            )
        included_rows = synthetic.loc[
            synthetic["Included"].astype(str).str.lower().eq("true")
        ].copy()
        included_folds = pd.to_numeric(included_rows["Fold"], errors="raise")
        included = included_rows.loc[included_folds.eq(fold)]
        synthetic_count = sum(
            len([item for item in str(value).split(";") if item.strip()])
            for value in included["Research_IDs"]
        )
        if synthetic_count != eligible_records:
            raise ValueError(
                f"Fold {fold} synthetic count differs from saved metadata: "
                f"manifest={synthetic_count}, metadata={eligible_records}"
            )
        result[f"fold_{fold}"] = {
            "baseline_train": len(baseline) - eval_count,
            "synthetic_records": synthetic_count,
            "eval": eval_count,
        }

    training_counts = [entry["baseline_train"] for entry in result.values()]
    result["source"] = {
        "baseline_manifest": str(ARM_A_FOLD_MANIFEST),
        "synthetic_manifest": str(ARM_B_FOLD_MANIFEST),
        "run_metadata": [str(ARM_A_RUN_META), str(ARM_B_RUN_META)],
    }
    result["note"] = (
        f"Baseline training size ranges from {min(training_counts)} to {max(training_counts)}. "
        f"{eligible_records} reviewed synthetic variants enter training only; evaluation "
        "is always the 200 baseline OOF records."
    )
    return result


def build_report() -> dict:
    for path in (
        ARM_A_OOF,
        ARM_A_METRICS,
        ARM_A_RUN_META,
        ARM_A_FOLD_MANIFEST,
        ARM_B_OOF,
        ARM_B_METRICS,
        ARM_B_RUN_META,
        ARM_B_FOLD_MANIFEST,
        PAIRED_CSV,
    ):
        if not path.is_file():
            raise FileNotFoundError(f"Required report input missing: '{path}'")

    aligned = align_oof(pd.read_csv(ARM_A_OOF), pd.read_csv(ARM_B_OOF))
    y_true = aligned["True_Label_A"].tolist()
    y_pred_a = aligned["Predicted_Label_A"].tolist()
    y_pred_b = aligned["Predicted_Label_B"].tolist()
    overall_a = compute_overall(y_true, y_pred_a)
    overall_b = compute_overall(y_true, y_pred_b)
    per_class_a = compute_per_class(y_true, y_pred_a)
    per_class_b = compute_per_class(y_true, y_pred_b)
    validate_stored_metrics(
        _read_required_json(ARM_A_METRICS, "Arm A metrics"), overall_a, "Arm A"
    )
    validate_stored_metrics(
        _read_required_json(ARM_B_METRICS, "Arm B metrics"), overall_b, "Arm B"
    )

    paired_summary, paired = recompute_paired(aligned)
    validate_saved_paired(pd.read_csv(PAIRED_CSV), paired)
    helped_rows = paired.loc[paired["Transition"].eq("A_wrong_B_correct")]
    hurt_rows = paired.loc[paired["Transition"].eq("A_correct_B_wrong")]
    revenue_label = "Lease Revenue / Payment / Enforcement"
    revenue_helped = int(helped_rows["True_Label"].eq(revenue_label).sum())
    revenue_a = next(row["TP"] for row in per_class_a if row["Label"] == revenue_label)
    revenue_b = next(row["TP"] for row in per_class_b if row["Label"] == revenue_label)

    deltas = {
        "Accuracy": overall_b["Accuracy"] - overall_a["Accuracy"],
        "Macro_F1": overall_b["Macro_F1"] - overall_a["Macro_F1"],
        "Weighted_F1": overall_b["Weighted_F1"] - overall_a["Weighted_F1"],
        "Misclassified_change": overall_b["Misclassified"] - overall_a["Misclassified"],
    }
    per_class_deltas = {}
    for label in LABEL_ORDER:
        a_row = next(row for row in per_class_a if row["Label"] == label)
        b_row = next(row for row in per_class_b if row["Label"] == label)
        per_class_deltas[label] = {
            "F1_delta": (
                b_row["F1"] - a_row["F1"]
                if b_row["F1"] is not None and a_row["F1"] is not None
                else None
            ),
            "TP_delta": b_row["TP"] - a_row["TP"],
        }

    record_columns = [ID_COLUMN, "True_Label", "Arm_A_Pred", "Arm_B_Pred", "Fold"]
    return {
        "report_title": "Experiment 4 Corrected Comparative Report v2",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "important_caveat": (
            "Development cross-validation results on 200 baseline records. Frozen fold "
            "assignments replayed. Not untouched held-out test results. Researcher review "
            "is not independent professional governance validation."
        ),
        "alignment": {
            "rows": len(aligned),
            "unique_ids_each_arm": int(aligned[ID_COLUMN].nunique()),
            "id_sets_match": True,
            "true_labels_match": True,
            "frozen_folds_match": True,
            "join_validation": "one_to_one",
            "status": "PASS",
        },
        "stored_evidence_validation": {
            "arm_a_metrics": "PASS",
            "arm_b_metrics": "PASS",
            "paired_csv": "PASS (validated against recomputed transitions)",
        },
        "arm_a": {
            "run_dir": str(ARM_A_RUN.relative_to(ML_ROOT)),
            "oof_rows": len(aligned),
            "overall": overall_a,
            "per_class": per_class_a,
            "confusion_matrix": {
                "labels": LABEL_ORDER,
                "matrix": confusion_matrix(y_true, y_pred_a, labels=LABEL_ORDER).tolist(),
            },
        },
        "arm_b": {
            "run_dir": str(ARM_B_RUN.relative_to(ML_ROOT)),
            "oof_rows": len(aligned),
            "overall": overall_b,
            "per_class": per_class_b,
            "confusion_matrix": {
                "labels": LABEL_ORDER,
                "matrix": confusion_matrix(y_true, y_pred_b, labels=LABEL_ORDER).tolist(),
            },
            "per_fold_training": read_per_fold_training(),
        },
        "deltas_b_minus_a": deltas,
        "per_class_deltas": per_class_deltas,
        "paired_comparison": {
            **paired_summary,
            "helped_records": helped_rows[record_columns].to_dict(orient="records"),
            "hurt_records": hurt_rows[record_columns].to_dict(orient="records"),
            "revenue_clarification": {
                "helped_revenue_records": revenue_helped,
                "tp_arm_a": revenue_a,
                "tp_arm_b": revenue_b,
                "correct_gain": revenue_b - revenue_a,
            },
        },
        "known_limitations": [
            "Development CV results; frozen folds replayed; not held-out test performance.",
            "Blank Related_Baseline_IDs do not establish semantic independence.",
            "class_weight='balanced' interaction with the augmented pool is untested.",
            "Vocabulary-shift effects from synthetic phrasing are untested.",
            "Researcher review is not independent professional governance validation.",
            "Closed-set four-class classifier; no validated out-of-scope detector.",
        ],
        "result_summary": (
            f"Arm B produced worse overall development CV metrics. Accuracy: "
            f"{overall_b['Accuracy']:.4f} vs {overall_a['Accuracy']:.4f}; Macro-F1: "
            f"{overall_b['Macro_F1']:.4f} vs {overall_a['Macro_F1']:.4f}; net change: "
            f"{paired_summary['net_gain']} ({paired_summary['helped']} helped, "
            f"{paired_summary['hurt']} hurt)."
        ),
        "pending_families_note": (
            "Approval must be based on review criteria, not on an attempt to improve metrics."
        ),
    }


def main() -> None:
    if OUTPUT_PATH.exists():
        raise FileExistsError(f"Corrected report already exists; refusing to overwrite: '{OUTPUT_PATH}'")
    report = build_report()
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")

    arm_a = report["arm_a"]["overall"]
    arm_b = report["arm_b"]["overall"]
    paired = report["paired_comparison"]
    revenue = paired["revenue_clarification"]
    training = report["arm_b"]["per_fold_training"]
    baseline_counts = [training[f"fold_{fold}"]["baseline_train"] for fold in range(1, 6)]
    print(f"Report written: {OUTPUT_PATH}")
    print(f"Arm A: accuracy={arm_a['Accuracy']:.4f}, macro-F1={arm_a['Macro_F1']:.4f}")
    print(f"Arm B: accuracy={arm_b['Accuracy']:.4f}, macro-F1={arm_b['Macro_F1']:.4f}")
    print(f"Helped={paired['helped']}, hurt={paired['hurt']}, net={paired['net_gain']}")
    print(
        "Revenue: "
        f"correct-count gain={revenue['correct_gain']}, helped records={revenue['helped_revenue_records']}"
    )
    print(f"Baseline training count range: {min(baseline_counts)}..{max(baseline_counts)}")


if __name__ == "__main__":
    main()
