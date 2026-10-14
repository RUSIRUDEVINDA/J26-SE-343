# Colombo State research supplement: 100 additional parcels

This is a separate addition to the existing 300-parcel research dataset. The original 300 CSV, import logic, and project lease-ready designations are unchanged. The supplement imports into `land_intel_colombo_state_research` only and adds 100 separate project lease-ready assumptions.

## Contents and classification

| Research group | Records | How assigned |
| --- | ---: | --- |
| Wetland | 38 | Original cadastral label or at least 80% mapped LUPPD wetland coverage |
| Scrub / low jungle | 26 | Original cadastral label or at least 80% mapped LUPPD scrub coverage |
| Coconut | 20 | At least 80% mapped LUPPD coconut coverage |
| Other State parcel | 16 | Eight Homegarden, four Paddy Field, four Open Waste Land source labels |
| Forest | 0 | No additional Colombo parcel passed all selection checks |

The Survey Department cadastral `pcl_if_clc=2` value is **source-reported State**, not an independent title check. It does not prove a parcel can legally be leased. All new records have actual lease availability **Not verified** and a **Lease-ready — project dataset assumption** designation, just like the original 300. Wetlands and scrub areas may be subject to legal or environmental restrictions; mapped cover is descriptive evidence only. The original cadastral land-use label remains in `real_parcel_sources`. The research group, classification basis, mapped overlap, and evidence live in `research_supplement_land_cover`.

Each of the 100 records has an original EPSG:4326 cadastral Polygon/MultiPolygon, a derived centroid, and an interior map marker. Selection required a valid polygon entirely inside Colombo district, at least 100 m² area, no duplicate source ID or meaningful overlap with the original 300, and no conflicting claimant value. The 16 other records are at least 500 m² and do not overlap the first 84. All 100 IDs, claimant values, source labels, and geometries were checked against the live non-personal Survey Department service on 14 October 2026. For the 51 GIS-derived classifications, LUPPD polygon labels and boundaries were also checked against that service; the minimum observed mapped coverage was 80.0473%.

Source layers: [Survey Department cadastral land use](https://lis.survey.gov.lk/server/rest/services/Publishing_Server/LUSE_Cadastre_2026/MapServer/0) and [LUPPD mapped land use](https://lis.survey.gov.lk/server/rest/services/Publishing_Server/Land_Use_LUPPD/MapServer/0). Research use of these public data must follow the source provider's terms. No road, water, soil, erosion, or lease-availability values were fabricated.

## Files

- `data/gis/experiments/colombo/state_research_supplement_100/colombo_state_supplement_100.csv`: attributes and original boundary GeoJSON.
- `data/gis/experiments/colombo/state_research_supplement_100/colombo_state_supplement_100.geojson`: display geometry and separate research classification.
- `data/gis/experiments/colombo/state_research_supplement_100/supplement_100_validation_report.json`: source and geometry checks plus file hashes.
- `tools/ColomboStateSupplement100Import`: strict, repeatable importer. It refuses every database except `land_intel_colombo_state_research` and requires all original 300 parcel IDs to be present before applying.

## Run

From `StateLandGovernance`:

```powershell
dotnet run --project tools/ColomboStateSupplement100Import -- --dry-run
$env:LAND_INTELLIGENCE_STATE_RESEARCH_CONNECTION = '<connection to land_intel_colombo_state_research>'
dotnet run --project tools/ColomboStateSupplement100Import -- --apply
```

The first apply should report `inserted: 100` and `originalPreserved: 300`; a repeat should report `inserted: 0, updated: 100`. The importer checks dataset hashes and original IDs before opening the database, then applies all changes in one transaction. It writes 100 separate project-designation rows but does not alter the original 300 designations or create simulated workflow rows.

The API reads `GET /api/v1/land/parcels/research-supplement` for the separate research group and basis. `GET /api/v1/land/parcels/metrics` should show 400 total parcels and 400 project lease-ready assumptions after both imports. Restart the API after rebuilding, then reload the Parcel Intelligence page. Its **Source-reported land use** filter still uses the original cadastral label; the **Project research group** filter finds all 20 mapped Coconut records and the other research groups separately. The Find Suitable Land form offers **Coconut — source or project GIS (23)**, combining the 3 original Coconut labels and 20 distinct mapped Coconut records. The default result limit is 30 so all 23 Coconut candidates can be shown. The recommendation cards identify mapped coverage separately from the source label.

For a new supplement parcel, the parcel detail API should return its Polygon/MultiPolygon boundary, centroid, source claimant category `State`, and actual lease availability `Not verified`. Official assessment is still required before any leasing decision.

The repository's `.gitignore` excludes `StateLandGovernance/data/gis/`. These dataset files are present in this local project but will not be included in a normal commit. Provision them separately on other machines in accordance with the source provider's reuse terms; the importer and API code are tracked normally.
