"""
Offline sensitivity analysis: original simulated environmental features vs
adapter-style Unknown replacement on the Colombo held-out split.

Does not retrain, does not tune on held-out data, does not activate the model.
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

import joblib
import numpy as np
import pandas as pd
from sklearn.metrics import accuracy_score, f1_score

_TOOLS_DIR = Path(__file__).resolve().parent / "tools"
if str(_TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(_TOOLS_DIR))

from prepare_osm_motor_roads import resolve_repository_root  # noqa: E402

# Keep in sync with train_colombo_osm.py
NUMERIC_FEATURES = ["area_hectares", "distance_to_road_m", "distance_to_water_m"]
CATEGORICAL_FEATURES = [
    "requested_purpose",
    "land_category",
    "gis_enrichment_status",
    "derived_soil_group",
    "environmental_restriction_type",
    "environmental_restriction_severity",
    "spatial_constraint_present",
]
TARGET = "suitability_label"


def main(argv: list[str] | None = None) -> int:
    root = resolve_repository_root()
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--data",
        type=Path,
        default=root / "data/gis/experiments/colombo/parcels_colombo_osm_v2.csv",
    )
    parser.add_argument(
        "--model-dir",
        type=Path,
        default=root / "src/Modules/LandIntelligence/ML/output_colombo_osm",
    )
    parser.add_argument(
        "--out",
        type=Path,
        default=root
        / "data/gis/experiments/colombo/colombo_env_unknown_sensitivity.json",
    )
    args = parser.parse_args(argv)

    data_path = args.data if args.data.is_absolute() else root / args.data
    model_dir = args.model_dir if args.model_dir.is_absolute() else root / args.model_dir
    out_path = args.out if args.out.is_absolute() else root / args.out

    schema_path = model_dir / "feature_schema.json"
    splits_path = model_dir / "split_assignments.csv"
    model_path = model_dir / "random_forest_pipeline.joblib"

    for required in (data_path, schema_path, splits_path, model_path):
        if not required.is_file():
            print(f"ERROR: missing {required}", file=sys.stderr)
            return 1

    schema = json.loads(schema_path.read_text(encoding="utf-8"))
    df = pd.read_csv(data_path)
    splits = pd.read_csv(splits_path)
    heldout_ids = set(splits.loc[splits["split"] == "test", "parcel_id"].astype(str))
    heldout = df[df["parcel_id"].astype(str).isin(heldout_ids)].copy()
    if heldout.empty:
        # fallback if column named differently
        if "split" in splits.columns and "parcel_id" not in splits.columns:
            print("ERROR: split_assignments.csv missing parcel_id", file=sys.stderr)
            return 1
        print("ERROR: held-out join produced empty frame", file=sys.stderr)
        return 1

    pipeline = joblib.load(model_path)

    def prepare(frame: pd.DataFrame) -> pd.DataFrame:
        x = frame[NUMERIC_FEATURES + CATEGORICAL_FEATURES].copy()
        x["spatial_constraint_present"] = x["spatial_constraint_present"].astype(str)
        return x

    x_orig = prepare(heldout)
    y_true = heldout[TARGET].astype(str)
    pred_orig = pipeline.predict(x_orig)

    x_adapter = x_orig.copy()
    # Match ExperimentalColomboMlFeatureAdapter: do not fabricate env restriction categories.
    x_adapter["environmental_restriction_type"] = "Unknown"
    x_adapter["environmental_restriction_severity"] = "Unknown"
    # Nullable spatial constraint when unavailable would become NaN then imputer→Unknown;
    # assessed False/True stay as strings. Probe both.
    pred_adapter = pipeline.predict(x_adapter)

    x_null_spatial = x_adapter.copy()
    x_null_spatial["spatial_constraint_present"] = np.nan
    pred_null_spatial = pipeline.predict(x_null_spatial)

    changed_env = int((pred_orig != pred_adapter).sum())
    changed_null = int((pred_orig != pred_null_spatial).sum())

    report = {
        "heldout_rows": int(len(heldout)),
        "feature_schema_numeric": schema.get("numeric_features"),
        "feature_schema_categorical": schema.get("categorical_features"),
        "preprocessing_note": schema.get("preprocessing"),
        "spatial_constraint_present_encoding": {
            "training": "cast to string True/False before OneHotEncoder",
            "null_handling": "SimpleImputer(constant=Unknown) then OHE handle_unknown=ignore",
            "expanded_categories_seen_in_schema": [
                name
                for name in schema.get("expanded_feature_names_after_fit", [])
                if "spatial_constraint_present" in name
            ],
            "unknown_category_supported": any(
                name.endswith("_Unknown")
                for name in schema.get("expanded_feature_names_after_fit", [])
                if "spatial_constraint_present" in name
            ),
            "note": (
                "Training did not observe spatial_constraint_present=Unknown; "
                "null/Unknown at serve time relies on imputer+handle_unknown=ignore."
            ),
        },
        "environmental_unknown_sensitivity": {
            "predictions_changed_vs_original": changed_env,
            "predictions_changed_fraction": changed_env / max(len(heldout), 1),
            "macro_f1_original_vs_simulated_labels": float(
                f1_score(y_true, pred_orig, average="macro")
            ),
            "macro_f1_adapter_unknown_env_vs_simulated_labels": float(
                f1_score(y_true, pred_adapter, average="macro")
            ),
            "accuracy_original": float(accuracy_score(y_true, pred_orig)),
            "accuracy_adapter_unknown_env": float(accuracy_score(y_true, pred_adapter)),
            "disclaimer": (
                "Sensitivity against simulated labels only — not real-world validation. "
                "No held-out tuning or retraining was performed."
            ),
        },
        "null_spatial_constraint_sensitivity": {
            "predictions_changed_vs_original": changed_null,
            "macro_f1_vs_simulated_labels": float(
                f1_score(y_true, pred_null_spatial, average="macro")
            ),
        },
        "unsupported_or_mismatch_notes": [
            "Adapter emits environmental_restriction_*=Unknown; training used simulated typed values.",
            "Adapter may leave spatial_constraint_present null when environmental assessment is unavailable; "
            "training used boolean True/False only for assessed samples.",
            "Live HttpMlSuitabilityClient still differs (Railway eligible; Complete/Partial statuses).",
            "ModelActivationEnabled remains false; ml_service.py / live joblib untouched.",
        ],
        "paths": {
            "data": str(data_path.resolve()),
            "model": str(model_path.resolve()),
            "schema": str(schema_path.resolve()),
            "report": str(out_path.resolve()),
        },
    }

    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report["environmental_unknown_sensitivity"], indent=2))
    print(f"Wrote {out_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
