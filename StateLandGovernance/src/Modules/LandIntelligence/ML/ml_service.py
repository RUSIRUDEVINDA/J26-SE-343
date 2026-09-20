"""
ML suitability serving API
---------------------------
Loads the trained Random Forest pipeline (random_forest_pipeline.joblib)
and exposes it over HTTP so the .NET backend can call it.

Run from this directory:
    pip install -r requirements.txt
    uvicorn ml_service:app --host 0.0.0.0 --port 8500
"""

from pathlib import Path
from typing import Optional

import joblib
import pandas as pd
from fastapi import FastAPI
from pydantic import BaseModel, Field

MODEL_PATH = Path(__file__).parent / "output" / "random_forest_pipeline.joblib"

# Column order matches train_random_forest.py / generate_synthetic_dataset.py.
MODEL_FEATURE_COLUMNS = [
    "requested_purpose",
    "land_category",
    "area_hectares",
    "province",
    "district",
    "elevation_meters",
    "distance_to_road_m",
    "distance_to_water_m",
    "gis_enrichment_status",
    "derived_soil_group",
    "soil_overlap_percentage",
    "terrain_description",
    "environmental_restriction_type",
    "environmental_restriction_severity",
    "spatial_constraint_present",
]

app = FastAPI(title="Land Suitability ML Service")
pipeline = joblib.load(MODEL_PATH)


class ParcelFeatures(BaseModel):
    requested_purpose: str
    land_category: str
    area_hectares: float
    province: str
    district: str
    elevation_meters: Optional[float] = Field(
        default=None,
        description="Null when elevation evidence is missing.",
    )
    distance_to_road_m: Optional[float] = Field(
        default=None,
        description="Null when GIS road distance is missing or unavailable.",
    )
    distance_to_water_m: Optional[float] = Field(
        default=None,
        description="Null when GIS water distance is missing or unavailable.",
    )
    gis_enrichment_status: str = Field(
        description='GIS enrichment status: "Complete", "Partial", or "Unavailable".',
    )
    derived_soil_group: Optional[str] = Field(
        default=None,
        description='GIS-derived soil group, explicit "Unknown", or null when missing.',
    )
    soil_overlap_percentage: Optional[float] = Field(
        default=None,
        description="Null when GIS soil overlap is missing or unavailable.",
    )
    terrain_description: Optional[str] = Field(
        default=None,
        description='Terrain description, explicit "Unknown", or null when missing.',
    )
    environmental_restriction_type: Optional[str] = Field(
        default=None,
        description='Restriction type, "None" when confirmed absent, "Unknown", or null when missing.',
    )
    environmental_restriction_severity: Optional[str] = Field(
        default=None,
        description='Restriction severity, "None" when confirmed absent, "Unknown", or null when missing.',
    )
    spatial_constraint_present: Optional[bool] = Field(
        default=None,
        description="Null when spatial-constraint evidence is missing; otherwise true/false.",
    )


class PredictionResponse(BaseModel):
    predicted_label: str
    probabilities: dict[str, float]


def _to_model_row(features: ParcelFeatures) -> pd.DataFrame:
    """
    Build a single-row DataFrame for the saved sklearn pipeline.

    Missing GIS evidence is passed through as pandas NA/NaN (or explicit
    "Unknown" strings from the caller). The API does not impute or guess;
    the joblib pipeline's SimpleImputer steps handle missing values exactly
    as during training.
    """
    row = pd.DataFrame([features.model_dump()])[MODEL_FEATURE_COLUMNS]
    row["spatial_constraint_present"] = row["spatial_constraint_present"].map(
        lambda value: str(value) if value is not None else pd.NA
    )
    return row


@app.post("/predict", response_model=PredictionResponse)
def predict(features: ParcelFeatures) -> PredictionResponse:
    row = _to_model_row(features)

    predicted = pipeline.predict(row)[0]
    proba = pipeline.predict_proba(row)[0]
    classes = pipeline.named_steps["classifier"].classes_

    return PredictionResponse(
        predicted_label=predicted,
        probabilities={cls: float(p) for cls, p in zip(classes, proba)},
    )


@app.get("/health")
def health():
    return {"status": "ok"}
