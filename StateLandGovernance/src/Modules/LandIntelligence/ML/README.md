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

`GET /health` → `model_identity.candidate` and `model_activation_enabled=false`
(artifact is not production-activated; this is **not** “predictions cannot run”
when opt-in `Enabled=true`).

Do **not** replace `ml_service.py` (port 8500) or live `output/` joblib.

### One-shot disposable Step-3

```powershell
cd StateLandGovernance
.\tests\IntegrationTests\LandIntelligence\ColomboExperimentalMlE2EVerify\Invoke-ColomboExpStep3E2E.ps1
```

Full-system `StateLandGovernance.Api` remains blocked by WorkflowGovernance
`ILeaseCaseRepository` DI (see `data/gis/experiments/colombo/step3_api.log.err`).
LandIntelligence-only HTTP verification uses port **5265** inside the Step-3 harness.
