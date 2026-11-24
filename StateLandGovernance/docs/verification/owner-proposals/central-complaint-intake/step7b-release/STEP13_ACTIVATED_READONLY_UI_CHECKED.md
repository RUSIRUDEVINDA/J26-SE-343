# Step 13 local activation recovered and read-only UI checked

2026-10-24, Asia/Colombo. Prepared release activated without rebuilding, backup replay, migrations, grants or product source changes. Activation is running; one UI acceptance limitation remains.

| Surface | Actual result |
| --- | --- |
| Native API | PID 37484, `.tmp/c4-step13-approved-local/api/StateLandGovernance.Api.exe`; single `ASPNETCORE_URLS=http://localhost:5264`; HTTP 200, Healthy, Live |
| Frontend | PID 19524, prepared `.tmp/c4-step13-approved-local/web`; port 3000; served BUILD_ID `AEGg-BevAWL_DD4P-ysYx` |
| Browser API endpoint | Prepared static assets use `http://localhost:5264`; no fixture port 58118 |
| ML | Original PID 27388; port 8104; HTTP 200, ready, model_version v2; not restarted |
| PostgreSQL | Original PID 7024; database state_land_governance_c4, OID 34144, 42 tables, 25 migrations; not restarted |

All prepared package manifest hashes passed before startup. The failed API PID 28012 was absent and ports 5264/3000 were free before launch. Closure unique partial index was present. The ordinary database matched the entire saved post-Step13 snapshot before startup and after browser checks: all 42 table counts/digests including migration history, sequences, columns and runtime grants unchanged. Neither migration was replayed.

## Actual browser acceptance

Real prepared frontend and ordinary API were used with the existing documented DS development identity. Browser interception allowed only GET/HEAD/OPTIONS plus sign-in and the authentication-session bridge. No business-write request was attempted or blocked. No page runtime errors occurred.

- Fresh Start Screening opened seven empty concern selectors without creating a record. No old New/Open Existing/History toolbar, Previous Assessments or duplicate manual verification form.
- Explicit saved Early request `944608c6-e20d-47dd-a477-85102ff6e281` displayed its current finalized investigation result without the duplicate form.
- Saved Regulatory evaluation `149231da-cf62-4567-b62d-06f7ef907a6f` displayed only seven Insufficient Information and three Human Review Required findings in the actionable selector; its 14 Compliant findings were excluded.
- Selecting No Issues Found — Verify and Close produced an enabled closure button, zero textareas and zero upload controls. Save was never clicked. Original results and unresolved statuses remained visible. The closure screenshot was visually inspected.
- Refresh retained the explicit Early/Regulatory IDs; in-workflow Back/Continue buttons retained both IDs.
- Separate Assessment History / Reports navbar navigation worked.
- **FAILED acceptance: query-free sidebar restoration after opening an existing chain by explicit deep link.** Sidebar Early Governance opened the empty investigation form instead of the selected saved result. Actor-scoped active-workflow storage was null. This reproduces with this prepared release; successful explicit-URL refresh and button navigation do not establish sidebar persistence. No source workaround was applied. The preceding Step13 report's blanket navigation claim is therefore not established for this live deep-link scenario.

Read-only Kiwulakale/demo reports were viewed only; no existing proposal or application record was modified. Ordinary synthetic write/durability testing remains pending and was not authorized here. No staging, commit or push.

Evidence: `.tmp/c4-step13-approved-local/recovery-{preflight,pids,activation,final-preservation}.json`; `api-recovery.*.log`, `web-recovery.*.log`; browser `initial.json`, `fresh-early.png`, `current-early.png`, `closure-unsaved.png`, `history.png`. Earlier browser harness attempts used an incorrect Proposals URL and overly strict implicit-label locators; corrected harness reached the genuine sidebar-restoration failure above. Initial attempts are not counted as passes. Two pre-start helper errors (OID JSON string comparison and index query JSON decoding) were corrected without any database/service action; actual preservation and package checks passed before startup.
