"""
Opt-in local experimental ML serving for Colombo backend-compatible candidate.

Loads ONLY output_colombo_osm_backend_compatible artifacts.
Does not replace ml_service.py or the live output/ model.

Usage (from StateLandGovernance/ or this directory):
    py experimental_ml_service.py
    # or:
    uvicorn experimental_ml_service:app --host 127.0.0.1 --port 8501

Environment:
    EXPERIMENTAL_COLOMBO_ML_PORT   default 8501
    EXPERIMENTAL_COLOMBO_ML_MODEL_DIR  optional absolute/relative path to artifact dir
"""

from __future__ import annotations

import json
import os
import sys
from pathlib import Path
from typing import Any, Optional

import joblib
import pandas as pd
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field

CANDIDATE_ID = "colombo_osm_backend_compatible"
DEFAULT_PORT = 8501
REQUIRED_NUMERIC = [
    "area_hectares",
    "distance_to_road_m",
    "distance_to_water_m",
]
REQUIRED_CATEGORICAL = [
    "requested_purpose",
    "land_category",
    "gis_enrichment_status",
    "derived_soil_group",
    "spatial_constraint_present",
]
MODEL_FEATURE_COLUMNS = REQUIRED_NUMERIC + REQUIRED_CATEGORICAL


def _resolve_model_dir() -> Path:
    env = os.environ.get("EXPERIMENTAL_COLOMBO_ML_MODEL_DIR")
    if env:
        path = Path(env)
        return path if path.is_absolute() else (Path.cwd() / path).resolve()
    return (Path(__file__).resolve().parent / "output_colombo_osm_backend_compatible").resolve()


def _load_artifacts(model_dir: Path) -> tuple[Any, dict]:
    schema_path = model_dir / "feature_schema.json"
    model_path = model_dir / "random_forest_pipeline.joblib"
    if not schema_path.is_file():
        raise FileNotFoundError(f"Missing feature schema: {schema_path}")
    if not model_path.is_file():
        raise FileNotFoundError(f"Missing model artifact: {model_path}")

    schema = json.loads(schema_path.read_text(encoding="utf-8"))
    if schema.get("candidate") != CANDIDATE_ID:
        raise ValueError(
            f"Incompatible candidate id {schema.get('candidate')!r}; "
            f"expected {CANDIDATE_ID!r}."
        )
    if schema.get("model_activation_enabled") is True:
        raise ValueError(
            "Refusing to load artifact with model_activation_enabled=true "
            "(experimental serve must stay opt-in / non-production)."
        )

    numeric = schema.get("numeric_features") or []
    categorical = schema.get("categorical_features") or []
    if numeric != REQUIRED_NUMERIC or categorical != REQUIRED_CATEGORICAL:
        raise ValueError(
            "Feature schema mismatch for backend-compatible candidate.\n"
            f"  numeric: expected {REQUIRED_NUMERIC}, got {numeric}\n"
            f"  categorical: expected {REQUIRED_CATEGORICAL}, got {categorical}"
        )

    pipeline = joblib.load(model_path)
    return pipeline, schema


MODEL_DIR = _resolve_model_dir()
try:
    PIPELINE, FEATURE_SCHEMA = _load_artifacts(MODEL_DIR)
except Exception as exc:  # noqa: BLE001 — fail at import/startup with clear message
    print(f"ERROR: experimental Colombo ML artifacts unavailable: {exc}", file=sys.stderr)
    raise

app = FastAPI(
    title="Colombo Experimental ML Service (backend-compatible)",
    description=(
        "Opt-in local serving for colombo_osm_backend_compatible. "
        "Not the production suitability model."
    ),
    version="experimental-local",
)


class ExperimentalParcelFeatures(BaseModel):
    requested_purpose: str
    land_category: str
    area_hectares: float
    distance_to_road_m: Optional[float] = None
    distance_to_water_m: Optional[float] = None
    gis_enrichment_status: str
    derived_soil_group: str = "Unknown"
    spatial_constraint_present: Optional[bool] = Field(
        default=None,
        description="Must be true/false when assessed; null is rejected (abstain client-side).",
    )


class ExperimentalPredictionResponse(BaseModel):
    predicted_label: str
    probabilities: dict[str, float]
    model_identity: dict[str, Any]
    limitations: list[str]


def _model_identity() -> dict[str, Any]:
    return {
        "candidate": CANDIDATE_ID,
        "model_dir": str(MODEL_DIR),
        "model_file": "random_forest_pipeline.joblib",
        "model_activation_enabled": False,
        "production_default": False,
        "numeric_features": REQUIRED_NUMERIC,
        "categorical_features": REQUIRED_CATEGORICAL,
    }


def _to_model_row(features: ExperimentalParcelFeatures) -> pd.DataFrame:
    if features.spatial_constraint_present is None:
        raise HTTPException(
            status_code=422,
            detail=(
                "spatial_constraint_present is null. Experimental serving requires an "
                "assessed conservation value; clients must abstain instead of calling /predict."
            ),
        )

    payload = {
        "requested_purpose": features.requested_purpose,
        "land_category": features.land_category,
        "area_hectares": features.area_hectares,
        "distance_to_road_m": features.distance_to_road_m
        if features.distance_to_road_m is not None
        else pd.NA,
        "distance_to_water_m": features.distance_to_water_m
        if features.distance_to_water_m is not None
        else pd.NA,
        "gis_enrichment_status": features.gis_enrichment_status,
        "derived_soil_group": features.derived_soil_group or "Unknown",
        "spatial_constraint_present": str(features.spatial_constraint_present),
    }
    return pd.DataFrame([payload])[MODEL_FEATURE_COLUMNS]


@app.get("/health")
def health() -> dict[str, Any]:
    return {
        "status": "ok",
        "service": "experimental_colombo_ml",
        "model_identity": _model_identity(),
    }


@app.get("/status")
def status() -> dict[str, Any]:
    return {
        "status": "ready",
        "model_identity": _model_identity(),
        "missing_evidence_policy": FEATURE_SCHEMA.get("missing_evidence_policy"),
        "limitations": [
            FEATURE_SCHEMA.get("label_limitations"),
            FEATURE_SCHEMA.get("independent_evaluation_required"),
            "Not production-activated; supplementary evidence only.",
        ],
    }


@app.post("/predict", response_model=ExperimentalPredictionResponse)
def predict(features: ExperimentalParcelFeatures) -> ExperimentalPredictionResponse:
    row = _to_model_row(features)
    predicted = PIPELINE.predict(row)[0]
    proba = PIPELINE.predict_proba(row)[0]
    classes = PIPELINE.named_steps["classifier"].classes_
    return ExperimentalPredictionResponse(
        predicted_label=str(predicted),
        probabilities={str(cls): float(p) for cls, p in zip(classes, proba)},
        model_identity=_model_identity(),
        limitations=[
            "Simulated-label training; not real-world suitability validation.",
            "Supplementary evidence only; does not override rule-based scores or hard constraints.",
            "Excluded environmental_restriction_type/severity from predictors.",
        ],
    )


def main() -> None:
    import uvicorn

    port = int(os.environ.get("EXPERIMENTAL_COLOMBO_ML_PORT", str(DEFAULT_PORT)))
    uvicorn.run(
        "experimental_ml_service:app",
        host="127.0.0.1",
        port=port,
        reload=False,
    )


if __name__ == "__main__":
    main()
