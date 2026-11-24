"""Re-score the archived parcel-grouped test split (frozen models, no retraining)."""

import argparse
import json
from pathlib import Path

import pandas as pd

from validate_colombo400_external import prepare_labeled_frame, score_prepared_frame

ARTIFACT_DIR = Path(__file__).with_name("colombo400_artefacts")


def evaluate_holdout_test_split() -> dict:
    features = pd.read_csv(ARTIFACT_DIR / "cleaned_features.csv")
    splits = pd.read_csv(ARTIFACT_DIR / "parcel_split_assignments.csv")
    test_parcels = set(
        splits.loc[splits["split"] == "test", "Cadastral Number"].astype(str).str.strip()
    )
    frame = features[
        features["Cadastral Number"].astype(str).str.strip().isin(test_parcels)
    ].copy()
    if frame.empty:
        raise ValueError("No test-split rows found in cleaned_features.csv")
    prepared = prepare_labeled_frame(frame.to_dict(orient="records"))
    report = score_prepared_frame(prepared, parcel_count=len(test_parcels))
    report["split"] = "test"
    report["note"] = (
        "Parcel-grouped hold-out from Colombo 400 expert review (seed=42). "
        "Same split as evaluation_report.txt; not independent external validation."
    )
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--report",
        type=Path,
        help="Optional path to write JSON report (also printed to stdout)",
    )
    args = parser.parse_args()
    report = evaluate_holdout_test_split()
    payload = json.dumps(report, indent=2) + "\n"
    print(payload, end="")
    if args.report:
        args.report.write_text(payload, encoding="utf-8")
