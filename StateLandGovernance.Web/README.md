# StateLandGovernance.Web

Next.js frontend for the **State Land Lease Information and Management System**.

This repository folder implements **Component 1: Land Intelligence and Spatial Recommendation** only. Components 2–4 are placeholders for other teams.

## Prerequisites

- Node.js 20+
- ASP.NET API running locally (default `http://localhost:5264`)
- PostgreSQL configured for the backend (see repository root `.env.example`)

## Setup

```bash
cd StateLandGovernance.Web
cp .env.local.example .env.local
npm install
```

## Run

```powershell
# Terminal 1 — API (pick one)

# Easiest: from repo root
.\StateLandGovernance\run-api.ps1

# From repository root (J26-SE-343)
dotnet run --project StateLandGovernance/src/Api/StateLandGovernance.Api.csproj

# From StateLandGovernance/src/Modules/LandIntelligence (your current folder)
dotnet run --project ../../Api/StateLandGovernance.Api.csproj

# From StateLandGovernance/src/Api
dotnet run

# Terminal 2 — frontend
cd StateLandGovernance.Web
npm run dev
```

Open [http://localhost:3000](http://localhost:3000).

## Build

```bash
npm run build
npm start
```

## Environment

| Variable | Description |
|----------|-------------|
| `NEXT_PUBLIC_API_BASE_URL` | Base URL for the ASP.NET Land Intelligence API (default `http://localhost:5264`) |

## Routes

- `/` — system module landing
- `/land-intelligence` — Component 1 dashboard
- `/land-intelligence/find-suitable-land`
- `/land-intelligence/recommendations`
- `/land-intelligence/parcel-intelligence`
- `/land-intelligence/knowledge-graph`
- `/component-02`, `/component-03`, `/component-04` — coming soon placeholders

## API usage (Component 1)

- `POST /api/v1/land/recommendations`
- `GET /api/v1/land/parcels/{id}`
- `GET /api/v1/land/parcels/{id}/constraints`
- `GET /api/v1/land/parcels/{id}/relationships`

Development CORS on the API allows `http://localhost:3000` when running in the Development environment.
