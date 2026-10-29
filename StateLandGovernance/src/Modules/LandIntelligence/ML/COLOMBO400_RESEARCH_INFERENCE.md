# Colombo 400 two-stage research inference

This is an opt-in Component 1 research model. It uses the 400-parcel expert-reviewed
workbook and does **not** replace the rule score, hard restrictions, lease status,
officer assessment, or authority decision. The existing standard ML service remains
the default when `COLOMBO400_RESEARCH_ML_ENABLED` is unset.

## Frozen inputs and provenance

- Source workbook: `Colombo_400_Parcel_Expert_Label_Review_Completed_v2.xlsx`.
- Workbook SHA-256: `29ded69d3645ff803c01a0dcda247324076ee3713e65ac4b8908e406deb4021f`.
- The model files and metadata were taken from `colombo400_ml_artefacts_v2.zip`.
- `colombo400_artefacts/inference_features.json` contains **400 distinct parcel feature rows**,
  without expert labels, notes, recommendation scores, or lease status.
- `colombo400_artefacts/cleaned_features.csv` is an audit copy of the Colab export
  with the duplicated `Intended Purpose` column removed. It is not read at inference.
- Model features and order are checked against `config.json` at service startup.
- The service checks the frozen feature file hash and refuses unknown cadastral numbers
  or parcels whose current area differs from the reviewed area.

The service takes cadastral number, intended purpose, and current parcel area. It
looks up the **frozen reviewed attributes** rather than attempting to reconstruct
missing mapped GIS coverage from the live database. New or changed parcels need a
separate, versioned feature preparation and independent review before they can be
served by this model.

## Start locally

From `StateLandGovernance/src/Modules/LandIntelligence/ML`:

```powershell
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r requirements_colombo400.txt
.\.venv\Scripts\python.exe -m uvicorn colombo400_ml_service:app --host 127.0.0.1 --port 8502
```

Use a Python installation that supports the pinned training packages. On the API
host, set these values in the repository `.env`, then restart the API:

```dotenv
COLOMBO400_RESEARCH_ML_ENABLED=true
COLOMBO400_RESEARCH_ML_URL=http://127.0.0.1:8502
```

For a local research demonstration, the flag may be enabled while predictions remain clearly labelled as research evidence. Keep it disabled for production use until an independent expert-labelled validation set has been
reviewed. When enabled, only Colombo parcels with a research record ID and a matching
frozen cadastral number can receive this research prediction. The model service returns
`Insufficient Evidence` from Stage A without running Stage B. It rejects a Commercial
`Suitable` prediction with HTTP 409; the .NET adapter independently refuses that
result as a second guard. Service errors leave rule-based recommendations available.
Keep `ExperimentalColomboMl:Enabled` off for this path; when that separate experiment
suppresses the standard ML client, it also suppresses this opt-in client.

Check `GET http://127.0.0.1:8502/health` for the loaded model and parcel count.

## Recorded evaluation, not external validation

The supplied artifact reports a parcel-grouped test split of 61 parcels (183
purpose assessments): Stage A accuracy 95.63%, conditional Stage B accuracy 97.60%,
and chained accuracy 95.63%. Stage A made two false-sufficiency decisions. These
numbers reproduce the *report in the supplied archive*; they do not measure
performance on independently collected parcels, and the models have not been
retrained here. The saved joblib files should only be loaded from this trusted archive.

Before using ML beyond this research demonstration, have a domain expert label a
new, untouched set of parcels with all three purposes and the same four approved
labels. Record the source and review date. Prepare identical features without
copying labels into model inputs. Evaluate the frozen model once by parcel, reporting
Stage A false sufficiency, end-to-end macro F1, per-purpose errors, and Commercial
policy violations. Do not tune the model on that holdout; any retraining requires
another untouched test set. A suitability label is never lease approval.

An evaluator is provided for that future review. Export a CSV with
`Cadastral Number`, `Suitability Label`, and the eight exact columns in
`config.json` (including `Intended Purpose`); keep one row per parcel and purpose.
It refuses parcels already present in the training 400 and duplicate assessments:

```powershell
.\.venv\Scripts\python.exe validate_colombo400_external.py new_expert_review.csv --report external_validation.json
```

No external validation result exists yet because no new expert-labelled parcels
were supplied. Do not present the archived 95.63% as independent real-world accuracy.
