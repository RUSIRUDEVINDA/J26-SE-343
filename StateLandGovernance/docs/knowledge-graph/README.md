# Knowledge graph setup (Component 1)

The Land Intelligence knowledge graph is **optional**. Search, recommendations, shortlist, and lease interest work without Neo4j.

## What the UI shows

- Route: `/land-intelligence/knowledge-graph?parcelId={uuid}`
- Parcel catalog/detail links open the same explorer with a human-readable cadastral label when available.
- Relationships are **only** those returned by `GET /api/v1/land/parcels/{id}/relationships`.
- Edges are never fabricated in the web app.

## API behaviour

| Situation | HTTP | UI |
|-----------|------|-----|
| Parcel missing | 404 | Error |
| Neo4j unset (`NEO4J_*` empty) | 503 | “Knowledge graph unavailable” + setup steps |
| Neo4j configured but query fails | 200 via PostGIS baseline when possible | Relationships if baseline can project them |
| Parcel OK, no synced edges | 200 empty list | Accurate empty state |

## Populate relationships

1. **Configure Neo4j** in the API environment (see repo `.env.example`):
   - `NEO4J_CONNECTION=bolt://localhost:7687`
   - `NEO4J_USERNAME=neo4j`
   - `NEO4J_PASSWORD=…` (do not commit secrets)
2. **Restart** the Land Intelligence API so DI binds the Neo4j client instead of the unconfigured stub.
3. **Sync parcel core graph** — parcel create/update paths call `LandParcelGraphSynchronizer` (admin area, category, use).
4. **Sync GIS-derived edges** — enrichment pipeline calls `LandParcelGisKnowledgeGraphSyncService` for near-road / near-water / soil / conservation edges. Skips quietly when Neo4j is unset.
5. **Research demo note** — the Colombo 300-parcel import has **no** GIS enrichment snapshots by default, so road/water/soil graph edges stay empty even when Neo4j is up. Admin/category baseline edges may still appear via PostGIS fallback when Neo4j is configured but down.

### Backfill Neo4j from PostgreSQL (Colombo import / real parcels)

GIS and research **imports write only to PostgreSQL**. To replace old synthetic Neo4j nodes with parcels from your current `LAND_INTELLIGENCE_CONNECTION` database:

```powershell
cd StateLandGovernance

# Preview how many parcels would sync
dotnet run --project tools/KnowledgeGraphBackfill -- --dry-run

# Wipe the local graph, then sync all parcels (core category / admin / use edges)
dotnet run --project tools/KnowledgeGraphBackfill -- --clear-graph

# Optional: limit or filter while testing
dotnet run --project tools/KnowledgeGraphBackfill -- --clear-graph --limit 10
dotnet run --project tools/KnowledgeGraphBackfill -- --district Colombo

# Optional: also push GIS-derived edges when enrichment snapshots exist
dotnet run --project tools/KnowledgeGraphBackfill -- --clear-graph --include-gis
```

Requires repository-root `.env` with `LAND_INTELLIGENCE_CONNECTION` and `NEO4J_*`. After a successful run, Neo4j Browser should show one `LandParcel` node per row in Postgres (cadastrals such as `52123409…` for the Colombo research DB).

## Verify

```http
GET /api/v1/land/parcels/{parcelId}/relationships
```

Open `/land-intelligence/knowledge-graph?parcelId={parcelId}` and confirm the visual explorer and accessible list match the API payload exactly.
