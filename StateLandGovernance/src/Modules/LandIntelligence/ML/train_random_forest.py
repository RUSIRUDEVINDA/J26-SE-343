"""
Random Forest parcel-purpose suitability classifier
------------------------------------------------------
Component 1 - Intelligent Land Knowledge Graph and Spatial Recommendation Platform
J26-SE-343

Trains Random Forest (primary candidate, per Table 3) against Logistic
Regression and Decision Tree baselines, and reports the metrics required by
Table 4 of the proposal: Accuracy, Precision, Recall, F1, Macro/Weighted F1,
Confusion Matrix, and stratified k-fold cross-validation.

Usage:
    python train_random_forest.py --data parcels_dataset.csv --outdir output

Input CSV is expected to have the columns produced by
generate_synthetic_dataset.py (or a real export with the same columns):
    requested_purpose, land_category, area_hectares, province, district,
    elevation_meters, distance_to_road_m, distance_to_water_m,
    gis_enrichment_status, derived_soil_group, soil_overlap_percentage,
    terrain_description, environmental_restriction_type,
    environmental_restriction_severity, spatial_constraint_present,
    suitability_label   <- target
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
from sklearn.ensemble import RandomForestClassifier
from sklearn.impute import SimpleImputer
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import (ConfusionMatrixDisplay, accuracy_score,
                              classification_report, confusion_matrix,
                              f1_score, precision_score, recall_score)
from sklearn.model_selection import (StratifiedKFold, cross_val_score,
                                      train_test_split)
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import OneHotEncoder
from sklearn.tree import DecisionTreeClassifier

TARGET = "suitability_label"
ID_COLS = ["parcel_id"]

NUMERIC_FEATURES = [
    "area_hectares", "elevation_meters", "distance_to_road_m",
    "distance_to_water_m", "soil_overlap_percentage",
]
CATEGORICAL_FEATURES = [
    "requested_purpose", "land_category", "province", "district",
    "gis_enrichment_status", "derived_soil_group", "terrain_description",
    "environmental_restriction_type", "environmental_restriction_severity",
    "spatial_constraint_present",
]


def build_preprocessor() -> ColumnTransformer:
    numeric_pipeline = Pipeline(steps=[
        # Missing GIS evidence (NaN) is imputed with the median only for the
        # model's internal math; it is never treated as a "favourable" value,
        # and the categorical "Unknown"/"Unavailable" columns still record
        # that the evidence was missing so the pattern is visible to the model.
        ("imputer", SimpleImputer(strategy="median")),
    ])
    categorical_pipeline = Pipeline(steps=[
        ("imputer", SimpleImputer(strategy="constant", fill_value="Unknown")),
        ("onehot", OneHotEncoder(handle_unknown="ignore")),
    ])
    return ColumnTransformer(transformers=[
        ("num", numeric_pipeline, NUMERIC_FEATURES),
        ("cat", categorical_pipeline, CATEGORICAL_FEATURES),
    ])


def evaluate_model(name, pipeline, X_test, y_test, labels, outdir):
    y_pred = pipeline.predict(X_test)

    metrics = {
        "accuracy": accuracy_score(y_test, y_pred),
        "precision_macro": precision_score(y_test, y_pred, average="macro", zero_division=0),
        "recall_macro": recall_score(y_test, y_pred, average="macro", zero_division=0),
        "f1_macro": f1_score(y_test, y_pred, average="macro", zero_division=0),
        "f1_weighted": f1_score(y_test, y_pred, average="weighted", zero_division=0),
    }

    report = classification_report(y_test, y_pred, labels=labels, zero_division=0)
    cm = confusion_matrix(y_test, y_pred, labels=labels)

    print(f"\n{'=' * 60}\n{name}\n{'=' * 60}")
    for k, v in metrics.items():
        print(f"{k:>18}: {v:.4f}")
    print("\nClassification report:\n", report)
    print("Confusion matrix (rows=actual, cols=predicted):\n", cm)

    disp = ConfusionMatrixDisplay(confusion_matrix=cm, display_labels=labels)
    fig, ax = plt.subplots(figsize=(5.5, 5))
    disp.plot(ax=ax, cmap="Blues", colorbar=False, xticks_rotation=30)
    ax.set_title(f"Confusion Matrix — {name}")
    fig.tight_layout()
    fname = os.path.join(outdir, f"confusion_matrix_{name.replace(' ', '_').lower()}.png")
    fig.savefig(fname, dpi=150)
    plt.close(fig)

    return metrics, report


def main():
    parser = argparse.ArgumentParser(description="Train Random Forest parcel suitability classifier")
    parser.add_argument("--data", type=str, required=True, help="path to parcels CSV")
    parser.add_argument("--outdir", type=str, default="output")
    parser.add_argument("--test-size", type=float, default=0.2)
    parser.add_argument("--cv-folds", type=int, default=5)
    parser.add_argument("--seed", type=int, default=42)
    args = parser.parse_args()

    os.makedirs(args.outdir, exist_ok=True)

    df = pd.read_csv(args.data)
    missing_cols = [c for c in NUMERIC_FEATURES + CATEGORICAL_FEATURES + [TARGET] if c not in df.columns]
    if missing_cols:
        raise ValueError(f"Input CSV is missing expected columns: {missing_cols}")

    X = df[NUMERIC_FEATURES + CATEGORICAL_FEATURES].copy()
    X["spatial_constraint_present"] = X["spatial_constraint_present"].astype(str)
    y = df[TARGET]
    labels = sorted(y.unique(), key=lambda l: ["Unsuitable", "Moderately Suitable", "Suitable"].index(l)
                     if l in ["Unsuitable", "Moderately Suitable", "Suitable"] else 99)

    X_train, X_test, y_train, y_test = train_test_split(
        X, y, test_size=args.test_size, random_state=args.seed, stratify=y
    )

    preprocessor = build_preprocessor()

    models = {
        "Logistic Regression": LogisticRegression(max_iter=1000),
        "Decision Tree": DecisionTreeClassifier(max_depth=8, random_state=args.seed),
        "Random Forest": RandomForestClassifier(
            n_estimators=300, max_depth=None, min_samples_leaf=2,
            class_weight="balanced", random_state=args.seed, n_jobs=-1,
        ),
    }

    skf = StratifiedKFold(n_splits=args.cv_folds, shuffle=True, random_state=args.seed)

    all_results = {}
    fitted_pipelines = {}

    for name, clf in models.items():
        pipeline = Pipeline(steps=[("preprocessor", preprocessor), ("classifier", clf)])
        pipeline.fit(X_train, y_train)
        fitted_pipelines[name] = pipeline

        metrics, report = evaluate_model(name, pipeline, X_test, y_test, labels, args.outdir)

        cv_scores = cross_val_score(pipeline, X, y, cv=skf, scoring="f1_macro", n_jobs=-1)
        metrics["cv_f1_macro_mean"] = float(cv_scores.mean())
        metrics["cv_f1_macro_std"] = float(cv_scores.std())
        print(f"Stratified {args.cv_folds}-fold CV (F1 macro): "
              f"{cv_scores.mean():.4f} +/- {cv_scores.std():.4f}")

        all_results[name] = metrics

    # Feature importance for Random Forest (primary candidate model)
    rf_pipeline = fitted_pipelines["Random Forest"]
    feature_names = rf_pipeline.named_steps["preprocessor"].get_feature_names_out()
    importances = rf_pipeline.named_steps["classifier"].feature_importances_
    fi = pd.Series(importances, index=feature_names).sort_values(ascending=False).head(20)

    fig, ax = plt.subplots(figsize=(8, 6))
    fi[::-1].plot(kind="barh", ax=ax)
    ax.set_title("Random Forest — Top 20 Feature Importances")
    ax.set_xlabel("Importance")
    fig.tight_layout()
    fig.savefig(os.path.join(args.outdir, "rf_feature_importance.png"), dpi=150)
    plt.close(fig)
    fi.to_csv(os.path.join(args.outdir, "rf_feature_importance.csv"))

    # Persist the trained Random Forest pipeline (preprocessing + model) so it
    # can be served from the REST API / ML experiment layer described in the
    # proposal, without retraining.
    model_path = os.path.join(args.outdir, "random_forest_pipeline.joblib")
    joblib.dump(rf_pipeline, model_path)

    with open(os.path.join(args.outdir, "metrics_summary.json"), "w") as f:
        json.dump(all_results, f, indent=2)

    print(f"\nSaved: {model_path}")
    print(f"Saved: {os.path.join(args.outdir, 'metrics_summary.json')}")
    print(f"Saved: confusion matrices + feature importance plots in {args.outdir}/")

    print("\n" + "=" * 60)
    print("Model comparison (test set)")
    print("=" * 60)
    comp = pd.DataFrame(all_results).T[["accuracy", "f1_macro", "f1_weighted", "cv_f1_macro_mean"]]
    print(comp.round(4))


if __name__ == "__main__":
    main()
