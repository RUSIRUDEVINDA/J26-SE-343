"""
ML suitability serving API
---------------------------
Loads the trained Random Forest pipeline (random_forest_pipeline.joblib)
and exposes it over HTTP so the .NET backend can call it.

The standard model uses trustworthy parcel fields only. Road, water, and soil
may still arrive from the .NET client for API compatibility but are ignored —
they are unavailable for the research dataset and must not be treated as
confirmed measurements.

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

# Must match train_random_forest.py NUMERIC_FEATURES + CATEGORICAL_FEATURES.
MODEL_FEATURE_COLUMNS = [
    "area_hectares",
    "elevation_meters",
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

app = FastAPI(
    title="Land Suitability ML Service",
    description=(
        "Supplementary Random Forest evidence only. "
        "Road/water/soil inputs are accepted but ignored by the standard model."
    ),
)
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
    # Accepted for .NET client compatibility; ignored by the standard model.
    distance_to_road_m: Optional[float] = Field(
        default=None,
        description="Ignored by standard model (unavailable / not inventable).",
    )
    distance_to_water_m: Optional[float] = Field(
        default=None,
        description="Ignored by standard model (unavailable / not inventable).",
    )
    gis_enrichment_status: str = Field(
        description='GIS enrichment status: "Complete", "Partial", or "Unavailable".',
    )
    derived_soil_group: Optional[str] = Field(
        default=None,
        description="Ignored by standard model (unavailable / not inventable).",
    )
    soil_overlap_percentage: Optional[float] = Field(
        default=None,
        description="Ignored by standard model (unavailable / not inventable).",
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
    ignored_unavailable_features: list[str] = Field(
        default_factory=list,
        description="Feature names accepted but not used by the standard model.",
    )


def _to_model_row(features: ParcelFeatures) -> pd.DataFrame:
    """
    Build a single-row DataFrame for the saved sklearn pipeline.

    Road/water/soil are dropped before prediction. Missing elevation/terrain
    pass through as NA / Unknown for the pipeline imputers.
    """
    payload = features.model_dump()
    row = pd.DataFrame(
        [
            {
                "area_hectares": payload["area_hectares"],
                "elevation_meters": payload["elevation_meters"],
                "requested_purpose": payload["requested_purpose"],
                "land_category": payload["land_category"],
                "province": payload["province"],
                "district": payload["district"],
                "gis_enrichment_status": payload["gis_enrichment_status"],
                "terrain_description": payload["terrain_description"],
                "environmental_restriction_type": payload[
                    "environmental_restriction_type"
                ],
                "environmental_restriction_severity": payload[
                    "environmental_restriction_severity"
                ],
                "spatial_constraint_present": (
                    str(payload["spatial_constraint_present"])
                    if payload["spatial_constraint_present"] is not None
                    else pd.NA
                ),
            }
        ]
    )[MODEL_FEATURE_COLUMNS]
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
        ignored_unavailable_features=[
            "distance_to_road_m",
            "distance_to_water_m",
            "derived_soil_group",
            "soil_overlap_percentage",
        ],
    )


@app.get("/health")
def health():
    return {
        "status": "ok",
        "model": "standard_random_forest",
        "features": MODEL_FEATURE_COLUMNS,
        "ignored_unavailable_features": [
            "distance_to_road_m",
            "distance_to_water_m",
            "derived_soil_group",
            "soil_overlap_percentage",
        ],
        "supplementary_only": True,
    }
