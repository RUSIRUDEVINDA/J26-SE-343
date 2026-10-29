# Central complaint assignment and direct handling — combined activation checkpoint

2026-10-23 (Asia/Colombo). **Implemented and isolated-tested. Ordinary activation is NOT authorized or performed. STOP for separate explicit approval.** This supersedes neither historical audit evidence nor earlier assignment-only evidence; it combines the two reviewed additive changes for a future deployment decision.

## Implementation and exact SQL

Existing assignment migration `20261022200907_AddCentralComplaintAssignments` remains unchanged. New migration `20261022205550_AddComplaintHandlingEvents` creates only the approved 22-column append-only handling table, four primary/unique constraints, three restrictive foreign keys, ten CHECK constraints and three explicit query indexes. Both Down methods refuse destructive history removal. No historical payload/backfill or existing-table column change is included.

- [COMBINED_REVIEWED_FORWARD.sql](COMBINED_REVIEWED_FORWARD.sql): generated EF SQL from existing ordinary migration `20261022152556_AddOfficerInvestigationsAndAssessmentRequests` through BOTH new migrations, in order. Never replay predecessors.
- [REVIEWED_FORWARD.sql](REVIEWED_FORWARD.sql): preserved original assignment-only SQL.
- [HANDLING_REVIEWED_FORWARD.sql](HANDLING_REVIEWED_FORWARD.sql): generated handling-only delta, usable only when the assignment predecessor is already present and verified.
- [COMBINED_REVIEWED_ADDITIONAL_PERMISSIONS.sql](COMBINED_REVIEWED_ADDITIONAL_PERMISSIONS.sql): exactly SELECT/INSERT on assignment and handling tables for existing `slg_c4_regulatory_runtime`. No new UPDATE/DELETE/DDL/schema/sequence/role/account/peer/outbox permission.
- [HANDLING_CHANGED_FILES.json](HANDLING_CHANGED_FILES.json): task delta with before/after hashes. [HANDLING_IMPLEMENTATION_SOURCE_SHA256.json](HANDLING_IMPLEMENTATION_SOURCE_SHA256.json) records the reviewed implementation set. The broader working tree contains pre-existing unrelated changes.

The existing command engines, classifier, anomaly/conflict processing, C1/C2/C3, authentication identities/passwords, workers and blockchain are unchanged. A fresh host directory read remains the authority for target eligibility. The C4-only handling store uses the same narrowly scoped complaint-key transaction lock as routing/review; no lock spans classifier HTTP. Assignment+handling and legacy review+handling commit or roll back together. Required handling revisions prevent stale actions after takeover, resolution or reopening. Same-key/content replay returns history and current effective responsibility separately; changed content conflicts. A historical assignment never restores access.

Central permissions are explicit Read, Assign and Review, with operation-specific ReviewOperations. Commissioner institution alone grants nothing. Direct handling, DS investigation, notes, information requests, findings, resolution, closure and explicit reopening preserve immutable event history. Central final decisions require actual findings, actions and decision basis plus applicant-safe summary. They do not verify allegations or confer land approval. DS has only current effective active complaint scope and four investigation operations; terminal handling, takeover and reassignment revoke its reads/actions/reviews/retries. Reopening restores direct Commissioner responsibility, never an old DS assignment automatically.

Applicant history contains public summaries only; actor references, internal notes and evidence references are omitted. Current DS history is limited to its current assignment episode and permitted public entries. Evidence references are bounded metadata, never fetched or promoted to verified truth. Existing original complaint/classification persistence remains intact. Applicant information upload/response is not a new contract in this scope; officers record genuine received references through the reviewed actions.

## Actual isolated verification

Docker PostgreSQL18, loopback port57657, separate fictional databases only. Image digest `sha256:edb0c08dc33befd0262ab3b5cf1c4f3f8cfd8b09e88c05b45adee29d6761ddb6`. No ordinary storage was used as an integration-test target.

| Gate | Observed result |
|---|---|
| Focused backend regression | Earlier configured run: 241 passed, 0 failed/skipped. Final rerun: 235 passed, 6 failed because isolated PostgreSQL connections were refused; subsequent Docker API check confirmed Docker Desktop engine unavailable. This is not a final all-green rerun claim. |
| Architecture | 5 passed |
| Frontend | 139 passed; focused lint 0 errors, one existing ref-cleanup warning |
| Complete controller/HTTP proof | 64 checks passed, including both direct Commissioner and DS handling paths |
| Supplemental transaction/concurrency/storage | 18 checks passed |
| Another-process retrieval | 9 checks passed; zero model calls |
| EF snapshot | No pending model changes |
| TypeScript | Only three existing C1 failures: RecommendationsView.tsx309 TS2345; ParcelCatalog.tsx131/143 TS18048 |

Evidence: [HANDLING_HTTP_EVIDENCE.json](HANDLING_HTTP_EVIDENCE.json), [HANDLING_CONCURRENCY_AND_ROLLBACK_EVIDENCE.json](HANDLING_CONCURRENCY_AND_ROLLBACK_EVIDENCE.json), [HANDLING_RESTART_RETRIEVAL_EVIDENCE.json](HANDLING_RESTART_RETRIEVAL_EVIDENCE.json), [HANDLING_TEST_SUMMARY.json](HANDLING_TEST_SUMMARY.json). These exercise actual controllers/repositories, restricted PostgreSQL rights, trusted directory revocation, grant expiry, owner/foreign/anonymous denials, current/historical replay, required revisions, competing writes, review-versus-resolution ordering, injected transaction rollback, canonical typed-column hashes, denied UPDATE/DELETE/schema-create and unchanged saved classification.

Final isolated complaint `2c86e0f8-540f-4c17-a8a0-d3bf524c656c`; classification assessment `f14c48c3-30b1-4452-8ccb-e3de4c158a8d`, model v2, actual category Lease Revenue / Payment / Enforcement. Probabilities: administrative0.32551130918636795; revenue0.41560552353101300; unauthorized-use0.12522691169455540; environmental0.13365625558806360. Final handling revision21 Resolved, event `5cb5bee4-279b-43d0-9cee-5040f51718e9`. These IDs are ONLY isolated fixtures, not ordinary URLs or real allegations.

Three genuine classifier calls total in this task: one in each independent fresh proof database. The first proof exposed an event-hash serialization field-name collision, fixed and covered by the review/rollback proof; the second stopped on an overly broad test inbox assertion, corrected to check the relevant complaint. Final fresh proof used exactly one real call. Supplemental/restart/GET/replay made zero additional calls. No anomaly inference or ordinary complaint/model call occurred. Initial missing-test-connection failures were rerun successfully before the later Docker outage; all limitations remain recorded. No whole-repository or production-build green claim is made. NuGet vulnerability-feed/unrelated existing compiler warnings remain.

Browser tools are unavailable in this session; no visual/keyboard/responsive/authenticated browser click-through success is claimed. Frontend automated tests verify server-derived controls and applicant-safe history. Manual checks below remain pending AFTER activation approval.

## Ordinary preservation

[HANDLING_ORDINARY_READ_ONLY_EVIDENCE.json](HANDLING_ORDINARY_READ_ONLY_EVIDENCE.json) is fresh SELECT/catalogue evidence with transaction_read_only=on: target `localhost:5432/c4_handover_local`, 17 migrations unchanged, both new tables absent, all audited complaint/classification fingerprints unchanged. The three existing complaints and classifications remain preserved. The existing exact Commissioner identity/profile pairing is rechecked without exposing private profile contents. [HANDLING_PROTECTED_SOURCE_VERIFICATION.json](HANDLING_PROTECTED_SOURCE_VERIFICATION.json) verifies protected identity/reference/module files and frozen artifact hashes. The registered-complaint controller is intentionally extended by this approved handling scope; it is not claimed unchanged.

No ordinary migration, permissions, private configuration, API restart/deployment, account/password change, assignment, handling action, complaint submission, model call, worker/Groq/ledger operation, commit or push occurred. A current complete ordinary backup/restore is NOT claimed. Earlier archives/evidence remain untouched. Docker later became unavailable; a successful final container-stop operation cannot be claimed, although no isolated listener was reachable. Retained test storage must be checked when Docker becomes available again.

## Proposed exact time-limited central configuration — NOT ENABLED

[COMBINED_PROPOSED_ACTIVATION_SETTINGS.json](COMBINED_PROPOSED_ACTIVATION_SETTINGS.json) is documentation, never merged into private runtime settings. Existing actor `0ab669fe-d0ab-5695-ad55-10f6b3b5aab3`, exact Officer/COMMISSIONER profile. Proposed new grant `3970d3d2-5fca-4858-a9ae-e98ed04f9809`; Read/Assign/Review; operations TakeDirect, AssignDs, StartInvestigation, RecordNote, RequestInformation, RecordFindings, Resolve, Close, Reopen.

Proposed interval **2026-10-24T03:30:00Z through 2026-10-25T03:30:00Z**, Sri Lanka24Oct09:00 to25Oct09:00. This is an unissued operator-review proposal, not proof of appointment or successful login. If approval occurs outside this interval, review an exact replacement grant/interval; never silently extend it. No new identity, role, password or ordinary account change is needed.

## Fresh backup, restore and controlled activation plan — NOT EXECUTED

1. Obtain separate approval for BOTH SQL migrations, exact two-table grants, exact timed central configuration and a reviewed compatible package/launcher. Recheck source/review hashes, ordinary API/binary identity, private configuration, all current schemas/history/grants/fingerprints and existing reports. Preserve read-only C1/C3/accounts settings, all working features, disabled sandboxes/worker/Groq/ledger settings. Stop for unexplained drift.
2. Create a NEW read-only pg_dump of only localhost5432/c4_handover_local in secure ignored storage; never overwrite old archives. Record absolute path, size, UTC timestamps and SHA256. Confirm activation-account readability and pg_restore listing. Restore into a NEW isolated Docker database; compare normalized schema/history, every original-row fingerprint and original grant metadata, including all current complaints/classifications/reviews/assessments/audits/outbox/receipts. Preserve concurrent additions; refresh baseline rather than removing records. Do not restore over ordinary storage. Docker must be healthy before proceeding.
3. Apply the exact combined SQL/grants ONLY in the isolated restored copy first. Reconstruct runtime permissions only inside isolation. Recheck empty new tables/no backfill, restrictive FK/check/uniqueness, denied broader rights and existing original-row fingerprints. Re-run the six database regressions currently blocked by Docker. Publish/build the reviewed current API and validate composition against this compatible pair of migrations. A NEW reviewed launcher must guard exact target/settings/package/single listener; no launcher is implemented or private settings changed here.
4. After all gates pass and approval is recorded, stop the identified ordinary API BEFORE changing schema/grants/settings. Apply only the combined SQL to the exact approved target with separate operator credentials, then exact SELECT/INSERT delta. Do not replay already-applied migrations. Merge only the reviewed CentralComplaints section into a preserved private configuration copy. Start one compatible new API; reuse existing healthy frontend/ML. No migrations as incidental startup, worker processing, complaint/classifier submission or automatic assignment.
5. Reauthenticate existing identities. Verify anonymous/capability/owner denials and all existing report preservation BEFORE any separately authorized real assignment/action. Existing complaints start Unassigned with no backfill. Ordinary handling/assignment IDs and browser success must be recorded only after actual approved operations; none exist from this task.

## Recovery safeguards

Stop the new API; disable CentralComplaints Enabled/WritesEnabled and grants. Revoke ONLY SELECT/INSERT on the two new tables before any older binary starts. Retain both tables, migration-history rows and append-only events; never destructive Down/delete/rewrite or restore over ordinary C4. Inspect partial SQL/config/grant outcomes before continuing.

**An older assignment-only or case-reviewer binary is not an equivalent authorization rollback.** It ignores terminal handling and may re-expose historical DS/case scope. Keep complaint officer access disabled or use a separately reviewed compatible read-only package. Review and demonstrate that recovery package/settings before activation. Do not assume revoking new-table grants alone prevents every legacy reader. Restore archives only into NEW isolated recovery databases for separately reviewed repair.

## Manual browser walkthrough after separate activation

1. Sign in with the exact time-granted central account. Open `http://localhost:3000/component-04/central-complaints`. Confirm Unassigned, Under Commissioner Review, Assigned to DS and Resolved/Closed tabs. Ungranted users cannot access central data.
2. Open an existing complaint; inspect original text, actual saved classifier result, separate triage/lifecycle status and historical assignments. Refresh must issue reads only. Do not create ordinary fictional complaints for testing.
3. Direct path: TakeDirect with a real reason; StartInvestigation; record actual notes/findings/references; RequestInformation with a public summary where appropriate. Only server-allowed controls appear. Resolve requires outcome/findings/actions/decision basis and public summary plus explicit confirmation. Close with confirmation; Reopen requires specific authority/reason/basis and returns Commissioner responsibility.
4. DS path: AssignDs to an eligible live directory target using current assignment AND handling revisions. DS opens `/component-04/complaint-inbox`, starts investigation and records permitted actions. Central reviews findings and resolves; DS reload/detail/history/review/retry must then be denied. Reassignment revokes the former DS immediately; historical replay must not reinstate it.
5. Applicant opens `/workflow-governance/applicant/complaints`: original/classification and public lifecycle summaries remain visible; internal notes, responsible actor IDs and private evidence references do not. Another applicant remains denied.
6. Reopen/refresh/history, sign out/in and switch accounts; verify no stale private response or automatic write/classification. Check keyboard focus, terminal confirmations, loading/error/uncertain-replay states and narrow-screen layout. Record observations; these visual checks have not been performed.

**Stopped at the user-required boundary. No ordinary activation is authorized by this report.**
