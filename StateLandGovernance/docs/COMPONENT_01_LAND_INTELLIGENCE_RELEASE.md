# Component 1 — Land Intelligence release guide

This document describes the **current** Colombo state research pilot (`land_intel_colombo_state_research`, 300 parcels), web routes, configuration flags, API surface, known limitations, and verification steps. It supersedes informal assumptions in the UI (static “Colombo / Partial GIS” labels are removed in favour of live API measurements).

## Product positioning (read this first)

| Stance | Supported today? |
| --- | --- |
| **Colombo research demo** (cadastre + project lease-ready designation + simulated workflow when flagged) | **Yes** — primary target for J26-SE-343 |
| **Authenticated preliminary lease-interest pilot** (applicant JWT, officer review in dev/demo gates) | **Yes** — non-production; not legal leasing |
| **Customer-facing government leasing product** | **No** — no official availability, no document upload, no payments, GIS mostly unassessed on research DB |

## Database

| Database | Purpose |
| --- | --- |
| `land_intel_colombo_state_research` | **300-parcel** isolated research import (required for this release narrative) |
| `land_intel_colombo_real_demo` / `land_intel_colombo_real_pilot` | Full cadastre pilots (different importers) |
| `land_intel_colombo_demo` | `DEMO-*` fixtures only — do not use for research UI |

**API connection:** set repository-root `.env`:

```text
LAND_INTELLIGENCE_CONNECTION=Host=localhost;Port=5432;Database=land_intel_colombo_state_research;Username=postgres;Password=<local>
```

**Migrations (apply before first run):** EF migrations under `Infrastructure/Persistence/Migrations/`, including:

- `AddLeaseInterestAndShortlist`
- `AddLeaseInterestApplicantNic`
- `AddColomboStateResearchProjectLeaseReady`
- `AddLeaseInterestStatusHistoryAudit`

Run via API startup migrate or `dotnet ef database update` against the Land Intelligence context.

**Research import:** see [COLOMBO_STATE_RESEARCH_300_IMPORT.md](./COLOMBO_STATE_RESEARCH_300_IMPORT.md).

### Measured GIS state (research DB, typical after import)

- **300** indexed parcels; district/province from parcel location fields (live dashboard).
- **0** GIS enrichment snapshots unless you import reference layers and run enrichment.
- **`GET /api/v1/land/parcels/gis-coverage`** reports per-layer **Unavailable** with zero parcel evidence.
- Config default: `LandIntelligence:GisEnrichmentCoverage:SupportedDistrictNames` = **Hambantota** only in `appsettings.Development.json` — add **Colombo** after layer import.

## Configuration

### Repository `.env` (API)

| Variable | Required | Notes |
| --- | --- | --- |
| `LAND_INTELLIGENCE_CONNECTION` | Yes | Must point at target DB |
| `LEASE_INTEREST_NIC_ENCRYPTION_KEY` | Yes for lease interest | Base64 32-byte key |
| `LEASE_INTEREST_NIC_LOOKUP_KEY` | Yes for officer NIC lookup | Base64 32-byte key |
| `LAND_INTELLIGENCE_JWT_SIGNING_KEY` | Production | Dev fallback in `appsettings.Development.json` |
| `NEO4J_*` | Optional | Knowledge graph; empty graph if unset |
| `ML_SUITABILITY_SERVICE_URL` | Optional | Supplementary ML evidence |

### `StateLandGovernance.Web/.env.local`

| Variable | Purpose |
| --- | --- |
| `NEXT_PUBLIC_API_BASE_URL` | e.g. `http://localhost:5264` |
| `NEXT_PUBLIC_LAND_INTELLIGENCE_RESEARCH_DEMO=true` | Banner + simulated lease workflow UI |
| `NEXT_PUBLIC_LAND_INTELLIGENCE_DEMO_OWNER_HEADER=true` | Legacy demo owner header when JWT absent (dev only) |

### API `appsettings.Development.json` (Land Intelligence)

| Setting | Research demo |
| --- | --- |
| `LandIntelligence:ResearchDemo:Enabled` | `true` to expose simulated workflow + officer gates |
| `LandIntelligence:Authentication:AllowDevelopmentSessionEndpoint` | Dev JWT bootstrap |
| `LandIntelligence:Authentication:AllowDemoOwnerHeader` | Align with web demo header flag |

Officer NIC lookup and lease-interest **review** APIs are gated like the research demo (non-production).

## Web routes (`/land-intelligence`)

| Route | Flow step |
| --- | --- |
| `/land-intelligence` | Dashboard (live parcel count, lease-ready count, GIS coverage) |
| `/land-intelligence/find-suitable-land` | Search form → recommendations API |
| `/land-intelligence/recommendations` | Recommendation results workspace |
| `/land-intelligence/parcels`, `/parcel-intelligence` | Parcel catalog / search |
| `/land-intelligence/parcels/[id]` | Parcel detail (+ GIS enrichment summary) |
| `/land-intelligence/parcels/[id]/map` | Map view (Leaflet) |
| `/land-intelligence/knowledge-graph` | Neo4j relationships (optional) |
| `/land-intelligence/shortlist` | Saved parcels |
| `/land-intelligence/parcels/[id]/lease-interest` | Lease interest draft/submit |
| `/land-intelligence/my-requests` | Applicant request list |
| `/land-intelligence/my-requests/[requestId]` | Status + timeline |
| `/land-intelligence/officer/lease-interest-queue` | Officer verification queue (demo gate) |
| `/land-intelligence/officer/lease-interest-lookup` | Officer NIC lookup (demo gate) |
| `/land-intelligence/officer/lease-interest-review/[requestId]` | Officer status transitions |

Sidebar shows officer queue and NIC lookup when `GET /api/v1/land/research-demo/config` reports `officerLeaseInterestNicLookupEnabled`.

## Primary API groups (Component 1)

| Area | Examples |
| --- | --- |
| Parcels | `GET /api/v1/land/parcels`, `GET .../metrics`, `GET .../gis-coverage`, `GET .../{id}` |
| Recommendations | `POST /api/v1/land/recommendations/search` |
| Graph | `GET /api/v1/land/parcels/{id}/relationships` |
| Citizen workspace | Shortlist, lease interest draft/submit, my requests |
| Officer (demo) | NIC lookup, review queue, status update |
| Auth | Development session + JWT bearer on applicant/officer routes |

Applicant/officer calls use bearer tokens via `LandIntelligenceSessionBootstrap` and `apiClient` auth roles.

## End-to-end flow (happy path)

1. **Dashboard** — confirms 300 parcels, project lease-ready effective count, GIS **Unavailable**.
2. **Find Suitable Land** — purpose + location + source filters (no road/water/soil preference controls).
3. **Recommendations** — scores treat missing GIS as not assessed (no false “clear”).
4. **Parcel / map** — cadastre source fields; GIS enrichment section shows not run.
5. **Shortlist** — persist candidates (authenticated).
6. **Lease interest** — draft → review step → submit; blocked for Private claimant / hard restrictions.
7. **My Requests** — status labels + detail page with officer-visible history.
8. **Officer** — open verification queue (or lookup by NIC) → review → transition to needs info / declined / referred.

## UX, mobile, accessibility

- **Mobile:** `PageLayout` stacks sidebar above content below 900px; dashboard and form panels use single-column grids at breakpoints in module CSS.
- **Errors:** `ErrorMessage`, `EmptyState`, `LoadingState` on major views; API errors surfaced via `ApiClientError` detail.
- **Accessibility:** form labels, `aria-live` on dashboard metrics, `aria-current` on nav, step trails on lease interest; map requires pointer interaction (Leaflet limitation).

## Verification commands

```powershell
# Backend unit tests (excludes DB integration by default filter)
cd StateLandGovernance
dotnet test tests/UnitTests/LandIntelligence/StateLandGovernance.UnitTests.LandIntelligence.csproj --filter "Category!=Integration"

# DB-backed GIS coverage smoke (requires LAND_INTELLIGENCE_CONNECTION in .env)
dotnet test tests/UnitTests/LandIntelligence/StateLandGovernance.UnitTests.LandIntelligence.csproj --filter "FullyQualifiedName~LandIntelligenceGisCoverageIntegration"

# Frontend production build
cd StateLandGovernance.Web
npm run build

# Component 1 frontend logic tests (Node, no Jest)
node tests/component-01.test.mjs
```

Restart the API after backend changes so new routes (e.g. `gis-coverage`) are served.

## Known limitations (research demo)

- No authoritative GIS enrichment on default 300 import.
- No document uploads for lease interest (checklist only).
- Simulated lease workflow is **not** legal availability.
- Project lease-ready is a **dataset assumption**, not government verification.
- Neo4j optional; graph may be empty without enrichment sync.
- Officer tools are pilot/demo gated, not production RBAC.

## Related docs

- [COLOMBO_STATE_RESEARCH_300_IMPORT.md](./COLOMBO_STATE_RESEARCH_300_IMPORT.md)
- Module UI map: `StateLandGovernance.Web/src/modules/component-01-land-intelligence/README.md`
- Experimental GIS/ML: `src/Modules/LandIntelligence/docs/COLOMBO_EXPERIMENTAL_GIS_ML.md`
