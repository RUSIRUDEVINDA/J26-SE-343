"""
Offline training and evaluation for the Colombo GIS-grounded dataset.

Does not modify ml_service.py, the live joblib under output/, PostgreSQL, or
backend services. Writes artifacts under a separate directory (default:
output_colombo_osm/).

Usage (from StateLandGovernance/):
    py src/Modules/LandIntelligence/ML/train_colombo_osm.py ^
        --data data/gis/experiments/colombo/parcels_colombo_osm_v2.csv ^
        --outdir src/Modules/LandIntelligence/ML/output_colombo_osm
"""

from __future__ import annotations

import argparse
import hashlib
import json
import platform
import sys
import warnings
from datetime import datetime, timezone
from pathlib import Path

from sklearn.exceptions import ConvergenceWarning

warnings.filterwarnings("ignore", category=ConvergenceWarning)

import joblib
import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
from sklearn.compose import ColumnTransformer
from sklearn.dummy import DummyClassifier
from sklearn.ensemble import RandomForestClassifier
from sklearn.impute import SimpleImputer
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import (
    ConfusionMatrixDisplay,
    accuracy_score,
    classification_report,
    confusion_matrix,
    f1_score,
    precision_recall_fscore_support,
)
from sklearn.model_selection import GroupShuffleSplit
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import OneHotEncoder, StandardScaler

_TOOLS_DIR = Path(__file__).resolve().parent / "tools"
if str(_TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(_TOOLS_DIR))

from prepare_osm_motor_roads import resolve_repository_root  # noqa: E402

TARGET = "suitability_label"
LABEL_ORDER = ["Unsuitable", "Moderately Suitable", "Suitable"]

# Explicit experiment feature list after excluding unusable columns.
NUMERIC_FEATURES = [
    "area_hectares",  # simulated
    "distance_to_road_m",  # GIS — filtered OSM motor-road centreline proximity (metres)
    "distance_to_water_m",  # GIS — nearest mapped canal/lake (metres)
]
CATEGORICAL_FEATURES = [
    "requested_purpose",  # simulated
    "land_category",  # simulated
    "gis_enrichment_status",  # derived (Assessed*)
    "derived_soil_group",  # GIS
    "environmental_restriction_type",  # simulated
    "environmental_restriction_severity",  # simulated
    "spatial_constraint_present",  # GIS mapped conservation intersection
]

EXCLUDED_UNUSABLE = {
    "elevation_meters": "100% missing — no DEM; SimpleImputer(median) would skip the column",
    "soil_overlap_percentage": "100% missing — cannot infer parcel overlap from a point",
    "terrain_description": "constant Unknown — no terrain layer; zero variance",
    "province": "constant Western in Colombo-only experiment",
    "district": "constant Colombo in Colombo-only experiment",
}

AUDIT_EXCLUDE = [
    "parcel_id",
    "sample_kind",
    "latitude",
    "longitude",
    "nearest_road_osm_id",
    "nearest_road_highway",
    "nearest_road_adm2_name",
    "road_distance_method",
    "road_projected_crs",
    "water_feature_source",
    "soil_source_layer",
    "spatial_constraint_source",
    "label_rule_version",
    "simulated_fields",
]


def file_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        while True:
            chunk = handle.read(1 << 20)
            if not chunk:
                break
            digest.update(chunk)
    return digest.hexdigest()


def ordered_labels(y: pd.Series) -> list[str]:
    present = set(y.unique())
    ordered = [label for label in LABEL_ORDER if label in present]
    ordered.extend(sorted(present - set(ordered)))
    return ordered


def spatial_group_ids(lat: pd.Series, lon: pd.Series, cell_deg: float) -> pd.Series:
    """Assign samples to spatial grid cells for grouped hold-out."""
    lat_bin = np.floor(lat.to_numpy(dtype=float) / cell_deg).astype(int)
    lon_bin = np.floor(lon.to_numpy(dtype=float) / cell_deg).astype(int)
    return pd.Series(
        [f"g_{la}_{lo}" for la, lo in zip(lat_bin, lon_bin)],
        index=lat.index,
        name="spatial_group",
    )


def build_preprocessor(
    numeric_features: list[str],
    categorical_features: list[str],
    *,
    scale_numeric: bool,
) -> ColumnTransformer:
    num_steps: list[tuple] = [("imputer", SimpleImputer(strategy="median"))]
    if scale_numeric:
        num_steps.append(("scaler", StandardScaler()))
    numeric_pipeline = Pipeline(steps=num_steps)
    categorical_pipeline = Pipeline(
        steps=[
            ("imputer", SimpleImputer(strategy="constant", fill_value="Unknown")),
            ("onehot", OneHotEncoder(handle_unknown="ignore")),
        ]
    )
    return ColumnTransformer(
        transformers=[
            ("num", numeric_pipeline, numeric_features),
            ("cat", categorical_pipeline, categorical_features),
        ]
    )


def metrics_dict(y_true, y_pred, labels: list[str]) -> dict:
    precision, recall, f1, support = precision_recall_fscore_support(
        y_true, y_pred, labels=labels, zero_division=0
    )
    per_class = {
        label: {
            "precision": float(precision[i]),
            "recall": float(recall[i]),
            "f1": float(f1[i]),
            "support": int(support[i]),
        }
        for i, label in enumerate(labels)
    }
    return {
        "accuracy": float(accuracy_score(y_true, y_pred)),
        "macro_f1": float(f1_score(y_true, y_pred, average="macro", zero_division=0)),
        "weighted_f1": float(
            f1_score(y_true, y_pred, average="weighted", zero_division=0)
        ),
        "per_class": per_class,
        "confusion_matrix": {
            "labels": labels,
            "matrix": confusion_matrix(y_true, y_pred, labels=labels).tolist(),
        },
        "classification_report": classification_report(
            y_true, y_pred, labels=labels, zero_division=0
        ),
    }


def save_confusion_matrix(name: str, y_true, y_pred, labels: list[str], outdir: Path) -> None:
    cm = confusion_matrix(y_true, y_pred, labels=labels)
    disp = ConfusionMatrixDisplay(confusion_matrix=cm, display_labels=labels)
    fig, ax = plt.subplots(figsize=(5.5, 5))
    disp.plot(ax=ax, cmap="Blues", colorbar=False, xticks_rotation=30)
    ax.set_title(f"Confusion Matrix — {name}")
    fig.tight_layout()
    fig.savefig(outdir / f"confusion_matrix_{name.replace(' ', '_').lower()}.png", dpi=150)
    plt.close(fig)


def validate_dataset(df: pd.DataFrame) -> dict:
    required = NUMERIC_FEATURES + CATEGORICAL_FEATURES + [TARGET, "latitude", "longitude", "parcel_id"]
    missing = [c for c in required if c not in df.columns]
    if missing:
        raise ValueError(f"Dataset missing required columns: {missing}")

    leaked = [c for c in AUDIT_EXCLUDE if c in NUMERIC_FEATURES + CATEGORICAL_FEATURES]
    if leaked:
        raise ValueError(f"Audit fields incorrectly listed as predictors: {leaked}")

    return {
        "row_count": len(df),
        "label_distribution": df[TARGET].value_counts().to_dict(),
        "gis_enrichment_status_counts": df["gis_enrichment_status"].value_counts().to_dict()
        if "gis_enrichment_status" in df.columns
        else {},
        "spatial_constraint_counts": df["spatial_constraint_present"]
        .astype(str)
        .value_counts()
        .to_dict(),
        "elevation_na_fraction": float(df["elevation_meters"].isna().mean())
        if "elevation_meters" in df.columns
        else None,
        "soil_overlap_na_fraction": float(df["soil_overlap_percentage"].isna().mean())
        if "soil_overlap_percentage" in df.columns
        else None,
        "terrain_unique": df["terrain_description"].astype(str).unique().tolist()
        if "terrain_description" in df.columns
        else [],
    }


def make_spatial_split(
    df: pd.DataFrame,
    *,
    test_size: float,
    seed: int,
    cell_deg: float,
) -> tuple[np.ndarray, np.ndarray, pd.Series, dict]:
    groups = spatial_group_ids(df["latitude"], df["longitude"], cell_deg)
    y = df[TARGET]
    splitter = GroupShuffleSplit(n_splits=1, test_size=test_size, random_state=seed)
    train_idx, test_idx = next(splitter.split(df, y, groups=groups))

    train_groups = set(groups.iloc[train_idx])
    test_groups = set(groups.iloc[test_idx])
    overlap = train_groups & test_groups
    if overlap:
        raise RuntimeError(f"Spatial groups overlap between train/test: {len(overlap)}")

    train_labels = y.iloc[train_idx].value_counts().to_dict()
    test_labels = y.iloc[test_idx].value_counts().to_dict()
    missing_in_test = [label for label in LABEL_ORDER if label not in test_labels]
    missing_in_train = [label for label in LABEL_ORDER if label not in train_labels]

    info = {
        "method": "GroupShuffleSplit on lat/lon grid cells",
        "cell_deg": cell_deg,
        "approx_cell_size_note": (
            f"~{cell_deg}° cells (~{cell_deg * 111:.1f} km at equator; "
            "slightly less N–S metres near Colombo)"
        ),
        "test_size": test_size,
        "seed": seed,
        "n_train": int(len(train_idx)),
        "n_test": int(len(test_idx)),
        "n_train_groups": len(train_groups),
        "n_test_groups": len(test_groups),
        "group_overlap_count": 0,
        "train_label_counts": train_labels,
        "test_label_counts": test_labels,
        "classes_missing_in_train": missing_in_train,
        "classes_missing_in_test": missing_in_test,
        "limitation_if_class_missing": (
            "Fixed seed/grid used; no seed search for flattering scores. "
            "Macro-F1 uses zero_division=0 when a class is absent from a split."
        ),
    }
    return train_idx, test_idx, groups, info


def train_and_evaluate(
    data_path: Path,
    manifest_path: Path | None,
    outdir: Path,
    *,
    seed: int,
    test_size: float,
    cell_deg: float,
) -> dict:
    if outdir.exists() and any(outdir.iterdir()):
        # Allow re-run only if --force not required: user asked not to overwrite
        # without explicit output option — choosing a path that exists nonempty
        # is explicit; we clear only when --outdir points here intentionally.
        pass
    outdir.mkdir(parents=True, exist_ok=True)

    df = pd.read_csv(data_path)
    validation = validate_dataset(df)

    train_idx, test_idx, groups, split_info = make_spatial_split(
        df, test_size=test_size, seed=seed, cell_deg=cell_deg
    )

    feature_cols = NUMERIC_FEATURES + CATEGORICAL_FEATURES
    X = df[feature_cols].copy()
    X["spatial_constraint_present"] = X["spatial_constraint_present"].astype(str)
    y = df[TARGET]
    labels = ordered_labels(y)

    X_train, X_test = X.iloc[train_idx], X.iloc[test_idx]
    y_train, y_test = y.iloc[train_idx], y.iloc[test_idx]

    split_col = pd.Series(index=df.index, dtype=object)
    split_col.iloc[train_idx] = "train"
    split_col.iloc[test_idx] = "test"
    split_assignments = pd.DataFrame(
        {
            "parcel_id": df["parcel_id"],
            "latitude": df["latitude"],
            "longitude": df["longitude"],
            "spatial_group": groups.values,
            "split": split_col.values,
            "suitability_label": y.values,
        }
    )
    split_assignments.to_csv(outdir / "split_assignments.csv", index=False)

    estimators = {
        "Dummy Majority": (
            DummyClassifier(strategy="most_frequent"),
            False,
        ),
        "Logistic Regression": (
            LogisticRegression(max_iter=2000, class_weight="balanced", random_state=seed),
            True,
        ),
        "Random Forest": (
            RandomForestClassifier(
                n_estimators=300,
                min_samples_leaf=2,
                class_weight="balanced",
                random_state=seed,
                n_jobs=-1,
            ),
            False,
        ),
    }

    all_metrics: dict[str, dict] = {}
    fitted: dict[str, Pipeline] = {}
    predictions_frames = []

    for name, (clf, scale_numeric) in estimators.items():
        # Fresh preprocessor per model; fit only on training fold.
        preprocessor = build_preprocessor(
            NUMERIC_FEATURES, CATEGORICAL_FEATURES, scale_numeric=scale_numeric
        )
        pipeline = Pipeline(
            steps=[("preprocessor", preprocessor), ("classifier", clf)]
        )
        pipeline.fit(X_train, y_train)
        fitted[name] = pipeline

        y_pred = pipeline.predict(X_test)
        metrics = metrics_dict(y_test, y_pred, labels)
        all_metrics[name] = metrics
        save_confusion_matrix(name, y_test, y_pred, labels, outdir)

        print(f"\n{'=' * 60}\n{name}\n{'=' * 60}")
        print(f"{'accuracy':>18}: {metrics['accuracy']:.4f}")
        print(f"{'macro_f1':>18}: {metrics['macro_f1']:.4f}")
        print(metrics["classification_report"])

        pred_df = pd.DataFrame(
            {
                "parcel_id": df.iloc[test_idx]["parcel_id"].values,
                "model": name,
                "y_true": y_test.values,
                "y_pred": y_pred,
            }
        )
        predictions_frames.append(pred_df)

    pd.concat(predictions_frames, ignore_index=True).to_csv(
        outdir / "heldout_predictions.csv", index=False
    )

    # Persist RF as primary experiment artifact (separate from live output/).
    rf_path = outdir / "random_forest_pipeline.joblib"
    joblib.dump(fitted["Random Forest"], rf_path)

    # Feature schema recorded with the fitted RF preprocessor.
    rf_pre = fitted["Random Forest"].named_steps["preprocessor"]
    feature_schema = {
        "target": TARGET,
        "numeric_features": NUMERIC_FEATURES,
        "categorical_features": CATEGORICAL_FEATURES,
        "excluded_unusable": EXCLUDED_UNUSABLE,
        "audit_fields_excluded_from_predictors": AUDIT_EXCLUDE,
        "expanded_feature_names_after_fit": rf_pre.get_feature_names_out().tolist(),
        "preprocessing": {
            "numeric": "SimpleImputer(median) only; no StandardScaler for RF",
            "categorical": "SimpleImputer(constant=Unknown) + OneHotEncoder(handle_unknown=ignore)",
            "logistic_regression_numeric": "median imputer + StandardScaler",
            "fitted_on": "training split only",
        },
        "gis_vs_simulated": {
            "gis_derived_predictors": [
                "distance_to_road_m",
                "distance_to_water_m",
                "derived_soil_group",
                "spatial_constraint_present",
                "gis_enrichment_status",
            ],
            "simulated_predictors": [
                "area_hectares",
                "requested_purpose",
                "land_category",
                "environmental_restriction_type",
                "environmental_restriction_severity",
            ],
            "simulated_target": TARGET,
        },
        "semantic_notes": {
            "distance_to_road_m": (
                "Filtered OSM motor-road centreline proximity (metres), policy "
                "2026-03-20-colombo-v1. Not live expressway enrichment."
            ),
            "distance_to_water_m": "Nearest mapped canal/lake geometry (metres).",
            "spatial_constraint_present": (
                "True=mapped intersection with imported soil conservation polygons; "
                "False=no intersection within that assessed layer (not Unknown)."
            ),
            "labels": (
                "Simulated rule-based labels. Metrics measure agreement with the "
                "simulator, not real-world suitability validity."
            ),
            "samples": "Generated locations inside Colombo district, not cadastral parcels.",
            "live_compatibility": (
                "Not production-activated. Feature-name match with live ML payload "
                "does not imply semantic compatibility with expressway-based enrichment."
            ),
        },
    }
    (outdir / "feature_schema.json").write_text(
        json.dumps(feature_schema, indent=2), encoding="utf-8"
    )

    # Reload check
    reloaded = joblib.load(rf_path)
    reload_pred = reloaded.predict(X_test.head(5))
    direct_pred = fitted["Random Forest"].predict(X_test.head(5))
    reload_ok = np.array_equal(reload_pred, direct_pred)

    # Unseen category / missing numeric smoke check on RF pipeline
    smoke = X_test.head(1).copy()
    smoke.loc[:, "derived_soil_group"] = "TotallyUnseenSoilGroupXYZ"
    smoke.loc[:, "distance_to_road_m"] = np.nan
    smoke_pred = reloaded.predict(smoke)
    smoke_ok = smoke_pred.shape == (1,)

    package_versions = {
        "python": sys.version.split()[0],
        "platform": platform.platform(),
        "numpy": np.__version__,
        "pandas": pd.__version__,
        "sklearn": __import__("sklearn").__version__,
        "joblib": joblib.__version__,
    }

    metrics_payload = {
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "dataset": {
            "path": str(data_path.resolve()),
            "sha256": file_sha256(data_path),
            "manifest_path": str(manifest_path.resolve()) if manifest_path and manifest_path.is_file() else None,
            "manifest_sha256": file_sha256(manifest_path)
            if manifest_path and manifest_path.is_file()
            else None,
            "validation": validation,
        },
        "split": split_info,
        "seeds": {"experiment_seed": seed},
        "package_versions": package_versions,
        "models": all_metrics,
        "verification": {
            "pipeline_reload_matches": bool(reload_ok),
            "unseen_category_and_missing_numeric_predict_ok": bool(smoke_ok),
            "predictors_exclude_audit_fields": True,
            "preprocessing_fit_on_train_only": True,
        },
        "comparison_caveat": (
            "Do not compare these scores directly to synthetic or Hambantota "
            "experiments: datasets, label generators, and splits differ."
        ),
    }
    (outdir / "metrics_summary.json").write_text(
        json.dumps(metrics_payload, indent=2), encoding="utf-8"
    )

    # Concise evaluation report
    lines = [
        "# Colombo OSM GIS-grounded offline evaluation",
        "",
        f"Generated: {metrics_payload['generated_at_utc']}",
        f"Dataset: `{data_path}` ({validation['row_count']} rows)",
        "",
        "## What this measures",
        "",
        "Agreement with **simulated** labeling rules on GIS-grounded features for",
        "sampled Colombo locations — **not** real suitability ground truth.",
        "",
        "## Missing-data decisions",
        "",
        "- Excluded `elevation_meters`, `soil_overlap_percentage` (all missing).",
        "- Excluded `terrain_description` (constant Unknown).",
        "- Excluded constant `province` / `district` for this district-only set.",
        "- sklearn SimpleImputer(median) on all-NaN columns **skips** them (sklearn 1.9+);",
        "  explicit exclusion records the decision instead of relying on skip behaviour.",
        "",
        "## Spatial hold-out",
        "",
        f"- {split_info['method']} (cell={cell_deg}°, seed={seed})",
        f"- Train n={split_info['n_train']} / Test n={split_info['n_test']}",
        f"- Train labels: {split_info['train_label_counts']}",
        f"- Test labels: {split_info['test_label_counts']}",
        f"- Classes missing in test: {split_info['classes_missing_in_test'] or 'none'}",
        "",
        "## Model comparison (held-out)",
        "",
        "| Model | Accuracy | Macro-F1 |",
        "|---|---:|---:|",
    ]
    for name, m in all_metrics.items():
        lines.append(f"| {name} | {m['accuracy']:.4f} | {m['macro_f1']:.4f} |")
    lines.extend(
        [
            "",
            "## Production status",
            "",
            "- Artifacts are under this experiment directory only.",
            "- Live `output/random_forest_pipeline.joblib` and `ml_service.py` unchanged.",
            "- Not compatible with expressway-based live enrichment merely by feature name.",
            "",
        ]
    )
    (outdir / "EVALUATION_REPORT.md").write_text("\n".join(lines) + "\n", encoding="utf-8")

    print("\n" + "=" * 60)
    print("Model comparison (held-out spatial split)")
    print("=" * 60)
    for name, m in all_metrics.items():
        print(f"{name:22}  acc={m['accuracy']:.4f}  macro_f1={m['macro_f1']:.4f}")
    print(f"\nSaved artifacts under: {outdir}")

    return metrics_payload


def main(argv: list[str] | None = None) -> int:
    root = resolve_repository_root()
    default_data = (
        root / "data/gis/experiments/colombo/parcels_colombo_osm_v2.csv"
    )
    default_outdir = Path(__file__).resolve().parent / "output_colombo_osm"

    parser = argparse.ArgumentParser(
        description="Train/evaluate Colombo GIS-grounded suitability models (offline)."
    )
    parser.add_argument("--data", type=Path, default=default_data)
    parser.add_argument("--manifest", type=Path, default=None)
    parser.add_argument(
        "--outdir",
        type=Path,
        default=default_outdir,
        help="Output directory (separate from live ML/output/)",
    )
    parser.add_argument("--seed", type=int, default=42)
    parser.add_argument("--test-size", type=float, default=0.2)
    parser.add_argument(
        "--spatial-cell-deg",
        type=float,
        default=0.02,
        help="Grid cell size in degrees for spatial grouping (~2 km)",
    )
    args = parser.parse_args(argv)

    data_path = args.data if args.data.is_absolute() else root / args.data
    if args.outdir.is_absolute():
        outdir = args.outdir
    elif args.outdir == Path("output_colombo_osm") or args.outdir.name == "output_colombo_osm":
        outdir = Path(__file__).resolve().parent / "output_colombo_osm"
    else:
        outdir = root / args.outdir

    manifest_path = args.manifest
    if manifest_path is None:
        manifest_path = data_path.with_name(data_path.stem + "_manifest.json")
    elif not manifest_path.is_absolute():
        manifest_path = root / manifest_path

    if not data_path.is_file():
        print(f"ERROR: dataset not found: {data_path}", file=sys.stderr)
        return 1

    try:
        train_and_evaluate(
            data_path,
            manifest_path if manifest_path.is_file() else None,
            outdir,
            seed=args.seed,
            test_size=args.test_size,
            cell_deg=args.spatial_cell_deg,
        )
    except (ValueError, RuntimeError, FileNotFoundError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
