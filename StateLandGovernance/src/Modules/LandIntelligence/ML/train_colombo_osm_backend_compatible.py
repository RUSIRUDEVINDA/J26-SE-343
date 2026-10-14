"""
Train a backend-compatible Colombo experimental candidate.

Excludes simulated environmental_restriction_type/severity because the adapter
cannot supply equivalent GIS-derived values. Preserves output_colombo_osm/.

Model selection uses training-only GroupKFold CV (preprocessing fitted inside
each fold). The prior held-out split is scored only as exploratory.

Does not modify ml_service.py, live output/, or activate any model.
Does not change suitability labels.
"""

from __future__ import annotations

import argparse
import json
import sys
import warnings
from datetime import datetime, timezone
from pathlib import Path

from sklearn.exceptions import ConvergenceWarning

warnings.filterwarnings("ignore", category=ConvergenceWarning)

import joblib
import numpy as np
import pandas as pd
from sklearn.dummy import DummyClassifier
from sklearn.ensemble import RandomForestClassifier
from sklearn.linear_model import LogisticRegression
from sklearn.model_selection import GroupKFold, cross_validate
from sklearn.pipeline import Pipeline

_ML_DIR = Path(__file__).resolve().parent
if str(_ML_DIR) not in sys.path:
    sys.path.insert(0, str(_ML_DIR))

_TOOLS_DIR = _ML_DIR / "tools"
if str(_TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(_TOOLS_DIR))

import train_colombo_osm as base  # noqa: E402
from prepare_osm_motor_roads import resolve_repository_root  # noqa: E402

TARGET = base.TARGET
LABEL_ORDER = base.LABEL_ORDER

NUMERIC_FEATURES = [
    "area_hectares",
    "distance_to_road_m",
    "distance_to_water_m",
]
CATEGORICAL_FEATURES = [
    "requested_purpose",
    "land_category",
    "gis_enrichment_status",
    "derived_soil_group",
    "spatial_constraint_present",
]

EXCLUDED_FROM_PREVIOUS_CANDIDATE = {
    "environmental_restriction_type": (
        "Simulated in dataset; ExperimentalColomboMlFeatureAdapter cannot supply "
        "equivalent typed GIS values and must abstain rather than fabricate."
    ),
    "environmental_restriction_severity": (
        "Simulated in dataset; excluded with type for the same adapter reason."
    ),
}

MISSING_EVIDENCE_POLICY = {
    "spatial_constraint_present_null": (
        "Adapter leaves null when conservation assessment is unavailable. "
        "Never coerce null/Unknown to False. Backend-compatible serving must "
        "abstain from experimental prediction when SpatialConstraintPresent is null."
    ),
    "handle_unknown_ignore": (
        "OneHotEncoder(handle_unknown='ignore') is an engineering fallback only; "
        "it is not evidence of predictive validity for unseen categories."
    ),
    "labels_still_simulated": (
        "suitability_label remains rule-simulated and may have used environmental "
        "restriction signals that are no longer predictors. Never retarget labels "
        "to inflate scores. Real-world validity requires independent evaluation "
        "and real suitability labels."
    ),
}


def prepare_features(df: pd.DataFrame) -> pd.DataFrame:
    X = df[NUMERIC_FEATURES + CATEGORICAL_FEATURES].copy()
    # Explicit string categories so True/False/Unknown are first-class (no null→False).
    X["spatial_constraint_present"] = (
        X["spatial_constraint_present"]
        .map(lambda v: "Unknown" if pd.isna(v) else str(v))
        .astype(str)
    )
    return X


def run_training_cv(
    X_train: pd.DataFrame,
    y_train: pd.Series,
    groups_train: pd.Series,
    *,
    seed: int,
    n_splits: int,
) -> dict:
    labels = base.ordered_labels(y_train)
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

    cv = GroupKFold(n_splits=n_splits)
    results: dict[str, dict] = {}
    for name, (clf, scale_numeric) in estimators.items():
        preprocessor = base.build_preprocessor(
            NUMERIC_FEATURES, CATEGORICAL_FEATURES, scale_numeric=scale_numeric
        )
        pipeline = Pipeline(
            steps=[("preprocessor", preprocessor), ("classifier", clf)]
        )
        scoring = {
            "accuracy": "accuracy",
            "macro_f1": "f1_macro",
        }
        cv_out = cross_validate(
            pipeline,
            X_train,
            y_train,
            groups=groups_train,
            cv=cv,
            scoring=scoring,
            n_jobs=-1,
            return_train_score=False,
        )
        results[name] = {
            "cv_accuracy_mean": float(np.mean(cv_out["test_accuracy"])),
            "cv_accuracy_std": float(np.std(cv_out["test_accuracy"])),
            "cv_macro_f1_mean": float(np.mean(cv_out["test_macro_f1"])),
            "cv_macro_f1_std": float(np.std(cv_out["test_macro_f1"])),
            "fold_macro_f1": [float(v) for v in cv_out["test_macro_f1"]],
            "fold_accuracy": [float(v) for v in cv_out["test_accuracy"]],
            "labels_in_training_pool": labels,
        }
        print(
            f"{name}: CV macro-F1={results[name]['cv_macro_f1_mean']:.4f} "
            f"± {results[name]['cv_macro_f1_std']:.4f}"
        )

    selected = max(results.items(), key=lambda item: item[1]["cv_macro_f1_mean"])[0]
    return {"folds": n_splits, "models": results, "selected_by_cv_macro_f1": selected}


def fit_final_on_train(
    X_train: pd.DataFrame,
    y_train: pd.Series,
    *,
    seed: int,
    selected: str,
) -> Pipeline:
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
    clf, scale_numeric = estimators[selected]
    preprocessor = base.build_preprocessor(
        NUMERIC_FEATURES, CATEGORICAL_FEATURES, scale_numeric=scale_numeric
    )
    pipeline = Pipeline(steps=[("preprocessor", preprocessor), ("classifier", clf)])
    pipeline.fit(X_train, y_train)
    return pipeline


def main(argv: list[str] | None = None) -> int:
    root = resolve_repository_root()
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--data",
        type=Path,
        default=root / "data/gis/experiments/colombo/parcels_colombo_osm_v2.csv",
    )
    parser.add_argument(
        "--outdir",
        type=Path,
        default=root
        / "src/Modules/LandIntelligence/ML/output_colombo_osm_backend_compatible",
    )
    parser.add_argument("--seed", type=int, default=42)
    parser.add_argument("--test-size", type=float, default=0.2)
    parser.add_argument("--cell-deg", type=float, default=0.02)
    parser.add_argument("--cv-folds", type=int, default=5)
    args = parser.parse_args(argv)

    data_path = args.data if args.data.is_absolute() else root / args.data
    outdir = args.outdir if args.outdir.is_absolute() else root / args.outdir
    outdir.mkdir(parents=True, exist_ok=True)

    df = pd.read_csv(data_path)
    # Reuse prior spatial split definition for exploratory holdout labelling only.
    train_idx, test_idx, groups, split_info = base.make_spatial_split(
        df, test_size=args.test_size, seed=args.seed, cell_deg=args.cell_deg
    )
    split_info["heldout_role"] = (
        "EXPLORATORY ONLY — this held-out set already informed development via "
        "sensitivity analysis. Scores here are not an untouched final evaluation. "
        "A new random split of the same data does not restore independent validity."
    )

    X = prepare_features(df)
    y = df[TARGET]
    labels = base.ordered_labels(y)

    X_train, X_test = X.iloc[train_idx], X.iloc[test_idx]
    y_train, y_test = y.iloc[train_idx], y.iloc[test_idx]
    groups_train = groups.iloc[train_idx]

    cv_summary = run_training_cv(
        X_train, y_train, groups_train, seed=args.seed, n_splits=args.cv_folds
    )
    selected = cv_summary["selected_by_cv_macro_f1"]
    pipeline = fit_final_on_train(X_train, y_train, seed=args.seed, selected=selected)

    # Exploratory held-out score (not used for selection).
    exploratory_pred = pipeline.predict(X_test)
    exploratory_metrics = base.metrics_dict(y_test, exploratory_pred, labels)
    base.save_confusion_matrix(
        f"{selected}_exploratory_heldout", y_test, exploratory_pred, labels, outdir
    )

    artifact_name = (
        "random_forest_pipeline.joblib"
        if selected == "Random Forest"
        else f"{selected.lower().replace(' ', '_')}_pipeline.joblib"
    )
    joblib.dump(pipeline, outdir / artifact_name)

    # Also always persist RF fitted on train for adapter experiments if RF not selected.
    if selected != "Random Forest":
        rf = fit_final_on_train(X_train, y_train, seed=args.seed, selected="Random Forest")
        joblib.dump(rf, outdir / "random_forest_pipeline.joblib")
        serving_pipeline = rf
        serving_model_name = "Random Forest (fitted on train after CV selected different model)"
    else:
        serving_pipeline = pipeline
        serving_model_name = "Random Forest"

    pre = serving_pipeline.named_steps["preprocessor"]
    try:
        feature_names = list(pre.get_feature_names_out())
    except Exception:
        feature_names = []

    schema = {
        "candidate": "colombo_osm_backend_compatible",
        "model_activation_enabled": False,
        "target": TARGET,
        "numeric_features": NUMERIC_FEATURES,
        "categorical_features": CATEGORICAL_FEATURES,
        "excluded_simulated_environmental_predictors": EXCLUDED_FROM_PREVIOUS_CANDIDATE,
        "missing_evidence_policy": MISSING_EVIDENCE_POLICY,
        "expanded_feature_names": feature_names,
        "spatial_constraint_present_encoding": {
            "training": "cast to string; NaN -> 'Unknown' before OneHotEncoder",
            "serving": (
                "Adapter sets bool when assessment Available; null triggers abstention. "
                "Do not convert null to false."
            ),
            "categories_expected": ["True", "False", "Unknown"],
        },
        "label_limitations": MISSING_EVIDENCE_POLICY["labels_still_simulated"],
        "compatible_adapter_fields": [
            "requested_purpose",
            "land_category",
            "area_hectares",
            "distance_to_road_m",
            "distance_to_water_m",
            "gis_enrichment_status",
            "derived_soil_group",
            "spatial_constraint_present",
        ],
        "independent_evaluation_required": (
            "Real-world validity requires an independent evaluation set and real "
            "suitability labels that were not used during development."
        ),
    }
    (outdir / "feature_schema.json").write_text(
        json.dumps(schema, indent=2), encoding="utf-8"
    )

    split_flags = np.full(len(df), "test_exploratory", dtype=object)
    split_flags[train_idx] = "train"
    split_assignments = pd.DataFrame(
        {
            "parcel_id": df["parcel_id"],
            "latitude": df["latitude"],
            "longitude": df["longitude"],
            "spatial_group": groups.values,
            "split": split_flags,
            "suitability_label": y.values,
        }
    )
    split_assignments.to_csv(outdir / "split_assignments.csv", index=False)

    pd.DataFrame(
        {
            "parcel_id": df.iloc[test_idx]["parcel_id"].values,
            "y_true": y_test.values,
            "y_pred_exploratory": exploratory_pred,
            "note": "exploratory_heldout_not_used_for_selection",
        }
    ).to_csv(outdir / "exploratory_heldout_predictions.csv", index=False)

    metrics_payload = {
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "candidate": "colombo_osm_backend_compatible",
        "dataset_path": str(data_path.resolve()),
        "dataset_sha256": base.file_sha256(data_path),
        "selection": {
            "method": "GroupKFold on training spatial groups only",
            "metric": "macro_f1",
            "result": cv_summary,
        },
        "split": split_info,
        "exploratory_heldout_metrics": {
            "role": "exploratory_only",
            "serving_model": serving_model_name,
            "metrics": exploratory_metrics,
        },
        "baselines_note": (
            "Dummy Majority and Logistic Regression are reported under selection.cv; "
            "Random Forest is the intended experimental serving artifact when selected."
        ),
        "previous_artifacts_preserved": str(
            (_ML_DIR / "output_colombo_osm").resolve()
        ),
        "live_ml_untouched": True,
        "model_activation_enabled": False,
    }
    (outdir / "metrics_summary.json").write_text(
        json.dumps(metrics_payload, indent=2), encoding="utf-8"
    )

    report = f"""# Colombo OSM backend-compatible candidate

## Feature schema (exact predictors)

Numeric: {", ".join(NUMERIC_FEATURES)}

Categorical: {", ".join(CATEGORICAL_FEATURES)}

Excluded vs prior `output_colombo_osm`: environmental_restriction_type, environmental_restriction_severity.

## Missing-evidence / abstention policy

{MISSING_EVIDENCE_POLICY["spatial_constraint_present_null"]}

{MISSING_EVIDENCE_POLICY["handle_unknown_ignore"]}

## Selection (training-only CV)

Selected by mean CV macro-F1: **{selected}**

{json.dumps(cv_summary, indent=2)}

## Exploratory held-out (not for model selection)

Macro-F1={exploratory_metrics["macro_f1"]:.4f}, accuracy={exploratory_metrics["accuracy"]:.4f}

This held-out set already informed development (sensitivity analysis). These numbers are exploratory.

## Limitations

{MISSING_EVIDENCE_POLICY["labels_still_simulated"]}

{schema["independent_evaluation_required"]}

ModelActivationEnabled remains false. `ml_service.py` and live `output/` were not modified.
"""
    (outdir / "EVALUATION_REPORT.md").write_text(report, encoding="utf-8")
    print(f"Wrote artifacts to {outdir}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
