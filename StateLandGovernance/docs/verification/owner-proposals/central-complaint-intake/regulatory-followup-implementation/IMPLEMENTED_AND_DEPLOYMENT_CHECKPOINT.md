# Regulatory follow-up — implemented and isolated-tested

2026-10-23 Asia/Colombo. **Implementation complete for the approved scope. Ordinary deployment NOT authorized or performed.** This supersedes the earlier statement that schema approval was pending; the original design/checkpoint remains historical evidence. No ordinary migration, grant, private runtime change, API replacement, complaint/evidence action or inference was performed. No staging, commit or push.

## Delivered behavior

Authenticated GET/POST `/api/governance-intelligence/regulatory-assessments/{id}/follow-ups` records internal clarification requests and reasoned human reviews against actual saved findings. Clarification is limited to InsufficientInformation; human review requires the saved review status/flag. Missing or duplicate rule codes fail closed. The server derives rule/version, authenticated actor, UTC and original-finding digest; client-supplied identities cannot grant authority.

Only a freshly eligible responsible DS who created that owned regulatory assessment may write. Existing exact-report read policy remains in use, with repeated authorization checks; Commissioner explicit report-read authority is read-only. Applicants fail the controller role gate; unrelated DS/foreign officers receive no report/history. Legacy reports without creator provenance remain read-only. Existing Development and RegulatoryProvenance Enabled/WritesEnabled gates are reused; no new flags, account or role.

Three bounded review outcomes: NoAdditionalConcern, AdditionalInformationRequired, FurtherInstitutionalLegalReview. Neither notes nor outcomes establish legal approval or overwrite a finding. Clarification records have status Recorded; no applicant delivery, notification, upload or C3 routing is claimed. References are bounded descriptive text and are never fetched or promoted to verified evidence. Reassessment remains the existing separate deliberate structured submission path.

The frontend adds Request Clarification, Record Human Review and saved history to the current result and saved report. Controls derive from server CanWrite plus saved findings. History shows rule/version, action/outcome, remarks/references, UTC/revision and progressive actor/event provenance. Session changes clear displayed data. Uncertain writes retain the exact account/assessment-scoped request in sessionStorage and require deliberate replay; no automatic writes. Definitive400/409 rejection clears the rejected local request and reloads history before another deliberate submission. GET/refresh never reruns an engine. The running ordinary API lacks these new endpoints, so its frontend cannot save until approved deployment; it displays unavailability rather than pretending persistence.

## Exact additive SQL and grants

Migration **20261023121903_AddRegulatoryFollowupEvents** adds exactly one empty18-column table, one primary key/one alternate key, two restrictive foreign keys, seven CHECKs and five explicit indexes (including EF's supporting composite self-FK index). No existing column or historical row/backfill changes. The model snapshot adds only the new entity. Down throws to retain history.

- [REVIEWED_FORWARD.sql](REVIEWED_FORWARD.sql): generated EF forward script from20261023093022_AddScreeningEvidenceVerification through only the new migration.
- [REVIEWED_ADDITIONAL_PERMISSIONS.sql](REVIEWED_ADDITIONAL_PERMISSIONS.sql): only SELECT,INSERT on `governance_intelligence.regulatory_followup_events` to existing `slg_c4_regulatory_runtime`.
- [CHANGED_FILES_SHA256.json](CHANGED_FILES_SHA256.json): exact implementation/test/wiring/snapshot delta paths and before/after hashes. Includes generated migration/designer; pre-existing dirty repository work is not this task's delta.

No new UPDATE/DELETE/DDL/schema/sequence/role/account/peer/outbox rights. The isolated test role `followup_runtime_20261023` has only existing-shaped report/provenance SELECT, schema USAGE, plus the exact new-table SELECT/INSERT. Test role creation/setup is NOT ordinary deployment SQL.

Unique actor/submission key enforces replay, evaluation/rule/revision uniqueness plus a restrictive same-stream predecessor FK enforce append-only chains. Two narrowly scoped parameterized transaction locks serialize same-key requests across streams, then the local rule chain. No model/network processing runs under the lock. Expected revision rejects stale writes; historical replay returns the saved event and current revision separately. EF interceptor stamps actor/UTC normalized to PostgreSQL microseconds and canonical typed JSON/SHA256 bound to the immutable original finding. Reads recompute that binding. EF sync/async update/delete guards and database runtime rights protect history.

## Backup, restore and preservation

Reused the explicitly verified secure archive without overwriting it:

`D:\AI-Driven-Digital-Lease-Governance-for-State-Land-Management\AI-Driven-Digital-Lease-Governance-for-State-Land-Management\.tmp\c4-handover-backup-20261023\state_land_governance_c4.dump`

360140bytes; SHA256 `d904779c03f0d40c5b96a23005ff66840405b744786a6b96395a8f706001e430`. Readability/checksum/archive listing passed. Restored to NEW Docker PostgreSQL database `c4_regulatory_followup_restore_20261023`, loopback57657; all40original tables/schema/20migration history match the archive. Original ownership/ACL execution skipped only in isolation; archive definitions retained. New migration/grants were applied ONLY there. The isolated database has41tables/21migrations; new table was empty before fictional fixtures.

All archived original rows remain unchanged in the isolated copy and present unchanged in ordinary storage. Ordinary remains **localhost5432/state_land_governance_c4, OID34144,40tables/20migrations**, with schema/grants unchanged. C1/C3/accounts fingerprints and frozen model hashes match. No C1/C2/C3 implementation change.

**Concurrent ordinary addition:** regulatory evaluation78→79, provenance20→21, audit233→234, outbox110→111. These records are preserved; their origin was not attributed to an actor/process by this task. They were already outside the archived snapshot when the new restore was prepared. Therefore a fresh ordinary backup and new isolated restore are mandatory before future deployment. No ordinary record was removed to match an old baseline. See [FINAL_PRESERVATION.json](FINAL_PRESERVATION.json) and [BACKUP_AND_NEW_RESTORE.json](BACKUP_AND_NEW_RESTORE.json).

## Actual verification

| Gate | Result |
|---|---|
| Focused backend regression |319passed,0failed/skipped; seven new follow-up tests |
| Frontend |158passed,0failed/skipped; two new follow-up tests |
| Architecture |7passed |
| Actual HTTP/repository/restricted PostgreSQL proof |34checks passed |
| EF snapshot |No pending model changes |
| Release publish |Succeeded to ignored `.tmp/regulatory-followup-review-api`; package hashes recorded |
| Focused new-file lint |0errors/warnings |
| TypeScript |Only3known unrelated C1errors; no whole-production-build green claim |

HTTP proof covers both operations/all three review outcomes, creator/Commissioner/applicant/anonymous denials, bounded input, compliant-finding rejection, same-key and concurrent same-key replay, changed-content409, competing revisions, historical replay/current revision, fresh DS and Commissioner revocation, host recreation, unchanged original findings, typed canonical hashes, EF immutability and actual database-denied UPDATE/DELETE/CREATE. See [ISOLATED_HTTP_EVIDENCE.json](ISOLATED_HTTP_EVIDENCE.json).

Final isolated evaluation/event IDs are in that evidence; they are fictional isolated fixtures and must never be used as ordinary report URLs. Two earlier proof fixtures are retained from test iterations. No inference was needed or run. Initial helper errors mistook ON DELETE RESTRICT for a DELETE statement and PostgreSQL t/f for JSON booleans; corrected before isolated migration. Initial proof's JSONB property-order comparison was corrected to typed finding equality; application results were not changed to make the test pass.

Explicit isolated transaction rollback left event count unchanged. Added new-table test-role rights were then revoked; actual SELECT was denied, all saved history retained. The isolated copy remains in that recovery state, and the Docker container was stopped with storage retained. Down-refusal test passes. See [ISOLATED_RECOVERY_EVIDENCE.json](ISOLATED_RECOVERY_EVIDENCE.json).

No graphical browser automation was performed. Frontend tests render initial states/check controls and request logic; they do not establish an authenticated visual click-through. Manual visual/focus/mobile checks remain after deployment. NuGet vulnerability-feed warnings persist. The published full host was not launched against ordinary storage; real controllers/repositories ran in the isolated host. Ordinary service HTTP health is recorded separately; no healthy service was restarted.

## Test commands

From Web: `npm.cmd test`; focused `npm.cmd run lint -- src/modules/component-04/components/RegulatoryFollowups.tsx src/modules/component-04/services/regulatoryFollowups.ts tests/component-04-followups.test.ts`; `npx.cmd tsc --noEmit`.

Backend: `dotnet test StateLandGovernance/tests/UnitTests/GovernanceIntelligence/StateLandGovernance.UnitTests.GovernanceIntelligence.csproj --no-restore --filter 'FullyQualifiedName~RegulatoryFollowup|FullyQualifiedName~GovernanceAccess|FullyQualifiedName~ManualEvidence|FullyQualifiedName~Complaint|FullyQualifiedName~CommissionerReportRead|FullyQualifiedName~OfficerReportController|FullyQualifiedName~IndependentRegulatoryReport|FullyQualifiedName~DependencyInjection|FullyQualifiedName~OfficerFlow|FullyQualifiedName~Kiwula|FullyQualifiedName~PostWorkflow'`, using COMPONENT4_TEST_POSTGRES_CONNECTION only for the established isolated regression database on57657.

Architecture: `dotnet test StateLandGovernance/tests/ArchitectureTests/StateLandGovernance.ArchitectureTests.csproj --no-restore`.

HTTP runner: `dotnet run --project StateLandGovernance/tests/IntegrationTests/RegulatoryFollowupProof/RegulatoryFollowupProof.csproj --no-build`, with explicit C4_FOLLOWUP_TEST_CONNECTION. It refuses any target except127.0.0.1:57657/c4_regulatory_followup_restore_20261023. The test-role rights are currently revoked after recovery rehearsal; an explicitly isolated setup must restore them before another run. Never point this fixture runner at ordinary storage.

## Future ordinary deployment — separate approval required

1. Review source/package/SQL/grant hashes and current runtime/private configuration. Create a NEW secure full read-only backup including the additional ordinary regulatory submission; verify readability/SHA256/listing and restore to a NEW isolated database. Compare all current rows/schema/history/grants/sequence state. Do not reuse this older snapshot as current protection.
2. Repeat migration/grant proof on that current restore; preserve all current data. Review a versioned successor launcher bound to `.tmp/regulatory-followup-review-api` and PACKAGE_SHA256.json while preserving existing private candidate, exact database/runtime target, independent Commissioner grants, read-only peers/accounts and disabled workers/sandboxes. Existing launcher remains bound to the OLD active package; do not silently repoint it or start this unapproved package.
3. After explicit deployment approval, identify and stop exactly the verified API before changes. With separate operator credentials, connect ONLY localhost5432/state_land_governance_c4; verifyOID34144/history20 and apply only REVIEWED_FORWARD.sql then REVIEWED_ADDITIONAL_PERMISSIONS.sql. Never replay predecessors or use startup migrations. Verify41tables/21migrations, empty new table, exact two privilege additions and all original records/rights unchanged.
4. Start exactly one compatible reviewed successor API, reusing healthy ML/frontend. Verify authentication, existing report/complaint/evidence access, applicant denial and responsible-DS/Commissioner read-only follow-up capability. Do not create ordinary fictional follow-ups or rerun engines for deployment testing. No new feature settings are required.
5. Record actual deployment/process/environment/hash/preservation evidence, then separately carry out genuine business follow-up only when authorized. This implementation approval is not authority to submit ordinary review notes.

Recovery: stop updated API; revoke ONLY SELECT/INSERT on the new table before considering a compatible earlier package. Keep table/migration/events, never destructive Down/DELETE/restore over ordinary. The currently active security/handling/evidence-compatible package can serve prior features without follow-up endpoints, but any rollback package must be explicitly verified against current complaint policy and renamed target. Preserve all independent permissions. An uncertain write must be reconciled by its saved key; no automatic retry/new key.

## Manual walkthrough after approved deployment

1. Sign in as responsible DS, open its saved regulatory report. Expand an InsufficientInformation finding; follow-up form permits Request Clarification, missing information, required request text and optional references. Saving records an internal event only. Inspect original outcome before/after and new rule revision/event/time.
2. Select a saved human-review finding; Record Human Review using one of the three bounded outcomes and required remarks. Inspect append-only history, rule version and original finding. A new review does not mark it Compliant or approve land.
3. Refresh/reopen: saved events persist without screening. On uncertain connection, read history and replay the same retained request deliberately. On a confirmed stale revision, inspect refreshed history before a new deliberate action.
4. Commissioner can read history but has no write form. OtherDS/applicant cannot access it. Verify account switching and no stale private content. Existing reassessment remains a separate deliberate path with actual updated facts/version and a new result identity.
5. Check keyboard/focus, small screens, loading, error and provenance details. Do not create ordinary fictional records merely to observe UI; use an isolated browser deployment for test writes.

[VERIFY_FOLLOWUPS_READ_ONLY.sql](VERIFY_FOLLOWUPS_READ_ONLY.sql) uses actual table/column names and omits private remarks/references/actor IDs. It is executable in the isolated copy now, and ordinary only after approved deployment.

**Remaining deployment blockers:** separate explicit ordinary approval, fresh backup/restore for current additions, reviewed successor launcher/package cutover. No additional schema/grant/engine scope is requested. Implementation is ready for that controlled deployment review; ordinary activation is not claimed.
