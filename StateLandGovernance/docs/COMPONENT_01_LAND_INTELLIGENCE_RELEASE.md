# Component 1 — Land Intelligence release guide

This document describes the **current** Colombo state research pilot (`land_intel_colombo_state_research`, **400 parcels: 300 original + 100 supplementary**), web routes, configuration flags, API surface, known limitations, and verification steps. It matches the implemented code as of the research-demo workstream (Prompts 1–12).

## Product positioning (read this first)

| Stance | Supported today? |
| --- | --- |
| **Colombo research demo** (cadastre + project lease-ready designation + simulated workflow when flagged) | **Yes** — primary target for J26-SE-343 |
| **Authenticated preliminary lease-interest pilot** (applicant JWT, officer review under demo gates) | **Yes** — non-production; not legal leasing |
| **Customer-facing government leasing product** | **No** — see [COMPONENT_01_CUSTOMER_RELEASE_PLAN.md](./COMPONENT_01_CUSTOMER_RELEASE_PLAN.md) |

### Three labels that must not be conflated

| Label | Meaning today |
| --- | --- |
| **Research demo** | Local/submission demo of search → recommend → shortlist → preliminary lease interest → officer review |
| **Project lease-ready** | Dataset designation for screening only — **not** an official government decision |
| **Official lease availability** | **Unknown / not verified** on the 300-parcel import; never inferred from claimant category, land use, GIS, project designation, or ML score |

## Database

| Database | Purpose |
| --- | --- |
| `land_intel_colombo_state_research` | **400-parcel** isolated research import (300 original + 100 supplementary) — **required** for this release narrative |
| `land_intel_colombo_real_demo` / `land_intel_colombo_real_pilot` | Full cadastre pilots (different importers; not the default J26 demo) |
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
- `AddLeaseInterestOfficerValuation` / `AddLeaseInterestPaperWorksheetFields`
- `AddLeaseInterestAuthorityDecisions` / `BindAuthorityDecisionToReviewedValuation`
- `AddLeaseInterestValuationIdempotencyKey`

Run via API startup migrate or `dotnet ef database update` against the Land Intelligence context.

**Research import:** see [COLOMBO_STATE_RESEARCH_300_IMPORT.md](./COLOMBO_STATE_RESEARCH_300_IMPORT.md) and the supplementary 100-parcel import tooling.

### Parcel classification provenance

| Layer | Meaning |
| --- | --- |
| **Original cadastral land use** | Source-reported label/code from the cadastre import — unchanged in storage and parcel detail |
| **GIS project classification** | Research supplement / project land-cover group — separate provenance, never replaces cadastral land use |
| **Canonical Find Suitable Land filter** | One user-facing list (Coconut, Wetland, Scrub / Low Jungle, …) that may match cadastral labels, GIS groups, or both; counts use **distinct parcel IDs** |

### Measured GIS state (research DB, typical after import)

- **400** indexed parcels (300 + 100); district/province from parcel location fields (live dashboard).
- **0** GIS enrichment snapshots unless you import reference layers and run enrichment.
- **`GET /api/v1/land/parcels/gis-coverage`** reports per-layer **Unavailable** with zero parcel evidence.
- Missing environmental / GIS evidence is **not assessed** in recommendation scoring and is never treated as a positive match.
- Config default: `LandIntelligence:GisEnrichmentCoverage:SupportedDistrictNames` = **Hambantota** only in `appsettings.Development.json` — add **Colombo** only after layer import.

## Configuration

### Repository `.env` (API)

| Variable | Required | Notes |
| --- | --- | --- |
| `LAND_INTELLIGENCE_CONNECTION` | Yes | Must point at target DB (research DB for this guide) |
| `LEASE_INTEREST_NIC_ENCRYPTION_KEY` | Yes for lease interest | Base64 32-byte key — do not commit secrets |
| `LEASE_INTEREST_NIC_LOOKUP_KEY` | Yes for officer NIC lookup | Base64 32-byte key — do not commit secrets |
| `LAND_INTELLIGENCE_JWT_SIGNING_KEY` | Production | Dev fallback in `appsettings.Development.json` |
| `NEO4J_*` | Optional | Knowledge graph; see [knowledge-graph/README.md](./knowledge-graph/README.md) |
| `ML_SUITABILITY_SERVICE_URL` | Optional | Supplementary ML only (`http://localhost:8500`) |

### `StateLandGovernance.Web/.env.local`

| Variable | Purpose |
| --- | --- |
| `NEXT_PUBLIC_API_BASE_URL` | e.g. `http://localhost:5264` |
| `NEXT_PUBLIC_LAND_INTELLIGENCE_RESEARCH_DEMO=true` | Banner + simulated lease workflow UI labels |
| `NEXT_PUBLIC_LAND_INTELLIGENCE_DEMO_OWNER_HEADER=true` | Legacy demo owner header when JWT absent (**dev only** — never for production builds) |

Copy from `StateLandGovernance.Web/.env.local.example`.

### API `appsettings.Development.json` (Land Intelligence)

| Setting | Research demo |
| --- | --- |
| `LandIntelligence:ResearchDemo:Enabled` | `true` to expose simulated workflow + officer gates |
| `LandIntelligence:Authentication:AllowDevelopmentSessionEndpoint` | Dev JWT bootstrap |
| `LandIntelligence:Authentication:AllowDemoOwnerHeader` | Align with web demo header flag |
| `LandIntelligence:ExperimentalColomboMl:Enabled` | **`false`** — keep experimental Colombo ML separate |

### Production `appsettings.json` (must stay locked down)

| Setting | Production value |
| --- | --- |
| `AllowDemoOwnerHeader` | **`false`** |
| `AllowDevelopmentSessionEndpoint` | **`false`** |
| `ExperimentalColomboMl:Enabled` | **`false`** |

Officer NIC lookup and lease-interest **review** APIs remain gated like the research demo (non-production RBAC).

## Web routes (`/land-intelligence`)

| Route | Flow step |
| --- | --- |
| `/land-intelligence` | Dashboard (live parcel count, project lease-ready counts, GIS coverage) |
| `/land-intelligence/find-suitable-land` | Search form → recommendations API |
| `/land-intelligence/recommendations` | Recommendation results workspace |
| `/land-intelligence/parcels`, `/parcel-intelligence` | Parcel catalog / search |
| `/land-intelligence/parcels/[id]` | Parcel detail (+ GIS enrichment summary + KG link) |
| `/land-intelligence/parcels/[id]/map` | Map view (Leaflet) |
| `/land-intelligence/knowledge-graph?parcelId=` | Relationship explorer (API edges only) |
| `/land-intelligence/shortlist` | Saved parcels |
| `/land-intelligence/parcels/[id]/lease-interest` | Start request / resume draft / redirect submitted |
| `/land-intelligence/my-requests` | Drafts vs submitted lists |
| `/land-intelligence/my-requests/[requestId]` | Status + timeline + applicant valuation summary + authority decision |
| `/land-intelligence/officer/lease-interest-queue` | Officer verification queue (demo gate) |
| `/land-intelligence/officer/lease-interest-lookup` | Officer NIC lookup (demo gate) |
| `/land-intelligence/officer/lease-interest-review/[requestId]` | Officer status transitions + manual valuation worksheet |
| `/land-intelligence/authority/lease-interest-queue` | Authority queue (Pending / Approved / Rejected) |
| `/land-intelligence/authority/lease-interest-review/[requestId]` | Authority decision after valuation |

Sidebar shows officer/authority tools when `GET /api/v1/land/research-demo/config` reports the matching demo gates.

## Primary API groups (Component 1)

Base path: `/api/v1/land`.

| Area | Implemented routes |
| --- | --- |
| Parcels | `GET /parcels`, `GET /parcels/metrics`, `GET /parcels/gis-coverage`, `GET /parcels/{id}`, `GET /parcels/{id}/constraints`, `GET /parcels/{id}/relationships` |
| Parcel search (catalog filters) | `POST /search` — **not** the recommendation endpoint |
| **Recommendations** | **`POST /recommendations`** (body = search criteria). Also `GET /recommendations/{id}`. **There is no `/recommendations/search`.** |
| Citizen workspace | `GET/PUT/DELETE /me/shortlist…`, `GET /me/parcels/{id}/lease-interest` (200 or **204**), `POST /me/parcels/{id}/lease-interest/draft` (explicit start), `PUT …/draft`, `POST …/submit`, `GET /me/lease-interest/requests`, `GET /me/lease-interest/requests/{id}/valuation` (**204** when none) |
| Officer (demo) | `GET /officer/lease-interest/queue`, `POST …/lookup`, `GET …/requests/{id}`, `POST …/requests/{id}/status`, `GET/POST …/requests/{id}/valuations` (`Idempotency-Key` required on POST) |
| Authority (demo) | `GET /authority/lease-interest/queue?statusFilter=`, `GET …/requests/{id}`, `POST …/requests/{id}/decisions` |
| Research demo | `GET /research-demo/config` |
| Auth | `POST /auth/session` (Development only when allowed) |

Applicant / officer / authority calls use bearer tokens via research-demo session minting and `apiClient` auth roles. Cached JWTs are checked for expiry; a single renew-and-retry applies on 401. **This is not production identity** — replace with real authentication and RBAC before deployment.

### Lease interest draft behaviour (implemented)

- **Viewing** the lease-interest page calls **GET** only → **204** when no request exists (no empty draft).
- **Start request** calls **POST** `…/lease-interest/draft`.
- Submitted requests redirect to My Requests (read-only status).

## Recommendation scoring (UI + engine)

- Total is a **match across assessed search criteria** — not legal suitability or lease approval.
- Displayed **100** only when every assessed criterion is fully met.
- Continuous area match preserved (e.g. target 0.05 ha vs parcel 0.045 ha → area score **90**, total &lt; 100).
- Road, water, and soil preferences are **not** part of search or rule scoring.
- Missing GIS / environmental evidence → **not assessed** (never a positive match).

## ML status (standard service)

| Item | Current |
| --- | --- |
| Role | Supplementary evidence only — does not override rules |
| Held-out RF accuracy | ~**76.5%** on a **synthetic, rule-labelled held-out sample** — **not** validated on real Colombo parcels; **80% target not achieved** |
| Area input | ML requests always send area in **hectares** (converted from the parcel's stored unit) |
| Road / water / soil | Excluded from standard model (not invented) |
| Colombo experimental ML | Separate service/artifacts; default **disabled** |

Details: [ai/README.md](./ai/README.md), `src/Modules/LandIntelligence/ML/README.md`.

## End-to-end flow (happy path)

1. **Dashboard** — confirms ~400 parcels, project lease-ready effective count, GIS **Unavailable**.
2. **Find Suitable Land** — purpose + location + **canonical land-use** filter + target area (no road/water/soil preference controls). URL + session preserve criteria for reload / Edit search.
3. **Recommendations** — suitability-ranked match scores; project designation vs official availability shown separately; missing GIS not assessed.
4. **Parcel / map** — cadastre source fields; GIS enrichment section shows not run when snapshots = 0.
5. **Shortlist** — persist candidates (authenticated).
6. **Lease interest** — **Start request** → partial **Save draft** allowed → submit requires full validation; blocked for Private claimant / hard restrictions.
7. **My Requests** — Drafts vs Submitted; valuation loading states (none / available / access denied / error+retry); authority Approved/Rejected with reason.
8. **Officer** — queue / NIC lookup → review → refer to authority → record **Agricultural/Commercial** or **Residential** valuation worksheet (manual figures only). Valuation save keeps status **ReferredToAuthority**; revisions are idempotent.
9. **Authority** — queue tabs (Pending / Approved / Rejected) → review full latest worksheet + revisions → **Approved** (preliminary; formal lease still required) or **Rejected**.

### Valuation and authority behaviour

| Rule | Behaviour |
| --- | --- |
| Valuation form types | Agricultural/Commercial and Residential paper-derived worksheets |
| Status on valuation save | Remains **ReferredToAuthority** (6) |
| Revisions | Append-only; client `Idempotency-Key` prevents duplicate revisions on retry |
| Authority outcomes | **Approved** (7) or **Rejected** (5) only |
| Official vs project | Project designation ≠ official government verification; official availability stays **Not verified** unless authoritative confirmation exists |

## UX, mobile, accessibility

- **Mobile:** `PageLayout` stacks sidebar above content below 900px; dashboard and form panels use single-column grids at breakpoints in module CSS.
- **Errors:** `ErrorMessage`, `EmptyState`, `LoadingState` on major views; API errors surfaced via `ApiClientError` detail.
- **Accessibility:** form labels, `aria-live` on dashboard metrics, `aria-current` on nav, step trails on lease interest; KG visual graph has a text list equivalent; map requires pointer interaction (Leaflet limitation).

## Verification commands

```powershell
# Backend unit tests (excludes DB integration by default filter)
cd StateLandGovernance
dotnet test tests/UnitTests/LandIntelligence/StateLandGovernance.UnitTests.LandIntelligence.csproj --filter "Category!=Integration"

# DB-backed GIS coverage smoke (requires LAND_INTELLIGENCE_CONNECTION in .env)
dotnet test tests/UnitTests/LandIntelligence/StateLandGovernance.UnitTests.LandIntelligence.csproj --filter "FullyQualifiedName~LandIntelligenceGisCoverageIntegration"

# Frontend production build + Component 1 logic tests
cd StateLandGovernance.Web
npm run build
npm run test:component-01
```

Manual checklist + recorded counts: [COMPONENT_01_DEMO_VERIFICATION.md](./COMPONENT_01_DEMO_VERIFICATION.md).

Restart the API after backend or `appsettings.Development.json` changes.

## Known limitations (research demo)

- No authoritative GIS enrichment on default 300 import.
- No secure document upload for lease interest (checklist text only).
- Simulated lease workflow is **not** legal availability — label as simulated when demo flags are on.
- Project lease-ready is a **dataset assumption**, not government verification.
- Official lease availability remains **unknown / not verified**.
- Neo4j optional; empty when unset or unsynced — [knowledge-graph/README.md](./knowledge-graph/README.md).
- Officer tools are pilot/demo gated, not production identity/RBAC.
- Standard ML below 80% held-out accuracy; keep supplementary.
- No payments, no authority-issued lease instruments.

## Related docs

- [COMPONENT_01_DEMO_VERIFICATION.md](./COMPONENT_01_DEMO_VERIFICATION.md)
- [COMPONENT_01_CUSTOMER_RELEASE_PLAN.md](./COMPONENT_01_CUSTOMER_RELEASE_PLAN.md) — real-customer remaining work (not production-ready)
- [COLOMBO_STATE_RESEARCH_300_IMPORT.md](./COLOMBO_STATE_RESEARCH_300_IMPORT.md)
- Module UI map: `StateLandGovernance.Web/src/modules/component-01-land-intelligence/README.md`
- Experimental GIS/ML: `src/Modules/LandIntelligence/docs/COLOMBO_EXPERIMENTAL_GIS_ML.md`
- Standard ML: `src/Modules/LandIntelligence/ML/README.md`
