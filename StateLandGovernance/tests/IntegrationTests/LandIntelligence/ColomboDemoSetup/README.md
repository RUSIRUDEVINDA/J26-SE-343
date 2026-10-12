# Colombo DEMO fixtures (test / research only — not for normal UI)

**Do not use this package for the normal Land Intelligence frontend or API.**

The active application must use imported Survey Department/iLAS Colombo cadastre
data (for example database `land_intel_colombo_real_demo` or
`land_intel_colombo_real_pilot`). See `docs/COLOMBO_REAL_PARCEL_IMPORT.md`.

This folder creates **`land_intel_colombo_demo`** and inserts synthetic parcels with
cadastral numbers `DEMO-COL-*`. Those rows are excluded from normal parcel search /
recommendations. They exist only for isolated integration checks and historical
demo reproducibility.

## Guardrails

- Setup and API start scripts require `-IUnderstandThisIsSyntheticDemoData`.
- Scripts never drop `state_land_governance` or real-cadastre databases.
- To remove `DEMO-*` rows from a mixed database, use  
  `tools/RemoveSyntheticLandParcels/Remove-SyntheticLandParcels.ps1` (dry-run by default).

## Setup (explicit opt-in)

```powershell
$env:LAND_INTELLIGENCE_DEMO_CONNECTION = "Host=localhost;Port=5432;Database=land_intel_colombo_demo;Username=postgres;Password=<local>"
.\Setup-ColomboDemoDatabase.ps1 -IUnderstandThisIsSyntheticDemoData -Password <local>
```

## Start API against demo DB (explicit opt-in)

```powershell
.\Start-ColomboDemoApi.ps1 -IUnderstandThisIsSyntheticDemoData
```

## Fixture IDs (synthetic)

| Cadastral | Purpose |
|-----------|---------|
| `DEMO-COL-ELIGIBLE-001` | Eligible rule-based Agricultural recommendations |
| `DEMO-COL-HARD-REJECT-001` | Hard-constraint rejection ([DEMO SIMULATED]) |
| `DEMO-COL-SPARSE-GIS-001` | Sparse GIS coverage fixture |

Land category / land-use **lookup** rows (`[SYNTHETIC]` descriptions) are EF
HasData seeds required by the schema FK model — not parcel demo rows.
