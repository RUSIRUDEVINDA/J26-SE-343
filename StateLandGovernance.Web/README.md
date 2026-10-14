# StateLandGovernance.Web

Next.js (App Router) frontend for the **State Land Lease Information and Management System**.

This folder implements **Component 1: Land Intelligence and Spatial Recommendation** and a **Component 2 Financial Intelligence & Proposal Support UI preview**.
Components 3–4 remain module placeholders (`lease-feasibility`, `governance-intelligence`,
`workflow-governance` / `component-0x`).

## Prerequisites

- Node.js 20+
- ASP.NET `StateLandGovernance.Api` running locally (default `http://localhost:5264`)
- PostgreSQL / PostGIS with the **Component 1 research demo** database imported:
  - **Recommended for J26-SE-343:** `land_intel_colombo_state_research` (300 parcels).
    See `StateLandGovernance/docs/COLOMBO_STATE_RESEARCH_300_IMPORT.md` and
    `StateLandGovernance/docs/COMPONENT_01_LAND_INTELLIGENCE_RELEASE.md`.
  - Full cadastre pilots (`land_intel_colombo_real_demo` / `real_pilot`) are separate;
    see `StateLandGovernance/docs/COLOMBO_REAL_PARCEL_IMPORT.md`.
- Do **not** point the normal UI at `land_intel_colombo_demo` (`DEMO-*` fixtures).

Imported parcels show **source-reported land use** from Survey Department/iLAS
metadata. That data does **not** prove ownership, legal availability, or suitability.
**Project lease-ready** is a research screening designation only. **Official lease
availability** remains unknown / not verified unless an authoritative record exists
(see customer release plan — not implemented for production leasing).

## Setup

```powershell
cd StateLandGovernance.Web
Copy-Item .env.local.example .env.local
npm install
```

Set repository-root `.env` `LAND_INTELLIGENCE_CONNECTION` to the research DB for the
standard Component 1 demo.

## Run (PowerShell)

```powershell
# Terminal 1 — API (LAND_INTELLIGENCE_CONNECTION in repo-root .env)
cd StateLandGovernance
.\run-api.ps1

# Terminal 2 — frontend
cd StateLandGovernance.Web
npm run dev
```

Open [http://localhost:3000](http://localhost:3000). Swagger:
[http://127.0.0.1:5264/swagger](http://127.0.0.1:5264/swagger).

Development CORS on the API allows only `http://localhost:3000` and
`http://127.0.0.1:3000`.

## Build / lint / typecheck

```powershell
cd StateLandGovernance.Web
npm run lint
npx tsc --noEmit
npm run build
npm run test:component-01
```

## Environment (Component 1)

| Variable | Description |
|----------|-------------|
| `NEXT_PUBLIC_API_BASE_URL` | Base URL for the ASP.NET API (see `.env.local.example`) |
| `NEXT_PUBLIC_LAND_INTELLIGENCE_RESEARCH_DEMO` | `true` for research-demo banners / simulated labels |
| `NEXT_PUBLIC_LAND_INTELLIGENCE_DEMO_OWNER_HEADER` | Dev-only owner-header fallback — **never** for production builds |

Experimental Colombo ML remains **disabled** in committed API configuration.
Do not enable it for normal frontend use. Standard ML (port 8500) is optional and
supplementary; 80% held-out accuracy is **not** claimed.

## Routes (Component 1)

| Route | Purpose |
|-------|---------|
| `/` | Module landing |
| `/land-intelligence` | Dashboard (metrics, GIS coverage) |
| `/land-intelligence/find-suitable-land` | Recommendation search form |
| `/land-intelligence/recommendations` | Session-stored search results |
| `/land-intelligence/parcels` | Parcel catalog |
| `/land-intelligence/parcels/[parcelId]` | Parcel details + constraints + KG link |
| `/land-intelligence/parcels/[parcelId]/map` | Map |
| `/land-intelligence/parcels/[parcelId]/lease-interest` | Start / resume lease interest (no draft on view alone) |
| `/land-intelligence/shortlist` | Saved parcels |
| `/land-intelligence/my-requests` | Drafts vs submitted requests |
| `/land-intelligence/my-requests/[requestId]` | Request status timeline |
| `/land-intelligence/knowledge-graph?parcelId=` | Relationship explorer |
| `/land-intelligence/officer/lease-interest-queue` | Officer queue (demo gate) |
| `/land-intelligence/officer/lease-interest-lookup` | Officer NIC lookup (demo gate) |
| `/land-intelligence/officer/lease-interest-review/[requestId]` | Officer review |
| `/land-intelligence/parcel-intelligence` | Legacy redirect → `/parcels` |

## API mappings (Component 1)

| UI action | HTTP |
|-----------|------|
| Search suitable land | **`POST /api/v1/land/recommendations`** (not `/recommendations/search`) |
| Parcel catalog search | `POST /api/v1/land/search` |
| List parcels | `GET /api/v1/land/parcels` |
| Metrics / GIS coverage | `GET /api/v1/land/parcels/metrics`, `…/gis-coverage` |
| Parcel facts | `GET /api/v1/land/parcels/{id}` |
| Constraints | `GET /api/v1/land/parcels/{id}/constraints` |
| Relationships | `GET /api/v1/land/parcels/{id}/relationships` |
| Find lease interest for parcel | `GET /api/v1/land/me/parcels/{id}/lease-interest` (204 if none) |
| Start draft | `POST /api/v1/land/me/parcels/{id}/lease-interest/draft` |
| Research demo config | `GET /api/v1/land/research-demo/config` |

Full route list and flags: `StateLandGovernance/docs/COMPONENT_01_LAND_INTELLIGENCE_RELEASE.md`.

## Production ML

Rule-based recommendations work without the production `ml_service`. When the ML
service is down, the API still returns rule scores; ML evidence is simply absent.
Absence of ML evidence must not be invented by the UI.

## WorkflowGovernance (API host) limitation

In **Development** / **Testing**, WorkflowGovernance lease cases use an **in-memory**
repository so the full API host can start. That store is volatile. **Production**
refuses to register it — durable persistence must be added before Production hosting.
Land Intelligence UI flows do not require lease-case storage.

## Component 2 UI preview

Open `/component-02` for the 13-screen financial document, assessment and proposal flow based on the Figma reference. It uses fictional sample data; OCR, scoring, prediction, RAG and submission are not connected. See `src/modules/component-02/README.md` for routes and integration boundaries.
