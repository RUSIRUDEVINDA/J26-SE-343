"""
Random Forest parcel-purpose suitability classifier
------------------------------------------------------
Component 1 - Intelligent Land Knowledge Graph and Spatial Recommendation Platform
J26-SE-343

Trains Random Forest against Logistic Regression, Decision Tree, and a
majority-class Dummy baseline. Reports held-out Accuracy, per-class Precision /
Recall / F1, macro/weighted F1, confusion matrix, and stratified CV on the
training fold only (no test leakage).

Trustworthy predictors only — road, water, and soil are excluded because they
are unavailable for the research dataset and must not be invented.

Usage:
    python train_random_forest.py --data parcels_dataset.csv --outdir output
"""

import argparse
import json
import os
import warnings

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
    precision_score,
    recall_score,
)
from sklearn.model_selection import StratifiedKFold, cross_val_score, train_test_split
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import OneHotEncoder
from sklearn.tree import DecisionTreeClassifier

TARGET = "suitability_label"
ID_COLS = ["parcel_id"]

# Explicitly excluded (unavailable / not inventable for research):
#   distance_to_road_m, distance_to_water_m, derived_soil_group, soil_overlap_percentage
NUMERIC_FEATURES = [
    "area_hectares",
    "elevation_meters",
]
CATEGORICAL_FEATURES = [
    "requested_purpose",
    "land_category",
    "province",
    "district",
    "gis_enrichment_status",
    "terrain_description",
    "environmental_restriction_type",
    "environmental_restriction_severity",
    "spatial_constraint_present",
]

EXCLUDED_UNAVAILABLE_FEATURES = [
    "distance_to_road_m",
    "distance_to_water_m",
    "derived_soil_group",
    "soil_overlap_percentage",
]

LABEL_ORDER = ["Unsuitable", "Moderately Suitable", "Suitable"]


def build_preprocessor() -> ColumnTransformer:
    numeric_pipeline = Pipeline(
        steps=[
            ("imputer", SimpleImputer(strategy="median")),
        ]
    )
    categorical_pipeline = Pipeline(
        steps=[
            ("imputer", SimpleImputer(strategy="constant", fill_value="Unknown")),
            ("onehot", OneHotEncoder(handle_unknown="ignore")),
        ]
    )
    return ColumnTransformer(
        transformers=[
            ("num", numeric_pipeline, NUMERIC_FEATURES),
            ("cat", categorical_pipeline, CATEGORICAL_FEATURES),
        ]
    )


def _ordered_labels(y: pd.Series) -> list[str]:
    present = set(y.unique())
    ordered = [label for label in LABEL_ORDER if label in present]
    extras = sorted(present - set(ordered))
    return ordered + extras


def evaluate_model(name, pipeline, X_test, y_test, labels, outdir):
    y_pred = pipeline.predict(X_test)

    precision_c, recall_c, f1_c, support_c = precision_recall_fscore_support(
        y_test, y_pred, labels=labels, zero_division=0
    )

    per_class = {
        label: {
            "precision": float(precision_c[i]),
            "recall": float(recall_c[i]),
            "f1": float(f1_c[i]),
            "support": int(support_c[i]),
        }
        for i, label in enumerate(labels)
    }

    metrics = {
        "accuracy": float(accuracy_score(y_test, y_pred)),
        "precision_macro": float(
            precision_score(y_test, y_pred, average="macro", zero_division=0)
        ),
        "recall_macro": float(
            recall_score(y_test, y_pred, average="macro", zero_division=0)
        ),
        "f1_macro": float(f1_score(y_test, y_pred, average="macro", zero_division=0)),
        "f1_weighted": float(
            f1_score(y_test, y_pred, average="weighted", zero_division=0)
        ),
        "per_class": per_class,
    }

    report = classification_report(y_test, y_pred, labels=labels, zero_division=0)
    cm = confusion_matrix(y_test, y_pred, labels=labels)

    print(f"\n{'=' * 60}\n{name}\n{'=' * 60}")
    for key in (
        "accuracy",
        "precision_macro",
        "recall_macro",
        "f1_macro",
        "f1_weighted",
    ):
        print(f"{key:>18}: {metrics[key]:.4f}")
    print("\nPer-class (held-out):")
    for label, row in per_class.items():
        print(
            f"  {label:22} P={row['precision']:.4f} R={row['recall']:.4f} "
            f"F1={row['f1']:.4f} n={row['support']}"
        )
    print("\nClassification report:\n", report)
    print("Confusion matrix (rows=actual, cols=predicted):\n", cm)

    disp = ConfusionMatrixDisplay(confusion_matrix=cm, display_labels=labels)
    fig, ax = plt.subplots(figsize=(5.5, 5))
    disp.plot(ax=ax, cmap="Blues", colorbar=False, xticks_rotation=30)
    ax.set_title(f"Confusion Matrix — {name}")
    fig.tight_layout()
    fname = os.path.join(
        outdir, f"confusion_matrix_{name.replace(' ', '_').lower()}.png"
    )
    fig.savefig(fname, dpi=150)
    plt.close(fig)

    return metrics, report


def main():
    parser = argparse.ArgumentParser(
        description="Train Random Forest parcel suitability classifier"
    )
    parser.add_argument("--data", type=str, required=True, help="path to parcels CSV")
    parser.add_argument("--outdir", type=str, default="output")
    parser.add_argument("--test-size", type=float, default=0.2)
    parser.add_argument("--cv-folds", type=int, default=5)
    parser.add_argument("--seed", type=int, default=42)
    args = parser.parse_args()

    os.makedirs(args.outdir, exist_ok=True)

    df = pd.read_csv(args.data)
    missing_cols = [
        c
        for c in NUMERIC_FEATURES + CATEGORICAL_FEATURES + [TARGET]
        if c not in df.columns
    ]
    if missing_cols:
        raise ValueError(f"Input CSV is missing expected columns: {missing_cols}")

    # Guard: refuse to train if excluded GIS columns are populated as if confirmed.
    for col in EXCLUDED_UNAVAILABLE_FEATURES:
        if col not in df.columns:
            continue
        if col in ("derived_soil_group",):
            non_unknown = df[col].dropna().astype(str).str.strip()
            invented = non_unknown[~non_unknown.isin(["", "Unknown", "nan", "None"])]
            if len(invented) > 0:
                print(
                    f"WARNING: {col} has {len(invented)} non-Unknown values; "
                    "standard trainer ignores this column (do not treat as confirmed)."
                )
        else:
            non_null = int(df[col].notna().sum())
            if non_null > 0:
                print(
                    f"WARNING: {col} has {non_null} non-null values; "
                    "standard trainer ignores this column (unavailable for research)."
                )

    X = df[NUMERIC_FEATURES + CATEGORICAL_FEATURES].copy()
    X["spatial_constraint_present"] = X["spatial_constraint_present"].astype(str)
    y = df[TARGET]
    labels = _ordered_labels(y)

    X_train, X_test, y_train, y_test = train_test_split(
        X, y, test_size=args.test_size, random_state=args.seed, stratify=y
    )

    preprocessor = build_preprocessor()

    models = {
        "Dummy Majority": DummyClassifier(strategy="most_frequent"),
        "Logistic Regression": LogisticRegression(max_iter=2000, class_weight="balanced"),
        "Decision Tree": DecisionTreeClassifier(
            max_depth=10, min_samples_leaf=3, random_state=args.seed, class_weight="balanced"
        ),
        "Random Forest": RandomForestClassifier(
            n_estimators=400,
            max_depth=14,
            min_samples_leaf=2,
            max_features="sqrt",
            class_weight="balanced_subsample",
            random_state=args.seed,
            n_jobs=-1,
        ),
    }

    # CV on training fold only — never touch the held-out test set here.
    skf = StratifiedKFold(n_splits=args.cv_folds, shuffle=True, random_state=args.seed)

    all_results = {}
    fitted_pipelines = {}

    for name, clf in models.items():
        pipeline = Pipeline(steps=[("preprocessor", preprocessor), ("classifier", clf)])
        pipeline.fit(X_train, y_train)
        fitted_pipelines[name] = pipeline

        metrics, report = evaluate_model(
            name, pipeline, X_test, y_test, labels, args.outdir
        )

        cv_scores = cross_val_score(
            pipeline, X_train, y_train, cv=skf, scoring="f1_macro", n_jobs=-1
        )
        metrics["cv_f1_macro_mean"] = float(cv_scores.mean())
        metrics["cv_f1_macro_std"] = float(cv_scores.std())
        print(
            f"Stratified {args.cv_folds}-fold CV on train (F1 macro): "
            f"{cv_scores.mean():.4f} +/- {cv_scores.std():.4f}"
        )

        all_results[name] = metrics

    rf_pipeline = fitted_pipelines["Random Forest"]
    feature_names = rf_pipeline.named_steps["preprocessor"].get_feature_names_out()
    importances = rf_pipeline.named_steps["classifier"].feature_importances_
    fi = (
        pd.Series(importances, index=feature_names)
        .sort_values(ascending=False)
        .head(20)
    )

    fig, ax = plt.subplots(figsize=(8, 6))
    fi[::-1].plot(kind="barh", ax=ax)
    ax.set_title("Random Forest — Top 20 Feature Importances")
    ax.set_xlabel("Importance")
    fig.tight_layout()
    fig.savefig(os.path.join(args.outdir, "rf_feature_importance.png"), dpi=150)
    plt.close(fig)
    fi.to_csv(os.path.join(args.outdir, "rf_feature_importance.csv"))

    model_path = os.path.join(args.outdir, "random_forest_pipeline.joblib")
    joblib.dump(rf_pipeline, model_path)

    target_accuracy = 0.80
    rf_accuracy = all_results["Random Forest"]["accuracy"]
    dummy_accuracy = all_results["Dummy Majority"]["accuracy"]
    summary = {
        "dataset": {
            "rows": int(len(df)),
            "train_rows": int(len(X_train)),
            "test_rows": int(len(X_test)),
            "test_size": args.test_size,
            "seed": args.seed,
            "label_source": "synthetic_rule_without_road_water_soil",
            "features_used": NUMERIC_FEATURES + CATEGORICAL_FEATURES,
            "features_excluded_unavailable": EXCLUDED_UNAVAILABLE_FEATURES,
        },
        "target_held_out_accuracy": target_accuracy,
        "target_achieved": bool(rf_accuracy >= target_accuracy),
        "models": all_results,
        "notes": [
            "ML is supplementary evidence only; rule-based scoring remains authoritative.",
            "Road, water, and soil were excluded and not invented.",
            "Cross-validation uses the training fold only.",
            "Experimental Colombo GIS ML must stay separate (output_colombo_* / port 8501).",
        ],
    }

    metrics_path = os.path.join(args.outdir, "metrics_summary.json")
    with open(metrics_path, "w", encoding="utf-8") as f:
        json.dump(summary, f, indent=2)

    report_path = os.path.join(args.outdir, "EVALUATION_REPORT.md")
    with open(report_path, "w", encoding="utf-8") as f:
        f.write("# Standard ML evaluation report\n\n")
        f.write(
            "Synthetic labels from trustworthy parcel fields only. "
            "Road, water, and soil were **not** invented or used.\n\n"
        )
        f.write(f"- Held-out Random Forest accuracy: **{rf_accuracy:.4f}** "
                f"({rf_accuracy * 100:.2f}%)\n")
        f.write(f"- Dummy majority baseline accuracy: **{dummy_accuracy:.4f}**\n")
        f.write(f"- 80% target achieved: **{'yes' if rf_accuracy >= target_accuracy else 'no'}**\n\n")
        if rf_accuracy < target_accuracy:
            f.write(
                "## Why 80% is not claimed\n\n"
                "Credible predictors without road/water/soil GIS leave substantial "
                "overlap between classes once label noise is kept honest. Reaching "
                "80% would require either inventing unavailable GIS fields, "
                "reducing label noise to chase the target, or real expert-labelled "
                "parcels with measured enrichment. Until then, keep ML "
                "**supplementary** and do not present the target as achieved.\n\n"
                "### Data needed for a credible ≥80% claim\n\n"
                "- Expert or historically validated suitability labels (not rule-replay)\n"
                "- Measured GIS road/water/soil only when enrichment actually exists\n"
                "- Enough labelled rows per class for a stable held-out set\n"
            )
        f.write("\n## Per-class (Random Forest, held-out)\n\n")
        f.write("| Class | Precision | Recall | F1 | Support |\n")
        f.write("|-------|-----------|--------|----|---------|\n")
        for label, row in all_results["Random Forest"]["per_class"].items():
            f.write(
                f"| {label} | {row['precision']:.4f} | {row['recall']:.4f} | "
                f"{row['f1']:.4f} | {row['support']} |\n"
            )

    print(f"\nSaved: {model_path}")
    print(f"Saved: {metrics_path}")
    print(f"Saved: {report_path}")
    print(f"Saved: confusion matrices + feature importance plots in {args.outdir}/")

    print("\n" + "=" * 60)
    print("Model comparison (held-out test set)")
    print("=" * 60)
    comp = pd.DataFrame(
        {
            name: {
                "accuracy": m["accuracy"],
                "f1_macro": m["f1_macro"],
                "f1_weighted": m["f1_weighted"],
                "cv_f1_macro_mean": m["cv_f1_macro_mean"],
            }
            for name, m in all_results.items()
        }
    ).T
    print(comp.round(4))
    print(
        f"\n80% held-out accuracy target: "
        f"{'ACHIEVED' if rf_accuracy >= target_accuracy else 'NOT ACHIEVED'} "
        f"(RF={rf_accuracy:.4f}, Dummy={dummy_accuracy:.4f})"
    )


if __name__ == "__main__":
    main()
