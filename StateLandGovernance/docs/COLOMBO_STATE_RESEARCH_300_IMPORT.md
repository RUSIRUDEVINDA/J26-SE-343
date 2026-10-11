# Colombo state research 300-parcel import

Isolated research dataset under `data/gis/experiments/colombo/state_research_300/`.
Real boundaries and source-reported metadata; **lease availability remains Unknown**.
Simulated workflow statuses live in a separate table and are exposed only when research-demo mode is enabled.

## Database (required)

Use **only** the isolated database:

```text
LAND_INTELLIGENCE_STATE_RESEARCH_CONNECTION=Host=localhost;Port=5432;Database=land_intel_colombo_state_research;Username=postgres;Password=<local>
```

The importer refuses any other database name (including `land_intel_colombo_real_demo`, `land_intel_colombo_real_pilot`, and `land_intel_colombo_demo`).

Point the running API at the same data by setting repository-root `.env`:

```text
LAND_INTELLIGENCE_CONNECTION=<same connection string as above>
```

## Dry-run validation

```powershell
cd StateLandGovernance
dotnet run --project tools/ColomboStateResearch300Import -- `
  --dry-run `
  --report outputs/colombo-state-research-300-dry-run.json
```

## Apply import

```powershell
$env:LAND_INTELLIGENCE_STATE_RESEARCH_CONNECTION = "Host=localhost;Port=5432;Database=land_intel_colombo_state_research;Username=postgres;Password=<local>"
dotnet run --project tools/ColomboStateResearch300Import -- `
  --apply --create-database `
  --connection LAND_INTELLIGENCE_STATE_RESEARCH_CONNECTION `
  --report outputs/colombo-state-research-300-import.json
```

Re-running the apply command updates existing rows (no duplicate parcels). A second apply should report `Inserted: 0`, `Updated: 300`, and `DatabaseParcelCount: 300`.

## Coordinates

- `centroid_latitude` / `centroid_longitude` → stored on `land_parcels."Centroid"` (EPSG:4326, longitude/latitude axis order in PostGIS).
- `map_marker_latitude` / `map_marker_longitude` → stored on `real_parcel_sources."MapMarkerLatitude"` / `"MapMarkerLongitude"` for display only.
- Boundaries come from `colombo_state_parcels_300_boundaries.geojson` unchanged (Polygon/MultiPolygon).

## GIS enrichment (optional, after import)

Reference layers are **not** bundled in the 300-parcel export. To assess road, water, soil, or environmental evidence, import Colombo GIS layers into the **same isolated database**, add `Colombo` to `LandIntelligence:GisEnrichmentCoverage:SupportedDistrictNames` in API configuration, then run parcel enrichment (same services as the Colombo demo setup). Missing layers remain **Unavailable**; measurements stay null.

## Research-demo mode (simulated lease statuses)

| Layer | Flag / setting |
| --- | --- |
| API | `LandIntelligence:ResearchDemo:Enabled` = `true` in `appsettings.Development.json` or environment `LandIntelligence__ResearchDemo__Enabled=true` |
| Web | `NEXT_PUBLIC_LAND_INTELLIGENCE_RESEARCH_DEMO=true` in `StateLandGovernance.Web/.env.local` |

When disabled (default), the API does not return simulated workflow fields. When enabled, the UI shows a banner and a separately labelled simulated status badge; actual availability stays **Not verified**.

## Start API and frontend

```powershell
# Terminal 1 — set LAND_INTELLIGENCE_CONNECTION to land_intel_colombo_state_research in repo-root .env
cd StateLandGovernance
.\run-api.ps1

# Terminal 2
cd StateLandGovernance.Web
npm run dev
```

## Recommendations

The rule engine evaluates up to **2000** candidates (`MaxCandidates` in `RuleBasedLandRecommendationEngine`). For this 300-parcel database, all matching candidates are evaluated before top results are returned.
