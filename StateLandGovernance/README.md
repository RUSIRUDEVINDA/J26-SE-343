# State Land Governance — Backend

An Intelligent AI-Driven Digital Lease Governance Ecosystem for Transparent and Efficient State Land Lease Management in Sri Lanka.

## Run the API locally

Configure environment variables at the **repository root** (copy `.env.example` → `.env`, set `LAND_INTELLIGENCE_CONNECTION`).

```powershell
# From this folder (StateLandGovernance)
.\run-api.ps1

# Or explicitly
dotnet run --project src\Api\StateLandGovernance.Api.csproj
```

If `dotnet run` fails with **MSB3027 / file is locked by StateLandGovernance.Api**, an old API instance is still running. Stop it first:

```powershell
.\stop-api.ps1
```

Then run `.\run-api.ps1` again. Only one API instance can use port **5264** at a time.

Swagger: [http://localhost:5264/swagger](http://localhost:5264)

The Next.js frontend lives in `StateLandGovernance.Web/` at the repo root.