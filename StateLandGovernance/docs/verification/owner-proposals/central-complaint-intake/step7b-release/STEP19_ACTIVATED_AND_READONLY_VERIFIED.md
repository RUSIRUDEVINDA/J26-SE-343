# Step 19 API and frontend activated; read-only verification completed

2026-10-25, Asia/Colombo. Owner-approved activation used the existing prepared API/frontend packages without rebuilding or changing product source. Both are live. Two live display cases remain unverified because the inspected ordinary records contain no ClarificationRequested or HumanReviewRecorded events; no test records were created.

| Surface | Actual state |
| --- | --- |
| API | PID **10144**, `.tmp/c4-step19-prepared/api/StateLandGovernance.Api.exe`, port 5264, Healthy/Live |
| API binding | Exactly `ASPNETCORE_URLS=http://localhost:5264` |
| Frontend | PID **39920**, `.tmp/c4-step19-prepared/web`, port 3000 |
| Served BUILD_ID | **DRFPggN7amhgFu7ERvWXg**, confirmed in sign-in HTML |
| Frontend internal/browser API | `http://localhost:5264`; fixture port 58118 absent from static assets |
| ML | Original PID **2412**, port 8104, ready/v2; not restarted |
| PostgreSQL | Original PID **7024**, port 5432, accepting connections; not restarted |
| Ordinary database | `state_land_governance_c4`, OID **34144**, 42 tables, **25 migrations**, expected closure index |

## Preflight, activation and preservation

All 88 prepared API manifest entries and 826 frontend entries passed SHA256 verification, as did retained source hashes and the expected BUILD_ID. Existing protected configuration was validated against its retained private baseline and approved delta without exposing credentials. Restricted C4 identity, read-only peer/account connections and disabled unrelated workers remain intact. Old API/frontend package manifests were also verified for recovery.

Before replacement, the old API PID 6224 was confirmed to use the Step 17 native package, and frontend PID 15544 served Step 18 BUILD_ID `2jzvVaGeJ9hEwUoYaZHjT` from the retained Step 18 package. Executables, command lines, listeners and working directories were checked. Only these two application processes were stopped. No recovery package fallback was needed.

The Step 19 API launcher started PID 10144 successfully. The temporary activation runner then waited on inherited output pipe handles despite the API being healthy. Only that stalled Python orchestration process was identified and stopped. The runner was corrected to redirect helper output to files; the healthy API was reused, and the prepared frontend started as PID 39920. This was an orchestration issue, not an API bind/health failure; neither package was changed or rebuilt.

Read-only snapshots before activation, after startup and after browser verification matched exactly: all 42 table row counts/digests, migration identities, sequences, columns and runtime table grants. OID 34144, the 25 migrations and the unique partial closure index remained correct. No migration, grant, seed or business-record write ran. Existing original findings and follow-up audit records were preserved.

## Read-only live browser results

The real localhost:3000 frontend and ordinary API were used with the existing documented DS identity and normal browser security. Browser interception allowed only GET/HEAD/OPTIONS and the existing sign-in/authentication-session endpoints. Zero business-write requests were attempted or blocked, and zero page runtime errors occurred.

**10 checks passed:**

1. Authoritative readiness matched saved current follow-up completeness across 10 existing owned 24-rule evaluations.
2. All 24 progressive cards retained their original saved rule order.
3. Compliant cards remained visible but excluded from follow-up actions; no duplicate officer-review list appeared.
4. Green Verified & Closed history badges and both orange Initial Outcome badges retained their existing styling.
5. Original engine findings remained expandable under Technical Details.
6. Missing follow-ups displayed Awaiting Officer Follow-up, identified the exact rules and kept Continue disabled.
7. Refresh and sidebar navigation retained the exact actor-scoped workflow, displayed evaluation and enabled Continue for the complete evaluation.
8. Continue navigated within C4 to Conflict Detection without an approval or business-write request; the Unicode arrow remained correct.
9. Separate Assessment History and data retrieval worked; historical-investigation label/dropdown remained hidden, populated and unselected.
10. Deliberate fresh Start Screening opened seven empty concerns without creating a record.

**Live verification limitations:** None of the 10 accessible owned evaluations contained saved ClarificationRequested or HumanReviewRecorded events, including their saved history. Accordingly this activation does not claim live confirmation of their Addressed badges or outstanding-concern text. Their save/persistence/completeness behavior remains supported by the prior Step 19 isolated PostgreSQL and prepared-browser proofs: 14 Compliant + seven Clarification Requests + three Human Reviews = 24 Addressed, with outstanding concerns retained. No ordinary synthetic data was created to fill this evidence gap. Existing live complete records exercise the unchanged terminal-closure route.

An initial browser assertion incorrectly expected the evaluation ID in the query-free sidebar URL. The corrected harness verified the actor-scoped stored IDs and exact displayed evaluation instead; the final journey passed. No application change was made to accommodate the harness. Initial attempts are not counted as passes.

## Manual startup guide

Updated `LOCAL_MANUAL_STARTUP_GUIDE.md` in this directory to reference Step 19 API/frontend packages and BUILD_ID. PostgreSQL and ML sections A/B were retained byte-for-byte. The guide contains no credentials or private connection-string values.

Release-specific operational helpers:

- `.tmp/c4-step19-start-api.ps1`: package/protected-configuration/read-only database validation; single localhost binding; hidden native API; prior environment restored.
- `.tmp/c4-step19-start-frontend.ps1`: manifest/BUILD_ID/API validation; internal API URL; hidden Next process; prior environment restored.
- `.tmp/c4-step19-stop-service.ps1`: dynamic listener identity checks; API executable or frontend command/served BUILD_ID checks; CheckOnly mode; no PostgreSQL control.

The stop helper was exercised against the positively identified old API/frontend using `-Release Step18`; the new Step19 CheckOnly paths were verified without gratuitously stopping the healthy new services. All 13 PowerShell guide blocks parsed. The guide's actual API/frontend start-and-health blocks were executed after activation and correctly reused the matching healthy services. Existing ML CheckOnly and PostgreSQL pg_isready checks passed. The guide includes bounded startup health polling and exact individual stop commands.

## Evidence and remaining state

Evidence in `.tmp/c4-step19-prepared/`: `activation-before.json`, `activation-preflight.json`, `activation-after-startup.json`, `activation.json`, `activation-after-browser.json`, `activation-postflight.json`, `orchestration-recovery.json`, package/source manifests, `live-api.*.log`, `live-web.*.log`, `browser-live/result.json` and screenshots, `manual-guide-preservation.json`, `manual-guide-tested.json`, and `manual-guide-before.md`. Temporary activation/browser/guide helpers and logs are retained under `.tmp/c4-step19-*`.

No activation blocker remains. The two absent ordinary follow-up types are explicit live verification limits, not claimed passes. New ordinary follow-up/save tests still require separate authorization. No product source change, rebuild, database write/migration/grant, ML/PostgreSQL restart, other-component change, Git staging, commit or push occurred. The earlier preparation report's prepared-only service state is superseded by this activation report.
