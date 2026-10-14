# Colombo experimental GIS / ML path — documentation and readiness

**Readiness decision:** Available for opt-in local research demonstration; **not** validated for real-world suitability decisions.

**Default activation:** `LandIntelligence:ExperimentalColomboMl:Enabled` is `false` in
`src/Api/appsettings.json` and `src/Api/appsettings.Development.json`. Production
`ml_service.py` (port 8500) and live `ML/output/` artifacts are not replaced by this path.

This document records what was implemented and what was actually verified from on-disk
code, configuration, metrics, and verification reports. It does **not** claim
full-system E2E success through `StateLandGovernance.Api`.

---

## 1. Implementation

### 1.1 GIS sources, OSM snapshot, road filter, 25 km buffer

| Item | Value (from artifacts / code) |
| --- | --- |
| OSM roads source | `data/gis/LandIntelligence_GIS/osm/roads.gpkg` (prepared offline) |
| Filter policy | `2026-03-20-colombo-v1` (`ML/tools/osm_motor_road_policy.py`) |
| OSM snapshot (manifest) | `2026-09-09` (`parcels_colombo_osm_v2_manifest.json` → `road_network.osm_snapshot`) |
| License | HDX / ODbL (`hdx-odc-odbl`) |
| Prepared nationwide GPKG | `data/gis/experiments/colombo/osm_motor_roads.gpkg` — **262,911** features |
| Colombo+25 km import GeoJSON | `osm_motor_roads_colombo_buffer.geojson` — **82,480** features (`osm_motor_roads_colombo_buffer.export.json`) |
| Clip note | “Clipped to Colombo + 25.0 km buffer (262911 -> 82480 features). Roads beyond the district remain when they fall inside the buffer.” |
| Other GIS | District boundaries, soil groups, soil conservation areas, canals, lakes under `data/gis/LandIntelligence_GIS/` |
| Distance semantics | Mapped motor-road **proximity** only; OSM access tags were not exported (`PROXIMITY_DISCLAIMER` in policy) |

District-specific road layers (isolated / experimental DI, not default appsettings):

- Global fallback: `expressways`
- Hambantota → `expressways`
- Colombo → `osm_motor_roads`

Default committed appsettings keep **Hambantota only** + `expressways`.

### 1.2 Sampled locations vs cadastral parcels

From `parcels_colombo_osm_v2_manifest.json`:

- `sample_kind`: `sampled_locations_inside_district_boundary_not_cadastral_parcels`
- `sample_count` / rows: **1500**
- Sampling: uniform rejection sampling inside Colombo district polygon
- Offline training rows are **not** official cadastral parcels

Disposable PostGIS verification and the LandIntelligence-only HTTP sample use
**synthetic probe parcels** (cadastral IDs such as `COL-E2E-ML-SUPPORTED`) with
real enrichment against imported GIS layers — still not Land Commissioner
cadastral truth.

### 1.3 GIS-derived vs simulated features and labels

Manifest `dataset_kind`:

> real GIS-derived features with simulated attributes/labels; not observed real-world suitability ground truth

**GIS-derived (typical):** lat/lon, province/district, `distance_to_road_m`, nearest-road OSM metadata, `distance_to_water_m`, `derived_soil_group`, `spatial_constraint_present` (soil-conservation intersection), `gis_enrichment_status` (Assessed*).

**Simulated / non-GIS (typical):** `area_hectares`, `requested_purpose`, `land_category`, `environmental_restriction_type` / `severity`, `suitability_label` (rule-simulated).

Backend-compatible training **excludes** `environmental_restriction_type` and
`environmental_restriction_severity` as predictors (`feature_schema.json`).

### 1.4 Exact backend-compatible feature schema

Candidate id: **`colombo_osm_backend_compatible`**

Artifact dir: `src/Modules/LandIntelligence/ML/output_colombo_osm_backend_compatible/`

| Kind | Features |
| --- | --- |
| Numeric | `area_hectares`, `distance_to_road_m`, `distance_to_water_m` |
| Categorical | `requested_purpose`, `land_category`, `gis_enrichment_status`, `derived_soil_group`, `spatial_constraint_present` |

Also recorded in schema:

- `model_activation_enabled`: **false**
- Serving abstains when `spatial_constraint_present` is null (do not coerce to false)
- `handle_unknown=ignore` is an engineering fallback, not predictive validity

### 1.5 Missing-evidence and abstention

`ExperimentalColomboMlFeatureAdapter` and `ExperimentalColomboMlEvidenceService`:

- Conservation assessment unavailable → `SpatialConstraintPresent = null` →
  `ExperimentalPredictionSupported = false` → **no** `/predict` call; evidence
  criterion `ExperimentalColomboMlAbstention`
- Missing road distance (when that is the abstention reason) similarly blocks prediction
- Soft failures (timeout / malformed / candidate mismatch) →
  `ExperimentalColomboMlUnavailable` evidence; rule-based recommendation retained
- Hard constraint rejection → neither production nor experimental ML is called

### 1.6 Separate experimental service (port 8501)

- Entry: `ML/experimental_ml_service.py` (uvicorn; default port **8501**)
- Loads **only** `output_colombo_osm_backend_compatible`
- Env: `EXPERIMENTAL_COLOMBO_ML_PORT`, `EXPERIMENTAL_COLOMBO_ML_MODEL_DIR`
- Health: `GET /health` → `model_identity.candidate`, `model_activation_enabled`, etc.
- Does **not** replace `ml_service.py` on **8500**

#### Meaning of `model_activation_enabled=false`

This field means the artifact is **not** the production/default suitability model
and must not be treated as activated for real decisions.

It does **not** mean “no experimental prediction can run.” When:

1. `experimental_ml_service` is up on 8501, **and**
2. `LandIntelligence:ExperimentalColomboMl:Enabled=true` (opt-in),

the backend may still attach **supplementary** experimental evidence. Distinguish:

| Concept | Meaning |
| --- | --- |
| Service availability | Process listening on 8501; `/health` returns ok |
| Opt-in experimental use | .NET `Enabled=true` for a local process |
| Default / production activation | Remains **off**; live joblib / port 8500 unchanged |

### 1.7 Rule-based scores and hard constraints remain authoritative

`RuleBasedLandRecommendationEngine` computes suitability score and hard
constraints first. Experimental output is appended as evidence only
(`Source: ExperimentalColomboOsmRf`). When experimental is enabled,
`SuppressProductionMlWhenEnabled` (default true) skips production `IMlSuitabilityClient`
so both models are not called together.

---

## 2. Evaluation

All metrics below measure agreement with **simulated** `suitability_label` values.
They are **not** validated real-world accuracy. Model class probabilities are
**not** validated accuracy.

**Model identity:** `colombo_osm_backend_compatible`  
**Serving artifact:** `output_colombo_osm_backend_compatible/random_forest_pipeline.joblib`  
**Metrics file:** `output_colombo_osm_backend_compatible/metrics_summary.json`  
(`generated_at_utc`: `2026-09-20T16:14:00.178893+00:00`)  
**Dataset:** `parcels_colombo_osm_v2.csv`  
**Dataset SHA-256** (metrics + local hash): `ea52bb6df835bc902d768722c696f8ca83be80ecd36929c60442f2d45c0d342f`

### 2.1 Training-only spatial CV (model selection)

Method: GroupKFold on **training** spatial groups; select by mean CV **macro-F1**.

| Model | CV accuracy mean | CV macro-F1 mean |
| --- | ---: | ---: |
| Dummy Majority | 0.458 | 0.209 |
| Logistic Regression | 0.611 | 0.572 |
| **Random Forest (selected)** | **0.649** | **0.656** |

### 2.2 Exploratory held-out (prior reuse)

From `metrics_summary.json` → `split.heldout_role` and `exploratory_heldout_metrics`:

> EXPLORATORY ONLY — this held-out set already informed development via sensitivity analysis.
> Scores here are not an untouched final evaluation.

| Metric | Value |
| --- | ---: |
| Accuracy | 0.608 |
| Macro-F1 | 0.594 |
| Weighted-F1 | 0.581 |
| n_test | 278 |

Split: GroupShuffleSplit, cell 0.02°, seed 42, n_train=1222, group_overlap_count=0.

### 2.3 Environmental-input sensitivity

File: `data/gis/experiments/colombo/colombo_env_unknown_sensitivity.json`  
(Uses **prior** `output_colombo_osm` schema including simulated env predictors — not the backend-compatible serving schema.)

On the same held-out rows (278):

| Probe | Result |
| --- | --- |
| Predictions changed when env type/severity set to Unknown | 49 (fraction ≈ 0.176) |
| Macro-F1 original vs simulated labels | 0.783 |
| Macro-F1 adapter-style Unknown env vs simulated labels | 0.637 |
| Disclaimer (file) | “Sensitivity against simulated labels only — not real-world validation.” |

This analysis motivated excluding those predictors and abstaining when conservation
evidence is missing in the backend-compatible candidate.

---

## 3. Verification matrix

Evidence bases:

- Unit tests under `tests/UnitTests/LandIntelligence/`
- `data/gis/experiments/colombo/colombo_exp_isolated_verify_report.json`
- `data/gis/experiments/colombo/colombo_exp_step3_e2e_report.json` (`passed: true`, `failures: []`)
- `data/gis/experiments/colombo/colombo_exp_step3_swagger_sample.json`
- `data/gis/experiments/colombo/step3_api.log.err` (full Api host failure)
- `data/gis/experiments/colombo/offline_road_distance_check.json`

### Unit-tested

| Area | Tests (examples) |
| --- | --- |
| Feature adapter / abstention | `ExperimentalColomboMlFeatureAdapterTests` (schema keys; no coerce road→0; abstain when conservation unavailable; false ≠ unknown) |
| Recommendation wiring | `ExperimentalColomboMlRecommendationTests` (default off; suppress production ML; hard reject skips ML; abstention/unavailable soft-fail preserve score) |
| Centroid / invalidate (InMemory) | `LandParcelCentroidPersistenceTests` |
| Isolated DB guard | `IsolatedLandIntelligenceConnectionGuardTests` |
| Related GIS enrichment / coverage | Existing Road/Water/Soil/Env enrichment and `GisEnrichmentCoverageOptions` unit tests |

### Verified with disposable PostGIS

Database name token required: `colombo_exp_iso` (report used `land_intel_colombo_exp_iso`).

From isolated GIS verify + step-3 reports:

| Check | Observed |
| --- | --- |
| OSM import count | 82,480 features; filter policy `2026-03-20-colombo-v1`; second import idempotent |
| Colombo layer import | lakes 4, soil_groups 6, soil_conservation_areas 1, canals 0 (sparse layers noted in report) |
| Hambantota road source | `expressways`, status Available (step-3 recommendations) |
| Centroid update + fresh context | Updated lon/lat persisted; snapshot → Unavailable; re-enrich road Available on `osm_motor_roads` |
| Coordinate validation | Partial lat-only and out-of-range lat rejected (`partialCoordValid`/`invalidCoordValid` false) |
| Road-buffer samples | interior / near_boundary / cross_boundary_knn; nearest roads within 25 km of district polygon; max observed nearest ≈ 12.17 m (empirical only) |
| Offline cross-boundary KNN | `offline_road_distance_check.json`: point in Colombo; nearest road adm2 Gampaha; geodesic ≈ 12.17 m |
| Engine recommendation | Supported score 70; abstain parcel evidence path; hard reject `COL-E2E-H-9c049a58` score **0**, `HardConstraintRejected: true`, no experimental evidence |
| Experimental ML health | healthy on `http://127.0.0.1:8501`, expected candidate `colombo_osm_backend_compatible` |

### Verified through LandIntelligence-only HTTP host

Step-3 host on `http://127.0.0.1:5265` (Land Intelligence controllers only):

| Check | Observed |
| --- | --- |
| `POST /api/v1/land/recommendations` | **HTTP 200** |
| Request body | `{"requiredPurpose":1,"preferredLocation":{"district":"Colombo"},"maxResults":10}` |
| Supported parcel | `COL-E2E-ML-SUPPORTED` → `ExperimentalColomboMlPrediction` (e.g. Moderately Suitable ~75%) |
| Abstention | `COL-E2E-ML-ABSTAIN` → `ExperimentalColomboMlAbstention` (conservation unavailable / null spatial constraint) |

### Not executed

| Item | Notes |
| --- | --- |
| Full `StateLandGovernance.Api` E2E | Blocked — see below |
| Live experimental ML timeout / malformed JSON / schema-mismatch chaos | Soft-fail covered in **unit** tests with stubs; not exercised live against uvicorn |
| Nationwide road-buffer correctness proof | Only empirical samples + OutsideCoverage fallback; no exhaustive coverage guard |
| Independent real suitability labels / evaluation | Not available |
| Production model swap / activation | Intentionally not done |

### Known limitations (recorded in reports)

1. **Complete application host blocked:** `step3_api.log.err` shows DI failure resolving
   `ILeaseCaseRepository` for WorkflowGovernance handlers when starting
   `StateLandGovernance.Api`. **Do not claim full-system E2E success.**

2. **`default_off_smoke` in saved step-3 report:** fields show
   `Enabled: true`, `experimentalEvidencePresent: true`,
   `retainedExistingBehavior: false` while overall `passed: true`. That smoke check
   did **not** demonstrate default-off behavior in the saved report (process env
   override interacted with configuration). Committed appsettings still have
   `Enabled: false`. Treat default-off as **config/unit-tested**, not as proven by
   that smoke section.

3. **HTTP sample cadastral `COL-E2E-ML-HARD`:** swagger sample notes show
   `ExperimentalColomboMlPrediction` for that id (leftover probe without prohibitive
   restriction). Engine-path hard rejection was verified on **`COL-E2E-H-9c049a58`**,
   not on `COL-E2E-ML-HARD`.

4. **Road buffer:** step-3 report `guarantee` text states empirical-only; not a general
   correctness proof for every coordinate.

5. **Overall GIS enrichment status** on some Colombo probes reported as Unavailable (3)
   even when road distance Available — overall completeness is stricter than road-only.

---

## 4. Local demonstration runbook

Run from the `StateLandGovernance` solution directory (folder containing
`StateLandGovernance.sln`). Do **not** point `.env` `LAND_INTELLIGENCE_CONNECTION` at
the disposable database for normal app use.

### 4.1 Prerequisites

```powershell
cd <repo>\StateLandGovernance
# PostgreSQL 17+ with PostGIS; Python 3.10+
py -m pip install -r src\Modules\LandIntelligence\ML\requirements.txt
py -m pip install -r src\Modules\LandIntelligence\ML\tools\requirements-tools.txt
```

Requires local GIS under `data/gis/LandIntelligence_GIS/` (not always in git).

### 4.2 Artifact generation (if missing)

```powershell
py src\Modules\LandIntelligence\ML\tools\prepare_osm_motor_roads.py
py src\Modules\LandIntelligence\ML\tools\export_osm_motor_roads_geojson.py --district-buffer-km 25
py src\Modules\LandIntelligence\ML\generate_gis_grounded_dataset.py
# Backend-compatible train (writes output_colombo_osm_backend_compatible/)
py src\Modules\LandIntelligence\ML\train_colombo_osm_backend_compatible.py
```

### 4.3 Disposable database

```powershell
.\tests\IntegrationTests\LandIntelligence\ColomboExperimentalGisVerify\New-ColomboExpIsolatedDatabase.ps1
# Sets process env COLUMBO_EXP_ISOLATED_CONNECTION (no credentials documented here)
$env:LAND_INTELLIGENCE_CONNECTION = $env:COLUMBO_EXP_ISOLATED_CONNECTION
```

### 4.4 GIS imports + GIS verify (optional standalone)

```powershell
.\tests\IntegrationTests\LandIntelligence\ColomboExperimentalGisVerify\Invoke-ColomboExpGisVerify.ps1
# Report: data\gis\experiments\colombo\colombo_exp_isolated_verify_report.json
```

### 4.5 Experimental ML startup

```powershell
cd src\Modules\LandIntelligence\ML
$env:EXPERIMENTAL_COLOMBO_ML_PORT = "8501"
$env:EXPERIMENTAL_COLOMBO_ML_MODEL_DIR = (Resolve-Path .\output_colombo_osm_backend_compatible).Path
py -m uvicorn experimental_ml_service:app --host 127.0.0.1 --port 8501
# Other window:
Invoke-RestMethod http://127.0.0.1:8501/health
# Expect candidate=colombo_osm_backend_compatible and model_activation_enabled=false
```

### 4.6 One-shot Step-3 (DB + ML + LI host + cleanup)

```powershell
cd <repo>\StateLandGovernance
.\tests\IntegrationTests\LandIntelligence\ColomboExperimentalMlE2EVerify\Invoke-ColomboExpStep3E2E.ps1
# Keep DB: add -SkipCleanup
```

This script starts experimental ML, runs `ColomboExperimentalMlE2EVerify` (PostGIS checks,
engine scenarios, LandIntelligence-only HTTP on **:5265**), then stops ML and drops the
disposable DB unless `-SkipCleanup`.

### 4.7 Opt-in LandIntelligence host + recommendation request

The Step-3 harness already posts:

```http
POST http://127.0.0.1:5265/api/v1/land/recommendations
Content-Type: application/json

{"requiredPurpose":1,"preferredLocation":{"district":"Colombo"},"maxResults":10}
```

Sample response path:
`data/gis/experiments/colombo/colombo_exp_step3_swagger_sample.json`

**Abstention:** inspect evidence for `COL-E2E-ML-ABSTAIN` /
`ExperimentalColomboMlAbstention`.

**Hard rejection:** engine report entry `COL-E2E-H-*` with
`HardConstraintRejected: true` and score `0` (prefer report over leftover
`COL-E2E-ML-HARD` cadastral in HTTP sample notes).

### 4.8 Shutdown and disposable cleanup

```powershell
# Stop uvicorn / LI host processes if still running
.\tests\IntegrationTests\LandIntelligence\ColomboExperimentalGisVerify\Remove-ColomboExpIsolatedDatabase.ps1
Remove-Item Env:COLUMBO_EXP_ISOLATED_CONNECTION -ErrorAction SilentlyContinue
# Restore normal .env LAND_INTELLIGENCE_CONNECTION (application DB) — do not leave
# process overrides pointing at colombo_exp_iso
```

### 4.9 Rollback to default configuration

- Ensure `LandIntelligence:ExperimentalColomboMl:Enabled` is **false** (committed default).
- Do not set `LandIntelligence__ExperimentalColomboMl__Enabled=true` in the process environment.
- Use production ML URL/`ml_service.py` on **8500** only if you intend the live supplementary path.
- Leave `SupportedDistrictNames` as Hambantota-only unless deliberately experimenting on an isolated DB.

---

## 5. Readiness decision and remaining work

### Decision

> **Available for opt-in local research demonstration;  
> not validated for real-world suitability decisions.**

### Remaining work

1. **Full application-host DI repair** for WorkflowGovernance `ILeaseCaseRepository`, then
   integration verification through Swagger on `StateLandGovernance.Api`.
2. **Live** timeout / malformed response / schema-mismatch fault verification against
   `experimental_ml_service` (beyond unit stubs).
3. **Road-buffer coverage guard** or stronger documented assurance beyond empirical samples.
4. **Independent suitability labels** and evaluation that did not inform development.

---

## 6. Reproducibility

### Obtaining / regenerating gitignored data and models

Large GIS and experiment outputs typically live under `StateLandGovernance/data/gis/`
(and may be gitignored). Teammates regenerate with the PowerShell commands in §4.2,
or copy shared offline packs that match the checksums below. Model joblib files under
`ML/output_colombo_osm_backend_compatible/` may need regeneration via
`train_colombo_osm_backend_compatible.py` if not shared.

Do **not** commit credentials. Connection strings stay in local `.env` / process env.

### Artifact checksums and paths (local workstation, 2026-09-20)

| Artifact | SHA-256 |
| --- | --- |
| `parcels_colombo_osm_v2.csv` | `ea52bb6df835bc902d768722c696f8ca83be80ecd36929c60442f2d45c0d342f` |
| `output_colombo_osm_backend_compatible/feature_schema.json` | `783B45D42C03CF946B363B640627E013C7FE411C4AA94DCF491712B2760817AA` |
| `output_colombo_osm_backend_compatible/metrics_summary.json` | `970CD13FA6B9F8F3302DE6D1D5F4F34792E41871EFC43190A1D312DB4272DC99` |
| `output_colombo_osm_backend_compatible/random_forest_pipeline.joblib` | `64745A65AFEFF9C933D70F00A452CEB6C4FC7A87403354690D18AD7FBF622EEB` |
| `osm_motor_roads_colombo_buffer.geojson` | `23CACFF0E84BC5CEE04EAC045DD6E52B630430CF04F3B70D87D4F8D9F08983E5` |
| `colombo_exp_step3_e2e_report.json` | `9FA27007460FAA3E689E02B5903942D121C59F3B5429B02FA6CF4F8E35AF2398` |

Versions / ids:

- Candidate: `colombo_osm_backend_compatible`
- Filter policy: `2026-03-20-colombo-v1`
- OSM snapshot (dataset manifest): `2026-09-09`
- Experimental default port: `8501`
- Production ML port (untouched): `8500`

Related reports for audit:

- `data/gis/experiments/colombo/colombo_exp_isolated_verify_report.json`
- `data/gis/experiments/colombo/colombo_exp_step3_e2e_report.json`
- `data/gis/experiments/colombo/colombo_exp_step3_swagger_sample.json`
- `data/gis/experiments/colombo/colombo_env_unknown_sensitivity.json`
- `data/gis/experiments/colombo/offline_road_distance_check.json`
- `ML/output_colombo_osm_backend_compatible/EVALUATION_REPORT.md`

---

## Evidence reviewed for this document

- `feature_schema.json`, `metrics_summary.json`, `EVALUATION_REPORT.md` (backend-compatible)
- `parcels_colombo_osm_v2_manifest.json`, `osm_motor_roads_colombo_buffer.export.json`
- `colombo_env_unknown_sensitivity.json`, `offline_road_distance_check.json`
- `colombo_exp_isolated_verify_report.json`, `colombo_exp_step3_e2e_report.json`,
  `colombo_exp_step3_swagger_sample.json`, `step3_api.log.err`
- `ExperimentalColomboMlOptions`, appsettings defaults, `experimental_ml_service.py`
- Unit test class names listed in §3
- `ML/tools/osm_motor_road_policy.py`, tools README import counts
