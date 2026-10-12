# Remove synthetic / demo land parcels

Deletes **only** Land Intelligence parcels whose cadastral number matches `DEMO-%`
and that are **not** linked to `land_intelligence.real_parcel_sources` (imported
Survey Department/iLAS rows are never deleted).

The script **never drops a database**. Default mode is dry-run.

## Dry-run (safe)

```powershell
cd StateLandGovernance
.\tools\RemoveSyntheticLandParcels\Remove-SyntheticLandParcels.ps1 `
  -ConnectionString $env:LAND_INTELLIGENCE_CONNECTION
```

## Delete (requires `-Force`)

```powershell
.\tools\RemoveSyntheticLandParcels\Remove-SyntheticLandParcels.ps1 `
  -ConnectionString $env:LAND_INTELLIGENCE_CONNECTION `
  -Force
```

Use the connection for the database you intentionally want cleaned (for example
`land_intel_colombo_real_demo` or a mixed demo DB). Confirm the dry-run list
before `-Force`.
