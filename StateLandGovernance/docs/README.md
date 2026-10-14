# Documentation

Documentation for the State Land Governance backend. Component 1 (Land Intelligence) guides below are maintained against the current research demo. Folders marked “placeholder” may still gain deeper content; existing READMEs inside them are authoritative when present.

## Component 1 — Land Intelligence (current)

| Doc | Purpose |
|---|---|
| [COMPONENT_01_LAND_INTELLIGENCE_RELEASE.md](./COMPONENT_01_LAND_INTELLIGENCE_RELEASE.md) | Setup, flags, routes, APIs (`POST /api/v1/land/recommendations`), limitations |
| [COMPONENT_01_DEMO_VERIFICATION.md](./COMPONENT_01_DEMO_VERIFICATION.md) | Automated + manual demo checklist and recorded counts |
| [COMPONENT_01_CUSTOMER_RELEASE_PLAN.md](./COMPONENT_01_CUSTOMER_RELEASE_PLAN.md) | Real-customer remaining work — **not** production-ready |
| [COLOMBO_STATE_RESEARCH_300_IMPORT.md](./COLOMBO_STATE_RESEARCH_300_IMPORT.md) | 300-parcel research import |
| [COLOMBO_REAL_PARCEL_IMPORT.md](./COLOMBO_REAL_PARCEL_IMPORT.md) | Full cadastre pilot import (not the default J26 demo DB) |
| [STATE_LAND_CANDIDATE_SCREENING.md](./STATE_LAND_CANDIDATE_SCREENING.md) | Screening rules — not official availability |
| [knowledge-graph/README.md](./knowledge-graph/README.md) | Neo4j optional setup |
| [ai/README.md](./ai/README.md) | Standard ML status (80% not achieved) |

## Folder index

| Folder | Purpose |
|---|---|
| **architecture/** | System architecture diagrams, ADRs, module boundaries (placeholder / expanding) |
| **api/** | OpenAPI specs, endpoint contracts (placeholder / expanding) |
| **database/** | PostgreSQL schemas, EF Core mappings, PostGIS (placeholder / expanding) |
| **knowledge-graph/** | Neo4j graph setup for Component 1 |
| **ai/** | Component 1 ML notes |
| **deployment/** | Docker Compose, Kubernetes, CI/CD (placeholder / expanding) |
