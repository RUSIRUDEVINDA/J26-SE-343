# Step 18 services recovered, frontend activated and manual startup documented

2026-10-25, Asia/Colombo. Owner-authorized recovery is complete. Existing frozen ML, the retained Step 17 native API and the prepared Step 18 frontend are running. No application source edit or package rebuild was performed.

| Service | Actual final result |
| --- | --- |
| ML | PID **2412**, localhost:8104, HTTP 200, ready, model_version v2 |
| API | PID **6224**, localhost:5264, HTTP 200, Healthy, Live; one ASPNETCORE_URLS binding |
| Frontend | PID **15544**, localhost:3000, BUILD_ID **2jzvVaGeJ9hEwUoYaZHjT** confirmed in served HTML |
| PostgreSQL | Original listener PID **7024**, accepting connections; Windows service **postgresql-x64-18**, wrapper PID **4312**, Running/Auto; not restarted |
| Ordinary database | state_land_governance_c4, OID **34144**, **42 tables**, **25 migrations**, expected unique partial closure index |

The API and frontend match their approved SHA256 manifests. The ML virtual environment and both frozen model hashes match the previously verified artifacts. Existing protected API configuration was validated against its retained original hash and approved candidate delta without exposing values. Runtime restrictions, read-only peer/account connections and disabled unrelated workers remain. No replacement configuration or credentials were created.

Startup used the existing `Start-C4Ml.ps1` launcher (same ML working directory, bundle verification and uvicorn src.http_service:app arguments), `.tmp/c4-step18-start-api.ps1` (safe protected environment loader, exact single localhost binding and correct native package working directory), and the existing Node/Next production binary against `.tmp/c4-step18-prepared/web` with C4_INTERNAL_API_BASE_URL=http://localhost:5264. No duplicate services were started.

## Database and service preservation

Explicitly read-only preflight and postflight snapshots matched all 42 table row counts/digests, columns, sequences, runtime grants and complete migration history. OID and closure index remained correct. PostgreSQL service wrapper/listener PIDs remained unchanged. Model hashes and package manifests were checked again after browser verification and remained unchanged.

A PowerShell native-argument quoting issue in the new safe helper's read-only SQL was detected during ValidateOnly and corrected by supplying the same read-only query on stdin before starting any application service. No configuration or SQL semantics changed. A captured-output pipe then held the temporary orchestration runner after it had already launched a healthy API. Only that temporary Python orchestration process was stopped; healthy ML/API listeners were reused, and the frontend was started once. This was an orchestration correction, not an API/ML restart or a change to their startup method. The documented API helper is invoked directly; automation guidance explains file redirection rather than inherited pipe-EOF waits.

## Real application browser acceptance

**16 read-only checks passed**, no page runtime errors, no business-write attempts/blocks. Browser routing allowed only GET/HEAD/OPTIONS plus existing authentication sign-in/session bridging. No fixture or mock business API response was used.

- All 24 progressive rule cards remain visible and in their original engine order.
- Existing 14 Compliant plus 10 saved closures produce 24 resolved / 0 unresolved and enable Continue.
- Saved history shows compact green Verified & Closed badges and orange Initial Outcome: Insufficient information / Initial Outcome: Human review required badges. Computed existing success styling and wrapping/alignment were checked; the actual badge screenshot was visually inspected.
- Original outcomes, audit history and saved dispositions remain unchanged and accessible in Technical Details; the duplicate officer-review list remains absent.
- A separate unresolved evaluation identifies outstanding findings and disables Continue.
- Historical investigation label/dropdown are hidden, remain in the DOM with loaded options and an empty selection, and do not automatically open an investigation. Other history/report access remains available.
- Refresh, query-free sidebar navigation, separate navbar History, fresh seven-empty-concern Start Screening, clean Early-only Regulatory selection and correct Unicode remain working.
- Continue opens the C4 Conflict view without a business write or implied external approval.

## Manual startup guide and safe local helpers

Saved guide:

`StateLandGovernance/docs/verification/owner-proposals/central-complaint-intake/step7b-release/LOCAL_MANUAL_STARTUP_GUIDE.md`

It contains actual copy-paste Windows PowerShell commands for the existing PostgreSQL Windows service, verified Python virtual environment/module/arguments, protected API loading, exact native/frontend working directories, startup order/terminals, health/build checks, individual identity-checked stops/restarts and troubleshooting. All 14 PowerShell command blocks parse successfully. PostgreSQL's confirmed conditional Start-Service command is documented but was not executed because the service was already healthy. No PostgreSQL stop/restart command was performed.

Local-only helpers added under `.tmp/` (not product source):

- `c4-step18-start-api.ps1`: verified startup plus read-only validation and safe reuse of the healthy PID. It holds no credentials and prints no private configuration.
- `c4-step18-stop-service.ps1`: dynamic port/PID/executable/module validation; CheckOnly paths were verified for ML/API/Frontend without stopping them. Actual stop commands are documented for deliberate future use, not executed during final verification.

The API helper was re-invoked after recovery and reported reusing healthy PID 6224, confirming no duplicate/restart. A separate all-in-one/PostgreSQL daemon was not introduced. The guide explains which foreground terminals must remain running and why retained `.tmp` packages/config/helpers must be preserved.

## Limits and handoff

No ordinary save/upload/closure write test was performed. Such new synthetic ordinary testing still needs separate authorization. This recovery verifies actual existing ordinary records read-only and uses the prior isolated proofs for write-path evidence.

No product source changes, package rebuilding, migrations, grants, database initialization/seeding, ML retraining, PostgreSQL restart, unrelated component/integration changes, Git staging, commit, push, reset or stash occurred. Existing work and historical records remain intact.

Evidence: `.tmp/c4-step18-recovery/{preflight.json,before.json,after-startup.json,after-browser.json,activation.json,pids.json,postflight.json}`, ML/API/frontend logs, `browser-live/{result.json,saved-history-badges.png,history-hidden.png,24-resolved.png,unresolved.png}`, and retained local helper scripts. The manual startup guide is the future-use handoff, and this report supersedes the earlier stopped/prepared-only Step 18 status.
