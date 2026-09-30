# Land Intelligence ML — GIS tools

Offline preparation scripts for GIS-backed experiments. These do **not** modify the live .NET backend, PostgreSQL imports, or the served Random Forest model unless you explicitly run the local-dev import commands below against an **isolated** database.

## OSM motor roads (Colombo experiment)

Filters `data/gis/LandIntelligence_GIS/osm/roads.gpkg` (under the `StateLandGovernance` solution folder) to mapped motor-road linework nationwide (spatial index for nearest-road queries near Colombo across district boundaries).

**Install (Windows):**

```powershell
cd "C:\Users\Ravindu Peiris\Documents\GitHub\J26-SE-343\StateLandGovernance"
py -m pip install -r src/Modules/LandIntelligence/ML/tools/requirements-tools.txt
```

**Prepare** (run from the `StateLandGovernance` folder that contains `StateLandGovernance.sln`):

```powershell
cd "C:\Users\Ravindu Peiris\Documents\GitHub\J26-SE-343\StateLandGovernance"
py src/Modules/LandIntelligence/ML/tools/prepare_osm_motor_roads.py
```

**Output:** `data/gis/experiments/colombo/osm_motor_roads.gpkg` and `osm_motor_roads_manifest.json` (under gitignored `data/gis/`). Paths resolve from the solution directory (where `StateLandGovernance.sln` lives), not from `ML/tools/`.

**Export GeoJSON for C# import** (separate file; does not write to PostgreSQL):

```powershell
cd "C:\Users\Ravindu Peiris\Documents\GitHub\J26-SE-343\StateLandGovernance"
py src/Modules/LandIntelligence/ML/tools/export_osm_motor_roads_geojson.py
```

Writes `data/gis/experiments/colombo/osm_motor_roads.geojson` (+ `.export.json` sidecar). Verify with:

```powershell
Test-Path "C:\Users\Ravindu Peiris\Documents\GitHub\J26-SE-343\StateLandGovernance\data\gis\experiments\colombo\osm_motor_roads.geojson"
```

**Tests:**

```powershell
cd "C:\Users\Ravindu Peiris\Documents\GitHub\J26-SE-343"
py -m unittest discover -s StateLandGovernance/src/Modules/LandIntelligence/ML/tools -p "test_*.py"
```

Distances for experiments should use **geodesic metres** (WGS84 geography), not planar degrees. Output describes **mapped motor-road proximity**, not verified vehicle access (OSM `access` tags were not exported). Offline UTM-vs-geodesic tolerance used in tools tests: **5 m**.

## Local-dev backend GIS alignment (inactive experimental path)

Canonical documentation and readiness assessment:
[`../docs/COLOMBO_EXPERIMENTAL_GIS_ML.md`](../docs/COLOMBO_EXPERIMENTAL_GIS_ML.md).

Default configuration preserves Hambantota + `expressways`. Experimental Colombo alignment is **opt-in** via config and import; the live `ml_service.py` / production joblib path is unchanged and the experimental adapter sets `ModelActivationEnabled=false`.

### Required configuration (`appsettings` / Development)

```json
"LandIntelligence": {
  "GisEnrichmentCoverage": {
    "SupportedDistrictNames": [ "Hambantota" ],
    "RoadSourceLayer": "expressways",
    "OsmMotorRoadFilterPolicyVersion": "2026-03-20-colombo-v1"
  }
}
```

For Colombo local experiments on an **isolated** DB only — use **district-specific** road layers so enabling Colombo OSM does not change Hambantota expressways:

```json
"SupportedDistrictNames": [ "Hambantota", "Colombo" ],
"RoadSourceLayer": "expressways",
"DistrictRoadSourceLayers": [
  { "DistrictName": "Hambantota", "RoadSourceLayer": "expressways" },
  { "DistrictName": "Colombo", "RoadSourceLayer": "osm_motor_roads" }
]
```

`RoadSourceLayer` alone is the **global/fallback** setting (default `expressways`). It is not automatically district-scoped; use `DistrictRoadSourceLayers` for Colombo OSM.

### Road import counts (262,911 → 82,480)

| Stage | Count | Meaning |
| --- | --- | --- |
| `prepare_osm_motor_roads.py` → `osm_motor_roads.gpkg` | **262,911** | Nationwide (prepared) motor highways after policy filter + geometry validation |
| `export_osm_motor_roads_geojson.py --district-buffer-km 25` | **82,480** | Roads intersecting Colombo district **+ 25 km buffer** (intentionally includes roads outside the district for cross-boundary nearest-road) |
| PostGIS `OsmMotorRoadImportService` | **82,480** | Idempotent upsert of that GeoJSON (`SourceFeatureId=osm_id`); no extra highway filter |

There is **no undocumented second highway filter** on import. The reduction is the **documented local-dev buffer clip**, not a semantics change: nearest-road enrichment still searches the imported OSM layer without clipping results to Colombo.

### Backend-compatible experimental candidate (offline)

Preserves `output_colombo_osm/`. Writes a new directory:

```powershell
cd "C:\Users\Ravindu Peiris\Documents\GitHub\J26-SE-343\StateLandGovernance"
py src\Modules\LandIntelligence\ML\train_colombo_osm_backend_compatible.py
```

Artifacts: `src/Modules/LandIntelligence/ML/output_colombo_osm_backend_compatible/`.

Predictors: `area_hectares`, `distance_to_road_m`, `distance_to_water_m`, `requested_purpose`, `land_category`, `gis_enrichment_status`, `derived_soil_group`, `spatial_constraint_present`.

Excluded: `environmental_restriction_type`, `environmental_restriction_severity` (adapter cannot supply equivalents; abstain instead of fabricating).

Selection: training-only GroupKFold CV macro-F1. Prior held-out scores are **exploratory only**.

Missing evidence: adapter sets `ExperimentalPredictionSupported=false` when conservation assessment is unavailable (`spatial_constraint_present` stays null — never coerced to false). `handle_unknown=ignore` is not treated as predictive validity.

### Isolated PostGIS verification (disposable DB only)

Never point this at `state_land_governance` or any shared app database. From the **solution root** (`StateLandGovernance/`):

```powershell
# 1) Create disposable DB (requires local PostgreSQL + PostGIS; Docker optional)
powershell -ExecutionPolicy Bypass -File "tests\IntegrationTests\LandIntelligence\ColomboExperimentalGisVerify\New-ColomboExpIsolatedDatabase.ps1"

# 2) Set the printed connection (example — use the value printed by step 1)
$env:COLUMBO_EXP_ISOLATED_CONNECTION = "Host=localhost;Port=5432;Database=land_intel_colombo_exp_iso;Username=postgres;Password=postgres"

# 3) Export buffered OSM GeoJSON if missing, migrate, import, enrich, rollback, write report
powershell -ExecutionPolicy Bypass -File "tests\IntegrationTests\LandIntelligence\ColomboExperimentalGisVerify\Invoke-ColomboExpGisVerify.ps1"

# 4) Offline sensitivity (held-out Unknown env features) — does not retrain or activate
py src\Modules\LandIntelligence\ML\analyze_colombo_env_unknown_sensitivity.py

# 5) Drop disposable DB when finished
powershell -ExecutionPolicy Bypass -File "tests\IntegrationTests\LandIntelligence\ColomboExperimentalGisVerify\Remove-ColomboExpIsolatedDatabase.ps1"
```

Report path: `data/gis/experiments/colombo/colombo_exp_isolated_verify_report.json`.

### Import (isolated DB only — do not run against shared/prod)

Prefer the PowerShell workflow above. The verify harness calls:

1. Hambantota pilot GIS import (expressways + related pilot layers).
2. Colombo district boundary import.
3. OSM motor-road GeoJSON import (`SourceLayer=osm_motor_roads`, idempotent on `SourceFeatureId=osm_id`).
4. Colombo-intersecting water / soil / conservation import.
5. Enrichment probes + OSM-only rollback (`DeleteOsmMotorRoadsAsync`).

**Rollback (local-dev):** removes only `osm_motor_roads` rows; expressways remain.

### Coverage semantics

| Outcome | Meaning |
| --- | --- |
| `Available` | Inside coverage and source data assessed |
| `OutsideCoverage` | Outside configured district boundaries |
| `Unavailable` | Inside coverage but source data missing / failed |

Missing distances stay `null` (never coerced to `0`). Successful enrichment is not reported when source data is unavailable.

### Feature mapping (experimental ↔ backend)

| Experimental feature | Persisted source | Backend calculation | Missing handling | Outgoing value |
| --- | --- | --- | --- | --- |
| `distance_to_road_m` | `gis_roads` where `SourceLayer=osm_motor_roads` | Geography KNN + `ST_Distance` geography (metres) | OutsideCoverage / Unavailable → null | metres or null |
| `distance_to_water_m` | `gis_water_features` canals/lakes | Geography nearest canal/lake | null; never WaterSupply | metres or null |
| `derived_soil_group` | `gis_soil_groups` | Intersection / PIP | `Unknown` | soil group name |
| `spatial_constraint_present` | `gis_soil_conservation_areas` | Intersection bool | null when assessment unavailable; False ≠ unconstrained | bool? |
| `gis_enrichment_status` | section statuses | Experimental `Assessed*` enum (distinct from domain Complete/Partial) | Unavailable | AssessedComplete / AssessedPartial / Unavailable |
| `environmental_restriction_*` | training simulated | **Not fabricated** by adapter | `Unknown` | Unknown |
| `area_hectares` / `land_category` / `requested_purpose` | parcel / request | Backend actual attributes | must exist on parcel | enum/string/number |

## Colombo GIS-grounded suitability dataset

After motor-road preparation, generate a reproducible offline dataset of **sampled locations** (not cadastral parcels) with GIS-derived `distance_to_road_m` / water / soil and **simulated** labels:

```powershell
cd "C:\Users\Ravindu Peiris\Documents\GitHub\J26-SE-343\StateLandGovernance"
py src/Modules/LandIntelligence/ML/generate_gis_grounded_dataset.py --n 1500 --district Colombo --seed 42 --out data/gis/experiments/colombo/parcels_colombo_osm_v2.csv
```

Prefer `*_v2.csv` (AssessedComplete status + corrected conservation intersection). Do not overwrite `parcels_colombo_osm.csv` without an explicit new `--out`.

### Offline train / evaluate (does not touch live model)

```powershell
cd "C:\Users\Ravindu Peiris\Documents\GitHub\J26-SE-343\StateLandGovernance"
py src/Modules/LandIntelligence/ML/train_colombo_osm.py --data data/gis/experiments/colombo/parcels_colombo_osm_v2.csv --outdir output_colombo_osm
```

Artifacts land in `src/Modules/LandIntelligence/ML/output_colombo_osm/` (relative to the script’s working directory / `--outdir` resolution — verify with `Test-Path` after the run).
