# C4 Step 8A local run readiness

2026-10-24, Asia/Colombo. **NOT READY FOR ACTIVATION APPROVAL.** This is a read-only local-readiness record. No ordinary migration, grant, record, configuration, package, service, Git index, commit or push was changed.

## Current checks

| Check | Result |
| --- | --- |
| Working-tree TypeScript | Passed using the installed TypeScript binary. |
| Frontend tests | 246 passed, 0 failed. |
| Next.js production build | Passed; all 37 routes generated. |
| API Release build | Passed with 0 errors; NuGet vulnerability-feed lookup warnings only. |
| Architecture tests | 7 passed, 0 failed. |
| Governance unit tests | 970 passed, 12 failed only because `COMPONENT4_TEST_POSTGRES_CONNECTION` was unset and the Python ML smoke launcher was unavailable. Those tests explicitly reject ordinary databases. |
| API unit tests | 39 passed, 1 failed: `RegisteredComplaintHttpTests` expects `403` while the current non-enumerating complaint policy returns `404`. This is a real test/contract mismatch requiring separate review. |
| Ordinary preflight | Passed read-only: OID 34144, 41 tables, 21 migrations, historical rows/schema/history/sequences/durable grants unchanged, private configuration/package unchanged. |
| Backup | `state_land_governance_c4.dump`, SHA256 `1b5f7ba0872f0cf59fe9f118c06cb3079f9c6a965120b568e31647e3f3032dd0`; listing/readability and prior isolated restore verified. |
| Frozen models | Complaint v2 `63b37eb7175e74a8dcde82bf042fcf9dcbb4f0432fbe3b4677aac037007a20ae`; anomaly v1 `59ed4a73d56d9f06798c4ebc4684a633afe72841c41ed0b3ba4e7bb658782b46`. |

The ordinary API, frontend and ML ports 5264, 3000 and 8104 were not listening at the check. PostgreSQL 5432 was not altered. The working tree contains 959 uncommitted entries after evidence generation; none were reset, staged or removed.

## Approved migration materials for a later activation approval

1. `step1-early-governance-20261024/REVIEWED_FORWARD.sql` — SHA256 `5af0fe8d358b9c22cad2c8a68007b8503f492230d155e46a0c7b34a191916c6f`; adds the attachment table and finalization operation/index, then records `20261024000805_AddInvestigationEvidenceAndFinalization`.
2. `step2-regulatory-followup-20261024/REVIEWED_FORWARD.sql` — SHA256 `bf2bf502852ecc08e4abdd63f108955cb7a397ffae27a5bdb2020f070f029489`; extends the regulatory follow-up CHECK and records `20261024022733_AllowRegulatoryNoFollowupRequired`.
3. `step1-early-governance-20261024/REVIEWED_ADDITIONAL_PERMISSIONS.sql` — only `SELECT, INSERT` on `governance_intelligence.officer_investigation_attachments` for `slg_c4_regulatory_runtime`.

## Later controlled activation sequence — do not run now

1. Re-run the read-only preflight. If any record or schema fingerprint changed, create a new complete read-only backup and restore it to a new isolated PostgreSQL database before continuing.
2. With a separate operator credential and the API stopped, connect only to `localhost:5432/state_land_governance_c4`; verify OID 34144 and exactly 41 tables/21 migrations before writes.
3. Apply only the Step 1 forward SQL, then only the Step 2 forward SQL, then only the attachment permission SQL. Do not replay predecessors, run EF startup migrations, run destructive Down, or broaden grants.
4. Read-only verify OID 34144, 42 tables, 23 migrations, both exact IDs, empty new attachment table unless genuine business records exist, preservation fingerprints, and attachment `SELECT`/`INSERT` only.
5. Run `Start-C4Step7BApprovedApi.ps1 -ValidateOnly` using the approved private candidate. It must accept the exact post-migration target and reject a wrong target or missing rights.
6. Only after explicit approval, start ML on 8104 with `Start-C4Ml.ps1`; it verifies the frozen bundle before binding. Start one approved API on 5264 using the fail-closed launcher, then start the built frontend on 3000. Reuse a healthy listener; never duplicate one.
7. Verify `/health`, model bundle health, sign-in, C4 admission routing, existing saved reports and denial behavior before any ordinary write action.

Recovery: stop the verified new API; do not restore over ordinary storage or run Down. Revoke only the new attachment table rights before considering a compatible recovery package. Preserve migration history and append-only data.

## Manual verification after a separately approved activation

| Area | Role and browser action | Expected API/database evidence |
| --- | --- | --- |
| Early Governance | Responsible DS creates/saves seven findings, attaches allowed evidence, finalizes, then runs saved screening on a dedicated authorized record. | Owner-scoped API success; immutable investigation/action/attachment rows, correct hashes and finalization event. |
| Regulatory | Same DS opens saved evaluation, records required clarification or human review, or No Follow-up Required where allowed. | Rule/version bound follow-up event; original evaluation unchanged; stale revision rejected. |
| Conflict | DS opens the existing screen and a selected saved result. | Existing saved result only; no clearance/completion write. |
| Combined Findings | DS opens the selected Early/Regulatory chain and optional saved Conflict. | Exact selected IDs/provenance; missing Conflict remains Not assessed; disabled handoff has no request. |
| Anomaly | Authorized DS and Commissioner open saved history; run inference only against a dedicated test fixture if separately approved. | Frozen v1 hash remains; saved flagged state governs presentation; no score interpreted as accuracy. |
| Applicant complaint | Applicant submits only an authorized dedicated test complaint with optional parcel/free-text address if separately approved. | Owner-only tracking/classification/public history; another applicant denied. |
| Complaint handling | Explicitly granted Commissioner and assigned DS inspect current history; perform no ordinary business action merely for testing. | Central/DS scope, rechecks and immutable history; former/unassigned DS denied. |
| Evidence verification | Responsible DS opens owned saved early assessment and evidence history; Commissioner reads without write controls. | Creator scope enforced; attachment download headers; other DS/applicant denied. |
| Authentication | Applicant, DS and Commissioner sign in/out and navigate C4 routes. | Admission cookie is HttpOnly/SameSite strict/path scoped; applicant C4 routes redirect; API denials do not leak protected data. |

Use `PGOPTIONS=-c default_transaction_read_only=on` for all verification queries. Save request IDs, API status/results, role, UTC time and non-sensitive row/count/hash comparisons as evidence. Do not create a fictional ordinary complaint, run inference, or issue a decision unless separately authorized.

## Blockers

1. Resolve the API unit-test contract mismatch (`403` expected vs actual uniform `404`) through a separate reviewed decision; neither the test nor the authorization behavior was changed here.
2. Provision an explicitly isolated PostgreSQL test database and supported Python ML environment to rerun the 12 environment-dependent Governance tests. They must never use the ordinary target.
3. The release-snapshot provenance limitation remains separate from local working-tree readiness; no clean reproducible source snapshot exists yet.
