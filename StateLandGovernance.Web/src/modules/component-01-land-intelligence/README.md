# Land Intelligence (Component 1)

Domain UI, API client mappings, types, and formatters for the Land Intelligence
module. Route files under `src/app/land-intelligence/` stay thin and import from
here.

Other platform components must not import this module. Use `src/shared` / `src/lib`
for cross-cutting UI and HTTP only.

## Release and operations

See **`StateLandGovernance/docs/COMPONENT_01_LAND_INTELLIGENCE_RELEASE.md`** for the
Colombo research database, routes, demo flags, API list, migrations, and verification
commands. Import procedure: **`StateLandGovernance/docs/COLOMBO_STATE_RESEARCH_300_IMPORT.md`**.

### Local demo checklist

1. Repository **`.env`**: `LAND_INTELLIGENCE_CONNECTION` → `land_intel_colombo_state_research` (see `.env.example`); set `LEASE_INTEREST_NIC_ENCRYPTION_KEY` and `LEASE_INTEREST_NIC_LOOKUP_KEY`.
2. **`StateLandGovernance.Web/.env.local`**: copy from `StateLandGovernance.Web/.env.local.example`.
3. API **Development**: `LandIntelligence:ResearchDemo:Enabled` and dev session endpoint (see release doc).
4. Apply EF migrations; run research import if the DB is empty.
5. Manual sign-off: **`StateLandGovernance/docs/COMPONENT_01_DEMO_VERIFICATION.md`**.

## Frontend tests

```bash
node tests/component-01.test.mjs
```

(Run from `StateLandGovernance.Web`.)
