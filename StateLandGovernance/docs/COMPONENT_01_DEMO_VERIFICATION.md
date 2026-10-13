# Component 1 — demo verification checklist

Use this before submission or merge. Prerequisites: research DB imported, migrations applied, API + Web env configured per [COMPONENT_01_LAND_INTELLIGENCE_RELEASE.md](./COMPONENT_01_LAND_INTELLIGENCE_RELEASE.md).

## Automated gates

```powershell
cd StateLandGovernance
dotnet test tests/UnitTests/LandIntelligence/StateLandGovernance.UnitTests.LandIntelligence.csproj --filter "Category!=Integration"

cd ..\StateLandGovernance.Web
npm run build
npm run test:component-01
```

## Manual E2E (research demo)

| Step | Route / action | Pass |
| --- | --- | --- |
| 1 | Dashboard: ~300 parcels, lease-ready count, GIS **Unavailable** | |
| 2 | Find Suitable Land: purpose + Colombo + optional target area (perches/acres) | |
| 3 | Recommendations: ties, parcel area, criterion transparency | |
| 4 | Parcel detail + map | |
| 5 | Shortlist add/remove | |
| 6 | Lease interest: read-only area = parcel summary; submit draft | |
| 7 | My requests list + detail timeline | |
| 8 | Officer queue → review → status transition; NIC lookup still works | |

## Known limitations (do not treat as defects)

- No document file upload (checklist + notes only).
- Default research import has no GIS enrichment snapshots.
- Officer tools are demo-gated JWT, not production RBAC.
- Project lease-ready is a dataset assumption, not government verification.

## PR test plan template

Copy the manual E2E table into your PR description and note which DB and env flags you used.
