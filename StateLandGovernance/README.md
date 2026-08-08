# State Land Governance — Backend

**AI-Assisted Digital Governance for State Land Lease Management in Sri Lanka**

ASP.NET Core 8 modular monolith backend using Clean Architecture principles. This repository contains **structural scaffolding only** — no business logic, database, authentication, or API endpoints are implemented yet.

## Solution Structure

```
StateLandGovernance/
├── src/
│   ├── Api/                    # Single deployable Web API host
│   ├── Modules/                # Four independent business modules
│   ├── Shared/                 # Cross-module shared abstractions
│   └── BuildingBlocks/         # Reusable architectural primitives
├── tests/                      # Unit, integration, and architecture tests
└── docs/                       # Architecture and domain documentation
```

## Modules

| Module | Responsibility (future) |
|---|---|
| **LandIntelligence** | Land parcel data, GIS/PostGIS integration, spatial analysis |
| **LeaseFeasibility** | Lease viability assessment, AI-assisted scoring |
| **WorkflowGovernance** | Approval workflows, document processing, compliance routing |
| **GovernanceIntelligence** | Knowledge graph (Neo4j), governance analytics, AI insights |

## Dependency Direction

```
Presentation → Application → Domain
Infrastructure → Application + Domain (+ Shared.Infrastructure)
Api → Module Presentation + Module Infrastructure (composition root)
```

- **Domain** has no dependency on Infrastructure or Application.
- **Application** defines interfaces; Infrastructure implements them.
- **Api** is the composition root — the only project that wires Infrastructure into DI.

## Getting Started

```bash
cd StateLandGovernance
dotnet build
dotnet run --project src/Api/StateLandGovernance.Api.csproj
```

## Technology Placeholders

Future integrations will be placed in:

- **PostgreSQL / EF Core** — `Shared/Infrastructure/Persistence/`, module `Infrastructure/Persistence/`
- **PostGIS / GIS** — `LandIntelligence/Infrastructure/Integrations/`
- **Neo4j** — `GovernanceIntelligence/Infrastructure/Integrations/`
- **Redis** — `Shared/Infrastructure/Persistence/`
- **Object Storage** — `Shared/Infrastructure/Storage/`
- **AI / ML** — `LeaseFeasibility/`, `GovernanceIntelligence/` Integrations
- **Document Processing** — `WorkflowGovernance/Infrastructure/Integrations/`

## Next Steps

Implement incrementally: shared building blocks → domain entities → application layer → infrastructure → API endpoints.
