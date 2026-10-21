"""Evaluate the frozen Colombo 400 models on a new expert-reviewed parcel set.

Input CSV must contain the exact model feature columns, Cadastral Number, and
Suitability Label. Every cadastral number must be outside the training 400.
This script never retrains or modifies the saved models.
"""

import argparse
import csv
import json
from pathlib import Path

import pandas as pd
from sklearn.metrics import accuracy_score, balanced_accuracy_score, f1_score

from colombo400_ml_service import FEATURES, FEATURE_COLUMNS, STAGE_A, STAGE_B


LABELS = {"Suitable", "Moderately Suitable", "Unsuitable", "Insufficient Evidence"}
PURPOSES = {"Agricultural", "Residential", "Commercial"}


def evaluate(input_path: Path) -> dict:
    with input_path.open(encoding="utf-8-sig", newline="") as source:
        reader = csv.DictReader(source)
        required = {"Cadastral Number", "Suitability Label", *FEATURE_COLUMNS}
        if reader.fieldnames is None or len(reader.fieldnames) != len(set(reader.fieldnames)):
            raise ValueError("CSV must have one unique header per field")
        if not required.issubset(reader.fieldnames):
            raise ValueError(f"Missing columns: {sorted(required - set(reader.fieldnames))}")
        rows = list(reader)
    if not rows:
        raise ValueError("External validation requires at least one labelled row")
    keys = [(row["Cadastral Number"].strip(), row["Intended Purpose"].strip()) for row in rows]
    if len(keys) != len(set(keys)):
        raise ValueError("Duplicate cadastral number and purpose")
    if any(not cadastral or cadastral in FEATURES for cadastral, _ in keys):
        raise ValueError("The external set must contain only new, non-empty cadastral numbers")
    if any(purpose not in PURPOSES for _, purpose in keys):
        raise ValueError("Unsupported intended purpose")
    if any(row["Suitability Label"].strip() not in LABELS for row in rows):
        raise ValueError("Every row needs one of the four approved expert labels")

    frame = pd.DataFrame(rows)
    frame["Area (ha)"] = pd.to_numeric(frame["Area (ha)"], errors="raise")
    frame["primary_cover_pct"] = pd.to_numeric(frame["primary_cover_pct"], errors="raise")
    if (frame["Area (ha)"] <= 0).any() or frame["primary_cover_pct"].lt(0).any() or frame["primary_cover_pct"].gt(100).any():
        raise ValueError("Area must be positive and cover percentage must be 0–100")
    overlap = frame["overlap_available"].astype(str).str.strip().str.lower()
    if not overlap.isin(["0", "1", "false", "true"]).all():
        raise ValueError("overlap_available must be 0/1 or false/true")
    frame["overlap_available"] = overlap.isin(["1", "true"]).astype(int)

    x = frame[FEATURE_COLUMNS]
    truth = frame["Suitability Label"].str.strip().tolist()
    stage_a_truth = ["Insufficient Evidence" if label == "Insufficient Evidence" else "Sufficient Evidence" for label in truth]
    stage_a_prediction = [str(value) for value in STAGE_A.predict(x)]
    final_prediction = ["Insufficient Evidence"] * len(rows)
    sufficient_positions = [i for i, label in enumerate(stage_a_prediction) if label == "Sufficient Evidence"]
    if sufficient_positions:
        stage_b_prediction = STAGE_B.predict(x.iloc[sufficient_positions])
        for index, label in zip(sufficient_positions, stage_b_prediction):
            final_prediction[index] = str(label)
    commercial_violations = sum(
        row["Intended Purpose"] == "Commercial" and prediction == "Suitable"
        for row, prediction in zip(rows, final_prediction)
    )
    return {
        "parcels": len({cadastral for cadastral, _ in keys}),
        "assessment_rows": len(rows),
        "stage_a_accuracy": accuracy_score(stage_a_truth, stage_a_prediction),
        "stage_a_macro_f1": f1_score(stage_a_truth, stage_a_prediction, labels=["Insufficient Evidence", "Sufficient Evidence"], average="macro", zero_division=0),
        "stage_a_false_sufficiency": sum(actual == "Insufficient Evidence" and predicted == "Sufficient Evidence" for actual, predicted in zip(stage_a_truth, stage_a_prediction)),
        "end_to_end_accuracy": accuracy_score(truth, final_prediction),
        "end_to_end_balanced_accuracy": balanced_accuracy_score(truth, final_prediction),
        "end_to_end_macro_f1": f1_score(truth, final_prediction, labels=sorted(LABELS), average="macro", zero_division=0),
        "commercial_suitable_violations": commercial_violations,
        "per_purpose": {
            purpose: {
                "n": sum(row["Intended Purpose"] == purpose for row in rows),
                "accuracy": accuracy_score(
                    [truth[i] for i, row in enumerate(rows) if row["Intended Purpose"] == purpose],
                    [final_prediction[i] for i, row in enumerate(rows) if row["Intended Purpose"] == purpose],
                ),
            }
            for purpose in sorted(PURPOSES)
            if any(row["Intended Purpose"] == purpose for row in rows)
        },
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input_csv", type=Path)
    parser.add_argument("--report", type=Path, required=True)
    args = parser.parse_args()
    report = evaluate(args.input_csv)
    args.report.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2))
