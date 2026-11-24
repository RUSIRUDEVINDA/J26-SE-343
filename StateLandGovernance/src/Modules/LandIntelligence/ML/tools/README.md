# Land Intelligence ML — GIS tools

Offline preparation scripts for GIS-backed research. These do **not** modify the live .NET backend, PostgreSQL imports, or the retained Colombo 400 research model unless you explicitly run the local-dev import commands below against an **isolated** database. The older synthetic-labelled ML experiment described in linked historical notes has been retired.

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

Default GIS configuration preserves Hambantota + `expressways`. These GIS preparation tools are separate from the retained Colombo 400 expert-reviewed ML model. The former synthetic-labelled experimental ML service and model artifacts have been removed.

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

The synthetic-labelled ML experiments and their generated model artifacts have been retired. These GIS tools do not provide features to the frozen Colombo 400 research model.
