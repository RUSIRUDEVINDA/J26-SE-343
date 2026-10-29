# Step 17 API and frontend activated; live C4 resolution verified

2026-10-24, Asia/Colombo. The owner-approved Step 17 native API and frontend are running. Prepared packages were activated without rebuilding or changing product source.

| Surface | Actual final state |
| --- | --- |
| API | PID **30832**, `.tmp/c4-step17-prepared/api/StateLandGovernance.Api.exe`, port 5264, HTTP 200, Healthy, Live |
| API binding | Exactly `ASPNETCORE_URLS=http://localhost:5264` |
| Frontend | PID **28152**, `.tmp/c4-step17-prepared/web`, port 3000 |
| Served BUILD_ID | **tQWx2vNmytvmgV5k-2kDY**, confirmed in actual sign-in HTML |
| ML | Original PID **27388**, port 8104, HTTP 200, ready, model_version v2; not restarted |
| PostgreSQL | Original PID **7024**, port 5432, accepting connections; not restarted |
| Ordinary database | state_land_governance_c4, OID **34144**, **42 tables**, **25 migrations** |

## Activation and preservation

Both retained package SHA256 manifests and the source manifest matched. The frontend BUILD_ID and localhost:5264 browser endpoint matched; fixture port 58118 was absent. Existing protected configuration was validated against its retained original/private hash and reviewed candidate delta without exposing values. Restricted runtime identity, read-only peer/account connections and disabled unrelated workers/sandboxes remained intact.

The preflight confirmed exact database identity, all 25 migration identities, expected columns/runtime grants and the unique partial `ux_regulatory_finding_closure` index. A new explicitly read-only preservation snapshot captured current records after the owner's intervening manual use; it did not assume older Step 13 business-row digests still described those later records. Two preflight reads matched before stopping services.

Only the positively identified old frontend PID 34968 and API PID 37484 were stopped. The native API started with one localhost binding, passed health and remained running before the frontend started. The actual new executable path and listener identities were rechecked afterward.

After startup and again after browser acceptance, **all 42 table counts/digests, columns, sequences, migration history and runtime table grants matched the new pre-activation baseline exactly**. No migration, grant, schema change, business write, approval event or closure replay occurred. No ordinary historical row was rewritten. Existing saved DS dispositions now affect the read-only C4 progression calculation as intended.

## Actual live browser results

**14 read-only checks passed**, normal browser security, zero runtime errors. Business request interception allowed only GET/HEAD/OPTIONS plus sign-in and the authentication-session bridge. Zero ordinary business-write attempts were made or blocked.

- Existing owned evaluation `0f1bea66-d3aa-4b47-9379-4fc920dd938e` has **14 Compliant + 10 previously saved DS closures**. The live UI/API now report **24 resolved / 0 unresolved** and enable Continue to Conflict Detection.
- All 24 progressive cards remain in exactly the original saved engine order. Verified & Closed is the primary current status; Compliant cards remain visible and excluded from the follow-up selector.
- Original engine findings remain available through expandable Technical Details. The duplicate current-officer-review list is absent; saved event audit history remains expandable.
- Reload and query-free sidebar Early -> Regulatory navigation preserve the exact evaluation and enabled state.
- Clicking Continue opens the existing C4 Conflict Detection view without a business POST, approval event or database write. It means only advance within C4 screening; no lease, land-use, legal-compliance or institutional approval is granted. The Conflict capability limitation remains visible.
- Separate existing owned evaluation `c5983ade-cc00-41e8-8308-994439f67d2a` remains unresolved. Every server-reported outstanding rule is individually identified in the UI and Continue stays disabled.
- An Early-only Regulatory selection displays no historical evaluation/cards/follow-up form. No previous follow-ups are inherited into that clean selection.
- Separate Assessment History, fresh seven-empty-concern Start Screening, sidebar persistence and the correct Unicode continuation arrow remain working.

The all-resolved live screenshot was visually inspected. No fixture API or mocked follow-up responses were used for these live checks. The existing saved closures were read, not replayed or newly created.

## Remaining limits

No new ordinary investigation, Early assessment, Regulatory evaluation or follow-up was created. Fresh-evaluation persistence, final-closure write/revision behavior, unauthorized-write denial and audit immutability remain covered by the Step 17 isolated PostgreSQL/browser verification. This activation verifies the changed calculation against already saved ordinary dispositions and an existing unresolved evaluation. Newly created ordinary synthetic write testing still requires separate authorization.

No Early Governance or other component implementation changed during activation. ML/PostgreSQL remained healthy on their original processes. No Git staging, commit or push occurred. Old packages remain retained.

Evidence: `.tmp/c4-step17-prepared/{activation-preflight.json,activation-before.json,activation-after-startup.json,activation-after-browser.json,activation-pids.json,activation.json,activation-postflight.json}`, `live-api.*.log`, `live-web.*.log`, and `browser-live/{result.json,24-resolved.png,unresolved.png}`. This report supersedes the preparation report's prepared-only service state; its isolated test evidence and C4-only approval boundary remain applicable.
