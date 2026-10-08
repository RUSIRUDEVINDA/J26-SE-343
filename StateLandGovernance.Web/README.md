# StateLandGovernance.Web

Next.js (App Router) frontend for the **State Land Lease Information and Management System**.

This folder implements **Component 1: Land Intelligence and Spatial Recommendation** only.
Components 2–4 are module placeholders (`lease-feasibility`, `governance-intelligence`,
`workflow-governance` / `component-0x`).

## Prerequisites

- Node.js 20+
- ASP.NET `StateLandGovernance.Api` running locally (default `http://localhost:5264`)
- PostgreSQL / PostGIS for Land Intelligence (configure via repository-root `.env` —
  never point automated checks at a shared production database)

## Setup

```powershell
cd StateLandGovernance.Web
Copy-Item .env.local.example .env.local
npm install
```

## Run (PowerShell)

```powershell
# Terminal 1 — API (Development). Uses LAND_INTELLIGENCE_CONNECTION from repo-root .env.
cd StateLandGovernance
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://127.0.0.1:5264"
dotnet run --project src\Api\StateLandGovernance.Api.csproj --urls http://127.0.0.1:5264

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
```

## Environment

| Variable | Description |
|----------|-------------|
| `NEXT_PUBLIC_API_BASE_URL` | Base URL for the ASP.NET API (see `.env.local.example`) |

Experimental Colombo ML remains **disabled** in committed API configuration.
Do not enable it for normal frontend demos.

## Routes (Component 1)

| Route | Purpose |
|-------|---------|
| `/` | Module landing |
| `/land-intelligence` | Dashboard |
| `/land-intelligence/find-suitable-land` | Recommendation search form |
| `/land-intelligence/recommendations` | Session-stored search results |
| `/land-intelligence/parcels` | Parcel catalog |
| `/land-intelligence/parcels/[parcelId]` | Parcel details + constraints + optional relationships |
| `/land-intelligence/knowledge-graph` | Relationship lookup by parcel id |
| `/land-intelligence/parcel-intelligence` | Legacy redirect → `/parcels` |

## API mappings

| UI action | HTTP |
|-----------|------|
| Search suitable land | `POST /api/v1/land/recommendations` |
| List parcels | `GET /api/v1/land/parcels` |
| Parcel facts | `GET /api/v1/land/parcels/{id}` |
| Constraints | `GET /api/v1/land/parcels/{id}/constraints` |
| Relationships | `GET /api/v1/land/parcels/{id}/relationships` |

Form fields that are not on the recommendation DTO (water proximity, soil group,
free-text extras) are shown disabled and are never submitted.

## Production ML

Rule-based recommendations work without the production `ml_service`. When the ML
service is down, the API still returns rule scores; ML evidence is simply absent.
Absence of ML evidence must not be invented by the UI.

## WorkflowGovernance (API host) limitation

In **Development** / **Testing**, WorkflowGovernance lease cases use an **in-memory**
repository so the full API host can start. That store is volatile. **Production**
refuses to register it — durable persistence must be added before Production hosting.
Land Intelligence UI flows do not require lease-case storage.
