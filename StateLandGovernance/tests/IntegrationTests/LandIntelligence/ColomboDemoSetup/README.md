# Local Colombo demo database for Land Intelligence API + Next.js frontend.
#
# Database: land_intel_colombo_demo (durable; setup never drops it)
# Experimental ML: disabled
# Training CSV parcels (1,500): not imported

## One-shot setup (PowerShell)

From the `StateLandGovernance` solution root (or any directory):

```powershell
cd "C:\Users\Ravindu Peiris\Documents\GitHub\J26-SE-343\StateLandGovernance"
$env:PGPASSWORD = "<your-local-postgres-password>"
.\tests\IntegrationTests\LandIntelligence\ColomboDemoSetup\Setup-ColomboDemoDatabase.ps1
```

Equivalent one-liner (password from environment only):

```powershell
cd "C:\Users\Ravindu Peiris\Documents\GitHub\J26-SE-343\StateLandGovernance"; $env:PGPASSWORD = "<your-local-postgres-password>"; .\tests\IntegrationTests\LandIntelligence\ColomboDemoSetup\Setup-ColomboDemoDatabase.ps1
```

Re-running setup is idempotent: parcels upsert by cadastral number; GIS importers update-in-place.

## Start API (demo DB)

```powershell
cd "C:\Users\Ravindu Peiris\Documents\GitHub\J26-SE-343\StateLandGovernance"
$env:LAND_INTELLIGENCE_DEMO_CONNECTION = "Host=localhost;Port=5432;Database=land_intel_colombo_demo;Username=postgres;Password=<your-local-postgres-password>"
.\tests\IntegrationTests\LandIntelligence\ColomboDemoSetup\Start-ColomboDemoApi.ps1
```

Swagger: http://127.0.0.1:5264/swagger

## Start frontend

```powershell
cd "C:\Users\Ravindu Peiris\Documents\GitHub\J26-SE-343\StateLandGovernance.Web"
Copy-Item .env.local.example .env.local -ErrorAction SilentlyContinue
npm run dev
```

Open http://localhost:3000 — use Land Intelligence routes.

## Demo fixtures (synthetic — not official cadastral)

Stable cadastral numbers (UUIDs are assigned on first create and written to
`data/gis/experiments/colombo/colombo_demo_fixtures.json`):

| Cadastral | UUID (this machine after setup) | Scenario |
|-----------|----------------------------------|----------|
| `DEMO-COL-ELIGIBLE-001` | `ef232700-4723-49af-84a9-892f938dc58e` | Eligible rule-based Agricultural recommendations (Colombo) |
| `DEMO-COL-HARD-REJECT-001` | `fcaeeb91-c24a-4f84-9881-91a9ba5d766a` | Hard-constraint rejected via **[DEMO SIMULATED]** Prohibitive ProtectedArea |
| `DEMO-COL-SPARSE-GIS-001` | `1d1ff03a-0ed7-46ab-9831-6f5dee229c25` | Outside GIS coverage (Kandy) — road enrichment OutsideCoverage; missing evidence stays Unavailable |

Re-running setup on the same database keeps the same parcel IDs (upsert by cadastral).

**Note:** Land category / land-use *lookup* descriptions come from shared seed rows keyed by type. Demo identity is the `DEMO-COL-*` cadastral numbers and the explicitly labelled `[DEMO SYNTHETIC]` / `[DEMO SIMULATED]` fields. Administrative GIS verification may report Unavailable (incomplete boundary themes); Colombo thematic road/water/soil enrichment can still be Available.

### Suggested UI checks

1. **Catalog** — `/land-intelligence/parcels` lists the three demo cadastrals (plus any Hambantota pilot parcels if that import loaded them).
2. **Search** — Find Suitable Land: Purpose=Agricultural, District=Colombo, reject prohibitive = on → eligible ranks; hard-reject is rejected/excluded.
3. **Recommendations** — rule scores present; no invented ML evidence (ExperimentalColomboMl off).
4. **Parcel details** — open each cadastral; sparse shows Unavailable / outside-coverage GIS evidence.
5. **Restart API** — data remains (durable DB).

## Data sources / provenance

| Layer | Source |
|-------|--------|
| OSM motor roads | `osm_motor_roads_colombo_buffer.geojson` (OSM/HDX ODbL; filter policy `2026-03-20-colombo-v1`) |
| Hambantota pilot GIS | `data/gis/LandIntelligence_GIS` via `ImportHambantotaPilotAsync` |
| Colombo water/soil/conservation | same GIS root, clipped by Colombo district boundary |
| Demo parcel attributes | **synthetic** fixtures labelled `[DEMO SYNTHETIC]` / `[DEMO SIMULATED]` |

Coverage is incomplete by design: unknown themes stay Unknown/Unavailable.

## Explicit cleanup (separate; not part of setup)

```powershell
cd "C:\Users\Ravindu Peiris\Documents\GitHub\J26-SE-343\StateLandGovernance"
$env:PGPASSWORD = "<your-local-postgres-password>"
.\tests\IntegrationTests\LandIntelligence\ColomboDemoSetup\Remove-ColomboDemoDatabase.ps1 -Force
```
