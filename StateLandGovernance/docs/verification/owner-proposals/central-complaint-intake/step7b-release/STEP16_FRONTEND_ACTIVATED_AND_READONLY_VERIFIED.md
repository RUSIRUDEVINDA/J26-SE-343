# Step 16 frontend activated and live read-only verification passed

2026-10-24, Asia/Colombo. Owner-approved frontend activation completed using the prepared package without rebuilding or implementing new product changes.

| Surface | Actual final state |
| --- | --- |
| Frontend | PID **34968**, port 3000, `.tmp/c4-step16-regulatory/web` |
| Served BUILD_ID | **vUoBXkxli3IRWEvDsD-uh**, confirmed in actual sign-in HTML |
| API | Original PID **37484**, port 5264, HTTP 200, Healthy, Live; not restarted |
| ML | Original PID **27388**, port 8104, HTTP 200, ready, model_version v2; not restarted |
| PostgreSQL | Original PID **7024**, port 5432, pg_isready: accepting connections; not restarted |
| Previous frontend | Step 15 PID 28724 stopped only after package/source/manifest/build/endpoint and service preflight passed |

All retained package SHA256 entries matched, packaged source matched current frontend source, and the approved BUILD_ID matched before stopping the old frontend. Static assets target http://localhost:5264 and contain no fixture port 58118. The new child uses C4_INTERNAL_API_BASE_URL=http://localhost:5264. Final listener inspection confirmed only the frontend PID changed. Old release artifacts remain retained.

## Actual localhost:3000 acceptance

**22 read-only checks passed**, normal browser security, zero page runtime errors. Browser request interception allowed GET/HEAD/OPTIONS plus sign-in and the authentication-session bridge. Zero ordinary business-write requests were attempted or blocked.

- Fresh Start Screening opened seven empty concerns without creating a record; no old toolbar or duplicate manual verification form appeared. Existing pre-save file selection, review and leave warnings remained available.
- An authorized saved Early selection different from the selected evaluation's Early request opened a clean Regulatory screen: no evaluation cards, follow-up form, old decisions or closed counts. Request tracking showed only the capabilities endpoint from that clean page, with no historical evaluation/follow-up fetch.
- The selected saved evaluation displayed **all 24 progressive rule cards**, in exactly the order of its saved API findings, using the restored original progressive components and styling. Compliant cards were visible and absent from the actionable selector.
- Returning to the exact saved evaluation restored precisely its own follow-up history and displayed closed-count state matching the real API. It did not run a new evaluation or inherit another evaluation's history.
- Explicit refresh, in-workflow navigation and sidebar Early -> Regulatory -> Early -> refresh retained exact workflow IDs. Invalid/stale selections did not substitute historical results. Fresh Start Screening and cross-officer isolation passed.
- Separate Assessment History / Reports remained accessible through its navbar page.
- Visible Unicode was checked, including **Continue to Conflict Detection →**. Raw main-screen NPD progression bullet lists stayed removed; collapsed Technical Details retained exact rule/hold information.

An initial harness assertion counted evaluation requests from the departing page during a navigation boundary. The clean page itself displayed no inherited data. The read-only harness was corrected to record each request's originating page; the final journey confirmed only Regulatory capabilities were fetched from the clean Early-only selection. No application changes or service retries were used to force acceptance.

## Remaining limitations

No new ordinary Early assessment or Regulatory evaluation was created during these checks. Clean Regulatory behavior was tested live with an existing authorized Early record and no selected evaluation. Genuine new-evaluation creation, zero initial follow-ups, fresh clarification/human-review/closure writes and durable isolation are established by the prior isolated PostgreSQL/browser proof in `STEP16_REGULATORY_CURRENT_PROGRESSIVE_UNICODE_VERIFIED.md`; they are not presented as new ordinary write tests.

Legitimate institutional/legal progression holds remain enforced. No migrations, grants, ordinary business-record writes, API/ML/PostgreSQL restarts, Git staging, commits or pushes occurred. Historical records were viewed only.

Evidence: `.tmp/c4-step16-regulatory/{activation-preflight.json,activation.json,activation-postflight.json,live-pid.json,live.stdout.log,live.stderr.log,browser-live/initial.json}` and browser screenshots (`fresh-early.png`, `current-early.png`, `closure-unsaved.png`, `clean-regulatory.png`, `history.png`). This document supersedes the preparation report's prepared-only service state.
