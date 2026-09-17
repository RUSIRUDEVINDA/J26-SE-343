# Land Intelligence ML Service

Random Forest suitability model for Component 1. This service is **supplementary evidence only** — the .NET rule-based recommendation engine keeps all hard constraints and scoring unchanged.

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

## Retrain (optional)

```bash
python generate_synthetic_dataset.py --n 3000 --out parcels_dataset.csv
python train_random_forest.py --data parcels_dataset.csv --outdir output
```

Replace synthetic data with real labelled exports when available.
