# Component 1 — demo verification checklist

Use this before submission or merge. Prerequisites: research DB imported (**400 parcels**), migrations applied (including valuation idempotency + authority decisions), API + Web env configured per [COMPONENT_01_LAND_INTELLIGENCE_RELEASE.md](./COMPONENT_01_LAND_INTELLIGENCE_RELEASE.md).

## Automated gates

```powershell
cd StateLandGovernance
dotnet test tests/UnitTests/LandIntelligence/StateLandGovernance.UnitTests.LandIntelligence.csproj --filter "Category!=Integration"

cd ..\StateLandGovernance.Web
npx tsc --noEmit
npm run build
# Prefer when Node supports the project test script; otherwise:
node --import ./tests/register.mjs --test tests/component-01.test.mjs tests/component-01-prompt10.test.mjs
```

## Manual E2E (research demo — disposable)

| Step | Route / action | Pass |
| --- | --- | --- |
| 1 | Dashboard: ~400 parcels, project lease-ready count, GIS **Unavailable** | |
| 2 | Find Suitable Land: purpose + target area + canonical land-use + Colombo | |
| 3 | Recommendations: chips show district/DS/area; cards show search district when filtered; ranked by suitability | |
| 4 | Edit search restores every previous value; reload URL preserves criteria (no duplicate silent loss) | |
| 5 | Exact and below-target area scoring visible on results | |
| 6 | Parcel detail: original land use vs GIS project classification; official availability **Not verified**; project designation separate | |
| 7 | Lease interest: create, save incomplete draft, reopen, submit with full validation | |
| 8 | Officer: refer to authority → save Agricultural/Commercial valuation → success popup → status still ReferredToAuthority | |
| 9 | Officer: save Residential valuation (separate request or revision) → confirm revision history | |
| 10 | Authority: complete latest worksheet visible; Approved on one disposable request; Rejected on another | |
| 11 | Applicant: after refresh, status + reason + timeline; valuation summary or clear load error/retry | |
| 12 | Stale authority decision / post-closure valuation rejected by API | |
| 13 | Expired session renews once or shows session-expired recovery; API failure / empty result / keyboard / mobile checked | |

## Distinctions (do not treat as defects)

| Concept | Correct interpretation |
| --- | --- |
| Research demo | Submission/local demo of the C1 journey |
| Project lease-ready | Dataset screening assumption |
| Simulated workflow status | Demo officer/applicant labels only |
| Official lease availability | Unknown / not verified — never inferred |
| ML ~76.5% | Synthetic rule-labelled held-out accuracy — **not** measured on the 400 real/project parcels |
| Valuation recorded | Does **not** change ReferredToAuthority by itself |
| Authority Approved | Preliminary project decision — formal lease processing still required |

Other non-defects: no document file upload; default research import has no GIS enrichment snapshots; officer/authority tools are demo-gated JWT, not production RBAC.

## Recorded verification (2026-10-18 Prompt 13)

| Check | Result | Evidence | Blocker |
| --- | --- | --- | --- |
| TypeScript | **Pass** | `npx tsc --noEmit` (exit 0) | — |
| Frontend component tests | **Pass** | `component-01.test.mjs` 51/51; `component-01-prompt10.test.mjs` 11/11 | — |
| Frontend production build | **Pass** | `npm run build` exit 0; C1 routes including authority queue/review | — |
| Land Intelligence unit tests | **Pass** | `dotnet test … Category!=Integration` — **450 passed** | — |
| Relevant integration tests | **Fail / env mismatch** | Workflow migrate error + GIS coverage Assert on connected DB | `LAND_INTELLIGENCE_CONNECTION` pointed at `land_intelligence` while live API metrics use research 400-parcel DB; re-point `.env` and re-run |
| Database migration | **Pass on connected EF DB** | `dotnet ef database update` — already up to date (includes `AddLeaseInterestValuationIdempotencyKey`) | Confirm same connection as the running API |
| Live API smoke | **Pass** | `GET /parcels/metrics` → 400 parcels / 400 designated; `POST /recommendations` → 200 | — |
| Live browser workflow | **Not run in this agent session** | Disposable E2E table above | Manual browser pass still required before claiming full demo sign-off |

## PR test plan template

Copy the manual E2E table into your PR description and note which DB and env flags you used. State clearly: research demo ≠ official lease availability.
