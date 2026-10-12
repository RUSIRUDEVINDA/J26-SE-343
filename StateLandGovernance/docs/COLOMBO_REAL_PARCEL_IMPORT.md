# Colombo real parcel GIS import

The primary source is `data/gis/source/Colombo_District_GIS.zip`, layer
`Colombo_GIS/Cadastre_Parcels_Landuse_classified___Land_Use_0.geojson`.
The Survey Department SLCadastre page and the iLAS ArcGIS applications all point to
`Publishing_Server/LUSE_Cadastre_2026/MapServer/0`, so the package already contains the current public parcel source referenced by all three supplied links.

**This dataset is source-reported cadastral/GIS data.** It supports parcel mapping and
source-reported land-use intelligence. It is **not** proof of ownership, legal
availability, lease eligibility, planning approval, or suitability. The source page
says reuse beyond public reference is subject to approval by the Survey Department
and Registrar General's Department. Claimant names and other PII are never imported.

## Recommended database for the normal UI/API

Set repository-root `.env`:

```text
LAND_INTELLIGENCE_CONNECTION=Host=localhost;Port=5432;Database=land_intel_colombo_real_demo;Username=postgres;Password=<local>
```

(`land_intel_colombo_real_pilot` remains valid for the 150-parcel pilot.) Do **not**
use `land_intel_colombo_demo` for normal frontend demos — that database holds
`DEMO-*` test fixtures only.

## Dry-run import

The importer is dry-run by default. It streams the GeoJSON, validates Polygon/MultiPolygon
geometry, checks intersection with the Colombo boundary, computes WGS84 centroids and
geodesic area, joins GN/DS names from `adm_gn_ix`, and records a JSON report with SHA-256
hashes. It never reads or stores `pcl_if_clm`, `created_user`, `last_edited_user`, or
other operator/person fields.

```powershell
cd StateLandGovernance
dotnet run --project tools/ColomboCadastreImport -- `
  --source-zip data/gis/source/Colombo_District_GIS.zip `
  --district Colombo --limit 100 --dry-run `
  --report outputs/colombo-real-parcel-dry-run.json
```

## Apply import (real-data DB)

```powershell
$env:LAND_INTELLIGENCE_REAL_CONNECTION = "Host=localhost;Port=5432;Database=land_intel_colombo_real_demo;Username=postgres;Password=<local>"
dotnet run --project tools/ColomboCadastreImport -- `
  --source-zip data/gis/source/Colombo_District_GIS.zip `
  --district Colombo --limit 150 --apply --create-database `
  --connection LAND_INTELLIGENCE_REAL_CONNECTION `
  --report outputs/colombo-real-parcel-import.json
```

Omit `--limit` only after pilot map/API checks pass. The source ZIP is ignored by Git
and must remain a local research input.

Source-reported land use is stored with official `pcl_if_luc` code, decoded English
label from the layer coded-value domain, optional source text (`pcl_if_lu`), and
provenance. Domain `LandCategoryType` / `LandUseType` are **not** used as the displayed
land use for these records.

## Remove accidental DEMO-* rows

```powershell
# Dry-run
.\tools\RemoveSyntheticLandParcels\Remove-SyntheticLandParcels.ps1 `
  -ConnectionString $env:LAND_INTELLIGENCE_CONNECTION

# Delete allowlisted DEMO-* parcels only (requires -Force)
.\tools\RemoveSyntheticLandParcels\Remove-SyntheticLandParcels.ps1 `
  -ConnectionString $env:LAND_INTELLIGENCE_CONNECTION `
  -Force
```

## Start API + frontend (real data)

```powershell
# Terminal 1
cd StateLandGovernance
.\run-api.ps1

# Terminal 2
cd StateLandGovernance.Web
npm run dev
```

Verify: `GET /api/v1/land/parcels?district=Colombo` returns imported cadastral IDs and
no `DEMO-*` records. Filter with `sourceReportedLandUse=<code|English label>`.
