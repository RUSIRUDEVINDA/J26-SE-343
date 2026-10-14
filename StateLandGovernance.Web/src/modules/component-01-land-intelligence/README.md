# Land Intelligence (Component 1)

Domain UI, API client mappings, types, and formatters for the Land Intelligence
module. Route files under `src/app/land-intelligence/` stay thin and import from
here.

Other platform components must not import this module. Use `src/shared` / `src/lib`
for cross-cutting UI and HTTP only.

## Release and operations

See **`StateLandGovernance/docs/COMPONENT_01_LAND_INTELLIGENCE_RELEASE.md`** for the
Colombo research database, routes, demo flags, API list (`POST /api/v1/land/recommendations`),
migrations, ML/GIS honesty, and verification commands.

- Import: **`StateLandGovernance/docs/COLOMBO_STATE_RESEARCH_300_IMPORT.md`**
- Demo checklist: **`StateLandGovernance/docs/COMPONENT_01_DEMO_VERIFICATION.md`**
- Real-customer gaps (not production-ready): **`StateLandGovernance/docs/COMPONENT_01_CUSTOMER_RELEASE_PLAN.md`**

### Local demo checklist

1. Repository **`.env`**: `LAND_INTELLIGENCE_CONNECTION` → `land_intel_colombo_state_research` (see `.env.example`); set `LEASE_INTEREST_NIC_ENCRYPTION_KEY` and `LEASE_INTEREST_NIC_LOOKUP_KEY`.
2. **`StateLandGovernance.Web/.env.local`**: copy from `StateLandGovernance.Web/.env.local.example` (`RESEARCH_DEMO=true` for simulated labels).
3. API **Development**: `LandIntelligence:ResearchDemo:Enabled` and dev session endpoint (see release doc). Keep production demo-auth flags **false**.
4. Apply EF migrations; run research import if the DB is empty.
5. Manual sign-off: **`COMPONENT_01_DEMO_VERIFICATION.md`** — distinguish research demo, project lease-ready, and official availability (unknown).

## Frontend tests

```bash
node tests/component-01.test.mjs
```

(Run from `StateLandGovernance.Web`.)
