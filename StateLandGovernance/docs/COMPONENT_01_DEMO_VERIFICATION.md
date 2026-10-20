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
| 1 | Dashboard: ~300 parcels, project lease-ready count, GIS **Unavailable** | |
| 2 | Find Suitable Land: purpose + Colombo + optional target area (perches/acres) | |
| 3 | Recommendations via **`POST /api/v1/land/recommendations`**: match score caption, missing GIS **not assessed**, total &lt; 100 unless all assessed = 100 | |
| 4 | Parcel detail + map; open **Relationships** / knowledge-graph link | |
| 5 | Shortlist add/remove | |
| 6 | Lease interest: open form → **no draft** until **Start request**; submit for preliminary assessment | |
| 7 | My requests: **Drafts** vs **Submitted**; detail timeline | |
| 8 | Officer queue → review → status transition (label **simulated** when ResearchDemo on); NIC lookup | |

## Distinctions (do not treat as defects)

| Concept | Correct interpretation |
| --- | --- |
| Research demo | Submission/local demo of the C1 journey |
| Project lease-ready | Dataset screening assumption |
| Simulated workflow status | Demo officer/applicant labels only |
| Official lease availability | Unknown / not verified — never inferred |

Other non-defects: no document file upload; default research import has no GIS enrichment snapshots; officer tools are demo-gated JWT, not production RBAC.

## Recorded verification (2026-10-14, local research DB)

| Check | Result |
| --- | --- |
| DB | `land_intel_colombo_state_research` via `LAND_INTELLIGENCE_CONNECTION` |
| Parcels | **300** (`GET /parcels/metrics` + SQL) |
| Project lease-ready designated / effective | **300 / 300** |
| GIS enrichment snapshots | **0** — overall status **Unavailable** (not a positive match) |
| Web | `NEXT_PUBLIC_API_BASE_URL`, `NEXT_PUBLIC_LAND_INTELLIGENCE_RESEARCH_DEMO=true` (local `.env.local`) |
| API Development | `LandIntelligence:ResearchDemo:Enabled=true`; JWT + session endpoint allowed in Development only |
| Production `appsettings.json` | `AllowDemoOwnerHeader=false`, `AllowDevelopmentSessionEndpoint=false` (unchanged) |
| Recommendation smoke | `POST /api/v1/land/recommendations` — Colombo + target 0.05 ha → **300** candidates; top score **97.86** (&lt; 100); spatial criterion **not assessed** |
| Lease-interest open | repeated GET → **204** (no draft created); POST draft resumes same id |
| Shortlist | authenticated PUT/list OK |
| Officer queue | available when ResearchDemo enabled after API restart; gated **403** when disabled |
| Simulated status | labeled simulated when research demo flags on; lease availability remains unverified / project-assumption only |
| Standard ML | supplementary; held-out ~76.5%; **80% not claimed** |
| Secrets / prod auth | no secrets committed; production demo-auth flags remain false |

Restart the API after changing `appsettings.Development.json` so ResearchDemo / officer gates reload.

## PR test plan template

Copy the manual E2E table into your PR description and note which DB and env flags you used. State clearly: research demo ≠ official lease availability.
