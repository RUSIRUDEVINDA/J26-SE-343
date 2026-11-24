# Land Intelligence ML

The only retained suitability model is the [Colombo 400 expert-reviewed two-stage research model](COLOMBO400_RESEARCH_INFERENCE.md). It is supplementary evidence; the .NET recommendation score, hard restrictions, lease status, officer assessment, and authority decision remain separate.

The earlier synthetic-trained standard model and GIS experiment model artifacts have been removed. Do not use their historical evaluation figures as the accuracy of the current system.

## Model

- Stage A: Gradient Boosting checks whether evidence is sufficient.
- Stage B: Random Forest predicts Suitable, Moderately Suitable, or Unsuitable only when Stage A finds sufficient evidence.
- Inputs: reviewed parcel features, the requested purpose, and a check that the current parcel area matches the reviewed area.
- Coverage: the 400 reviewed Colombo parcels. Unknown or changed parcels receive no model prediction.
- Evaluation: 95.63% chained accuracy on 61 held-out parcels (183 purpose assessments) from the reviewed dataset. No independent external validation has been completed.

## Run locally

```powershell
cd StateLandGovernance/src/Modules/LandIntelligence/ML
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r requirements_colombo400.txt
.\.venv\Scripts\python.exe -m uvicorn colombo400_ml_service:app --host 127.0.0.1 --port 8502
```

The .NET application uses `COLOMBO400_RESEARCH_ML_URL` if set, otherwise `http://localhost:8502`. When the service is unavailable, rule-based recommendations continue without ML evidence.

Read [COLOMBO400_RESEARCH_INFERENCE.md](COLOMBO400_RESEARCH_INFERENCE.md) for artifact provenance, serving checks, limitations, and the independent validation protocol.
