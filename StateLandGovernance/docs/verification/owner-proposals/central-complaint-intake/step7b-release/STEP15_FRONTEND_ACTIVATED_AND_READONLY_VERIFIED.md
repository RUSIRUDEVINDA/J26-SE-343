# Step 15 frontend activation - live read-only acceptance passed

2026-10-24, Asia/Colombo. The owner-approved frontend-only activation is complete. The prepared package was used without rebuilding or changing source. Step 15 is now served on localhost:3000.

| Surface | Actual final state |
| --- | --- |
| Frontend | PID **28724**, `.tmp/c4-step15-final/web`, port 3000 |
| Served BUILD_ID | **xnViROYZlEEuJ2PkpGBks**; verified in actual sign-in HTML |
| API | Original PID **37484**, port 5264, HTTP 200, Healthy, Live; not restarted |
| ML | Original PID **27388**, port 8104, HTTP 200, ready, model_version v2; not restarted |
| PostgreSQL | Original PID **7024**, port 5432; no restart, database access, migration or grant |
| Previous frontend | Step 14 PID 28244 stopped only after package/source/build/endpoint and service preflight passed |

Every retained package SHA256 manifest entry matched, and packaged source matched current frontend source. BUILD_ID matched the approved value. Static assets contain localhost:5264 and no fixture port 58118. The child process uses C4_INTERNAL_API_BASE_URL=http://localhost:5264. API, ML and PostgreSQL listener identities were checked before and after replacement; only the frontend changed. Old artifacts remain retained.

## Real localhost:3000 browser acceptance

**22 checks passed**, normal browser security, zero page runtime errors. Request interception allowed only GET/HEAD/OPTIONS plus authentication sign-in and session bridge. No ordinary business-write request was attempted or blocked. No Save Draft, finalization, screening Run or closure submission was performed.

- Fresh Start Screening opens seven empty concern controls without a record write or old history toolbar.
- An invalid file is rejected before save. A valid PNG is selected before any draft ID exists, reviewed through its original-byte download, retained when navigation is cancelled, and removed successfully.
- All seven attachment fields remain present and aligned; a pending-file mobile screenshot was visually inspected.
- Existing saved Early result and Regulatory evaluation retain their exact identities and current saved data.
- Actionable Regulatory selector includes only Insufficient Information and Human Review Required findings. In the inspected owned evaluation all actionable findings are already terminally closed: the selected action is explicitly disabled as Already Verified & Closed, with its duplicate-prevention reason. Saved Run is separately disabled to prevent a duplicate evaluation; progression holds remain separate. No unresolved ordinary finding was created to force an eligible-write scenario.
- Saved Verified & Closed dispositions are prominent and remain available through refresh and navigation. Existing original engine outcomes remain accessible in collapsed Technical Details, which was opened during the check.
- The raw persisted NPD bullet list is absent from the main workflow; the concise summary and collapsed exact progression requirements remain. Relevant finding cards still show outstanding actions; no real hold was removed.
- Deep link -> sidebar Early -> Regulatory -> Early -> refresh, Back/Continue, fresh Start Screening, invalid/stale IDs and cross-officer isolation pass.
- Assessment History remains on its separate navbar page.

One initial browser assertion incorrectly searched the original-engine details for capitalized Regulatory; the actual heading is Original regulatory findings. The harness assertion was corrected and all final checks passed. No product change was made for that harness issue.

## Remaining limits

This activation adds live read-only acceptance; it does not claim new ordinary write-path verification. The prior Step 15 report retains isolated PostgreSQL evidence for attachment persistence/retry and closure durability, and controlled browser evidence for eligible closure with empty remarks/references. Actual ordinary synthetic save/upload/closure testing still requires separate authorization.

Browser-selected File contents do not survive reload: retained descriptors request exact reselection where needed. Save Draft coordinates existing sequential endpoints; partial progress is reconciled by the retained submission and file keys. Institutional/legal progression holds remain enforceable after officer closure.

Evidence: `.tmp/c4-step15-final/activation-preflight.json`, `activation.json`, `activation-postflight.json`, `live-pid.json`, `live.stdout.log`, `live.stderr.log`, `browser-live/initial.json` and screenshots including `pending-mobile.png`. The implementation/preparation report remains `STEP15_WORKFLOW_UI_PREPARED_AND_VERIFIED.md`; this document supersedes its prepared-only service status.

No migrations, grants, ordinary business-record writes, API/ML/PostgreSQL restarts, source edits, Git staging, commits or pushes occurred during activation. Existing records and history were preserved.
