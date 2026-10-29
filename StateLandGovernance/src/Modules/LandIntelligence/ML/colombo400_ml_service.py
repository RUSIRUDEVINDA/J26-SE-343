"""Opt-in research inference for the expert-labelled Colombo 400 parcel dataset.

The feature store is frozen to the reviewed workbook. No labels, lease status, or
recommendation scores are read at inference time. This service is supplementary
decision support and cannot approve a lease.
"""

import hashlib
import json
import logging
from pathlib import Path
from typing import Literal

import joblib
import pandas as pd
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field


ARTIFACT_DIR = Path(__file__).with_name("colombo400_artefacts")
FEATURE_COLUMNS = [
    "Intended Purpose",
    "Source-reported Land Use",
    "Project Land-cover Group",
    "Classification Basis",
    "primary_cover_cat",
    "Area (ha)",
    "primary_cover_pct",
    "overlap_available",
]
DISCLAIMER = (
    "Research decision support only. This is not zoning approval, legal lease "
    "availability, a reservation, a permit, or lease approval."
)
logger = logging.getLogger(__name__)


def _load_artifacts():
    manifest = json.loads((ARTIFACT_DIR / "inference_manifest.json").read_text(encoding="utf-8"))
    feature_file = ARTIFACT_DIR / "inference_features.json"
    digest = hashlib.sha256(feature_file.read_bytes()).hexdigest()
    if digest != manifest["feature_store_sha256"]:
        raise RuntimeError("Colombo 400 feature store hash mismatch")
    for name, expected in manifest["model_sha256"].items():
        if hashlib.sha256((ARTIFACT_DIR / name).read_bytes()).hexdigest() != expected:
            raise RuntimeError(f"Colombo 400 model hash mismatch: {name}")
    features = json.loads(feature_file.read_text(encoding="utf-8"))
    if len(features) != manifest["parcel_count"] or len(features) != 400:
        raise RuntimeError("Colombo 400 feature store must contain 400 distinct parcels")
    config = json.loads((ARTIFACT_DIR / "config.json").read_text(encoding="utf-8"))
    configured = (
        config["features"]["categorical"]
        + config["features"]["numeric"]
        + config["features"]["boolean"]
    )
    if configured != FEATURE_COLUMNS:
        raise RuntimeError("Colombo 400 feature order differs from the trained model")
    stage_a = joblib.load(ARTIFACT_DIR / "stage_a_pipeline.joblib")
    stage_b = joblib.load(ARTIFACT_DIR / "stage_b_pipeline.joblib")
    return features, stage_a, stage_b


FEATURES, STAGE_A, STAGE_B = _load_artifacts()
app = FastAPI(title="Colombo 400 research ML", description=DISCLAIMER)


class PredictionRequest(BaseModel):
    cadastral_number: str = Field(min_length=1)
    requested_purpose: Literal["Agricultural", "Residential", "Commercial"]
    area_hectares: float = Field(gt=0)


class PredictionResponse(BaseModel):
    predicted_label: str
    probabilities: dict[str, float]
    evidence_status: str
    model_source: str = "Colombo 400 expert-labelled research dataset"
    disclaimer: str = DISCLAIMER


def _model_row(parcel: dict[str, str], purpose: str) -> pd.DataFrame:
    row = {"Intended Purpose": purpose, **parcel}
    row["Area (ha)"] = float(row["Area (ha)"])
    row["primary_cover_pct"] = float(row["primary_cover_pct"])
    row["overlap_available"] = int(row["overlap_available"])
    return pd.DataFrame([row], columns=FEATURE_COLUMNS)


@app.post("/predict", response_model=PredictionResponse)
def predict(request: PredictionRequest) -> PredictionResponse:
    parcel = FEATURES.get(request.cadastral_number.strip())
    if parcel is None:
        raise HTTPException(status_code=404, detail="Parcel is outside the reviewed Colombo 400 dataset")
    reviewed_area = float(parcel["Area (ha)"])
    if abs(request.area_hectares - reviewed_area) > max(0.00001, reviewed_area * 0.001):
        raise HTTPException(status_code=409, detail="Parcel area differs from the reviewed dataset")
    row = _model_row(parcel, request.requested_purpose)
    evidence_label = str(STAGE_A.predict(row)[0])
    if evidence_label == "Insufficient Evidence":
        probabilities = STAGE_A.predict_proba(row)[0]
        return PredictionResponse(
            predicted_label=evidence_label,
            probabilities={str(label): float(value) for label, value in zip(STAGE_A.classes_, probabilities)},
            evidence_status=evidence_label,
        )
    if evidence_label != "Sufficient Evidence":
        raise RuntimeError(f"Unexpected Stage A label: {evidence_label}")
    suitability_label = str(STAGE_B.predict(row)[0])
    if request.requested_purpose == "Commercial" and suitability_label == "Suitable":
        logger.error("Commercial Suitable policy violation for parcel %s", request.cadastral_number)
        raise HTTPException(status_code=409, detail="Commercial Suitable policy violation")
    probabilities = STAGE_B.predict_proba(row)[0]
    return PredictionResponse(
        predicted_label=suitability_label,
        probabilities={str(label): float(value) for label, value in zip(STAGE_B.classes_, probabilities)},
        evidence_status=evidence_label,
    )


@app.get("/health")
def health():
    return {
        "status": "ok",
        "model": "colombo400_two_stage_research",
        "parcel_count": len(FEATURES),
        "supplementary_only": True,
        "disclaimer": DISCLAIMER,
    }
