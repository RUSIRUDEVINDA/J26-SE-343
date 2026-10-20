# Land Intelligence ML Service

Random Forest suitability model for Component 1. This service is **supplementary evidence only** — the .NET rule-based recommendation engine keeps all hard constraints and scoring unchanged.

## Honest status (standard service)

| Item | Value |
|------|--------|
| Held-out Random Forest accuracy | **~76.5%** (see `output/metrics_summary.json`) |
| 80% target | **Not achieved** — do not claim otherwise |
| Dummy majority baseline | ~49.5% |
| Road / water / soil | **Excluded** — unavailable for research; not invented |
| Labels | Synthetic rule labels from trustworthy fields only |
| Colombo experimental ML | **Separate** (`experimental_ml_service.py` :8501) — do not replace `output/` |

Full write-up: [`output/EVALUATION_REPORT.md`](./output/EVALUATION_REPORT.md).

### Trustworthy features used

- Numeric: `area_hectares`, `elevation_meters` (nullable)
- Categorical: `requested_purpose`, `land_category`, `province`, `district`, `gis_enrichment_status`, `terrain_description`, `environmental_restriction_type`, `environmental_restriction_severity`, `spatial_constraint_present`

### Explicitly ignored

`distance_to_road_m`, `distance_to_water_m`, `derived_soil_group`, `soil_overlap_percentage` — accepted on the HTTP schema for .NET compatibility, dropped before prediction.

### Data needed for a credible ≥80% claim

- Expert or historically validated labels (not rule-replay of the same fields)
- Measured GIS road/water/soil **only when enrichment actually exists**
- Enough labelled rows in the minority `Suitable` class for a stable held-out set

Until then, keep ML supplementary.

## Prerequisites

- Python 3.10+
- Trained model at `output/random_forest_pipeline.joblib`

## Install and run

From this directory (`StateLandGovernance/src/Modules/LandIntelligence/ML/`):

**Windows (Python Launcher — use this if `pip` is not recognized):**

```powershell
py -m pip install -r requirements.txt
py -m uvicorn ml_service:app --host 0.0.0.0 --port 8500
```

**Linux/macOS:**

```bash
pip install -r requirements.txt
uvicorn ml_service:app --host 0.0.0.0 --port 8500
```

Health check: `GET http://localhost:8500/health`

## .NET configuration

Add to your repository `.env`:

```
ML_SUITABILITY_SERVICE_URL=http://localhost:8500
```

The backend calls `POST /predict` via `HttpMlSuitabilityClient`. If the service is down, recommendations continue without ML evidence.

## Retrain (standard)

```bash
python generate_synthetic_dataset.py --n 3000 --out parcels_dataset.csv
python train_random_forest.py --data parcels_dataset.csv --outdir output
python -m unittest test_ml_service.py -v
```

Evaluation uses an 80/20 stratified held-out test set, per-class precision/recall/F1, a Dummy majority baseline, and stratified CV **on the training fold only**.

Replace synthetic data with real labelled exports when available. Reports must state synthetic vs real.

## Colombo experimental (opt-in local only)

**Canonical readiness + runbook:**
[`docs/COLOMBO_EXPERIMENTAL_GIS_ML.md`](../docs/COLOMBO_EXPERIMENTAL_GIS_ML.md)

**Decision:** Available for opt-in local research demonstration; **not** validated for
real-world suitability decisions. Default `ExperimentalColomboMl:Enabled` remains
**false**. Candidate id: `colombo_osm_backend_compatible` under
`output_colombo_osm_backend_compatible/`.

### Experimental serving (separate process / port)

```powershell
cd StateLandGovernance\src\Modules\LandIntelligence\ML
$env:EXPERIMENTAL_COLOMBO_ML_PORT = "8501"
$env:EXPERIMENTAL_COLOMBO_ML_MODEL_DIR = (Resolve-Path .\output_colombo_osm_backend_compatible).Path
py -m uvicorn experimental_ml_service:app --host 127.0.0.1 --port 8501
```

Do **not** replace `ml_service.py` (port 8500) or live `output/` joblib.
