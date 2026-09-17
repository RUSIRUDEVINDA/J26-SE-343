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
from pydantic import BaseModel

MODEL_PATH = Path(__file__).parent / "output" / "random_forest_pipeline.joblib"

app = FastAPI(title="Land Suitability ML Service")
pipeline = joblib.load(MODEL_PATH)


class ParcelFeatures(BaseModel):
    requested_purpose: str
    land_category: str
    area_hectares: float
    province: str
    district: str
    elevation_meters: Optional[float] = None
    distance_to_road_m: Optional[float] = None
    distance_to_water_m: Optional[float] = None
    gis_enrichment_status: str
    derived_soil_group: str = "Unknown"
    soil_overlap_percentage: Optional[float] = None
    terrain_description: str = "Unknown"
    environmental_restriction_type: str = "None"
    environmental_restriction_severity: str = "None"
    spatial_constraint_present: bool = False


class PredictionResponse(BaseModel):
    predicted_label: str
    probabilities: dict[str, float]


@app.post("/predict", response_model=PredictionResponse)
def predict(features: ParcelFeatures) -> PredictionResponse:
    row = pd.DataFrame([features.model_dump()])
    row["spatial_constraint_present"] = row["spatial_constraint_present"].astype(str)

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
