# Central complaint activation preparation — GO / NO-GO

2026-10-23, Asia/Colombo. **Preparation GO. Ordinary activation NOT performed and still requires separate explicit approval.** This report supplements the preserved [combined checkpoint](COMBINED_ACTIVATION_CHECKPOINT.md); its earlier Docker-outage evidence remains historical.

The six blocked PostgreSQL regressions now pass. The full focused combined backend run also passes **241/241**, zero skipped. Docker Desktop29.6.2 was restored, then the existing isolated PostgreSQL18 container was restarted. It was stopped successfully after preparation, retaining all isolated storage. No ordinary service was started, stopped or replaced. No listeners were observed on5264/3000/8104 during preparation; their later availability must be checked at activation.

## Verified archive and isolated restore

New secure archive, without overwriting any previous backup:

```text
D:\AI-Driven-Digital-Lease-Governance-for-State-Land-Management\AI-Driven-Digital-Lease-Governance-for-State-Land-Management\.tmp\central-activation-preparation-20261023_053654\c4_handover_local.dump
```

- Size315322bytes; last write2026-10-23T05:36:55.606672Z.
- SHA256 `ab6780784f2cf36b405b33c81d9e1a81da5e215f7cbcdc0418b61f988da433d4`, rechecked after all tests.
- New directory inheritance disabled; only the preparation account and SYSTEM have access. No unrelated/system-wide ACL was changed. The archive was read and listed successfully by the actual preparation account.
- Full database dump used the existing local operator credential, forced transaction-read-only, targeting ONLY localhost5432/c4_handover_local. No peer database connection was opened for the dump. No password was changed or published.
- Restored into NEW Docker database `c4_central_restore_preparation_20261023_053654`,127.0.0.1:57657. No existing database was overwritten or deleted. Ownership/ACL restoration was skipped; the archive retains original ACL definitions, and inspected runtime rights were reconstructed explicitly only in isolation.
- All **36 original user tables across governance_intelligence and component4_synthetic_demo** match, including migration history, complaints, classifications, assessments, research data, audit/outbox/receipts. Ordinary migration history remains17; both new ordinary tables remain absent. All original row-hash multisets still match after isolated handling tests.

Evidence: [PREPARATION_FRESH_BACKUP_AND_RESTORE.json](PREPARATION_FRESH_BACKUP_AND_RESTORE.json), [PREPARATION_PRESERVATION_AND_ROLLBACK.json](PREPARATION_PRESERVATION_AND_ROLLBACK.json).

Initial comparison differences were representational: ordinary Asia/Colombo versus isolated UTC; different default collation ordering; and PostgreSQL's equivalent literal varchar-array-to-text cast deparsing in three existing CHECKs and one index predicate. Explicit UTC, canonical row JSON, C ordering and narrowly normalized equivalent casts produced matching data/schema without changing stored values or constraints. The original synthetic payload rows also compared equal after decoding. This is not a skipped mismatch or a changed database baseline to hide loss.

The obsolete temporary operator-configuration archive was inaccessible; its ACL was left untouched. A restricted-role full dump then correctly failed on the pre-existing synthetic schema. That incomplete attempt is retained in its separate new directory and is NOT the approved backup. The successful complete archive above used the existing local operator credential read-only.

## Isolated migration, permission and application results

Only [COMBINED_REVIEWED_FORWARD.sql](COMBINED_REVIEWED_FORWARD.sql) was applied to the restored copy: `20261022200907_AddCentralComplaintAssignments`, followed by `20261022205550_AddComplaintHandlingEvents`. History became19 only in isolation; new tables were initially empty with no backfill. Both reviewed SQL files are unchanged from the combined checkpoint.

Only the approved additional SELECT/INSERT grants on the two new tables were applied to existing-shaped `slg_c4_regulatory_runtime` in isolation, after reconstructing its inspected old table/column rights. No ordinary role/grant changed. [PREPARATION_ISOLATED_MIGRATION_AND_GRANTS.json](PREPARATION_ISOLATED_MIGRATION_AND_GRANTS.json) contains the exact four new privilege entries and original-row preservation results.

The actual controllers/repositories ran both paths against that restored copy under the restricted role: direct handling → investigation → findings → resolution → closure → reopening → explicit DS assignment → DS investigation/findings → central resolution. All **11 restored-copy HTTP checks passed**, including anonymous/DS-central/foreign-applicant denial, terminal DS revocation, historical replay retaining current terminal state, public-only owner history and canonical hashes. Only a newly seeded isolated fictional fixture was acted upon. No restored original complaint received an assignment, investigation, decision or review. No original record was modified. **Zero classifier/anomaly calls** were made during this preparation.

Isolated-only fixture `6ae57def-a07f-4ec2-a647-94e6a284e307`; assignment event `8d0b8733-f348-4bcf-8da2-7d94d2f954c8`. These are not ordinary report IDs. See [PREPARATION_RESTORED_HTTP_EVIDENCE.json](PREPARATION_RESTORED_HTTP_EVIDENCE.json). The restored test harness initially omitted required TakeDirect notes and used a JSONB LIKE lookup; both fixture errors were corrected. Application code, engines, migrations and grants were not changed. Previous 91 HTTP/concurrency/restart checks remain their recorded evidence; they were not falsely counted as a fresh91-check rerun.

## Exact identity, grant and package

Fresh read-only account metadata confirms exact actor `0ab669fe-d0ab-5695-ad55-10f6b3b5aab3`, Officer/COMMISSIONER with matching profile ActorId. No role/password/profile change or ordinary login was attempted. [PREPARATION_CENTRAL_IDENTITY_READ_ONLY.json](PREPARATION_CENTRAL_IDENTITY_READ_ONLY.json).

The exact [proposed configuration](COMBINED_PROPOSED_ACTIVATION_SETTINGS.json) was bound through the actual configuration binder and authorization policy. Grant `3970d3d2-5fca-4858-a9ae-e98ed04f9809`, Read/Assign/Review and all nine explicitly reviewed ReviewOperations passed within its window; before-window and expiry checks denied access. [PREPARATION_EXACT_GRANT_POLICY_EVIDENCE.json](PREPARATION_EXACT_GRANT_POLICY_EVIDENCE.json).

**Window:24Oct2026 09:00 to25Oct2026 09:00 Sri Lanka**, UTC2026-10-24T03:30:00Z through2026-10-25T03:30:00Z. The grant is unissued and not valid on23Oct. An immediate activation outside that window requires separately reviewed exact replacement settings; do not silently extend it.

Release API publish succeeded to `.tmp/central-preparation-published-api`;88files are hashed in [PREPARATION_PACKAGE_SHA256.json](PREPARATION_PACKAGE_SHA256.json). Published application source matches the reviewed implementation. Only the isolated proof runner was extended; the new launcher is preparation tooling. [PREPARATION_SOURCE_SHA256.json](PREPARATION_SOURCE_SHA256.json) records the exact current source set; prior manifests are preserved. Frozen complaint/anomaly hashes and protected auth/reference/module files match [PREPARATION_PROTECTED_ARTIFACT_EVIDENCE.json](PREPARATION_PROTECTED_ARTIFACT_EVIDENCE.json).

A new candidate environment copy exists only in the secure backup directory as `candidate-central-environment.NOT_ENABLED.local.json`. It preserves every existing private-environment key and adds only the reviewed CentralComplaints delta. It has NEVER been loaded into the ordinary process or substituted for existing private files. The original private environment's hash remains unchanged. [PREPARATION_PRIVATE_CONFIGURATION_BASELINE.json](PREPARATION_PRIVATE_CONFIGURATION_BASELINE.json) records paths/delta without credentials.

New reviewed launcher [Start-CentralComplaintsApprovedApi.ps1](../../../../tools/development/Start-CentralComplaintsApprovedApi.ps1) passed **ValidateOnly**: all package hashes, exact candidate delta, target/runtime identity, read-only C1/C3/accounts protections and disabled worker/sandboxes were verified. No process environment was loaded or API started. The launcher also enforces the timed window and duplicate5264 refusal when actual activation is separately authorized. Windows script policy required a process-scoped `-ExecutionPolicy Bypass`; no persistent policy was changed.

Compatibility evidence is Release publish, actual controller/repository restored-copy execution and focused regressions. The published ordinary host itself was NOT launched during preparation. Full host startup, existing authenticated report retrieval and browser rendering remain activation-time gates, not fabricated successes.

## Rollback rehearsal

After the isolated HTTP host stopped, only SELECT/INSERT on assignment/handling tables were revoked in the restored copy. Actual restricted SELECT was denied; all pre-existing table grants matched the inspected baseline; all10new handling events and all original rows remained. Both new migrations/tables remain in that isolated copy, now with added runtime rights revoked. It is intentionally retained in this tested recovery state.

Safe recovery is **stop the new API, disable central settings in its candidate/private environment, revoke ONLY the two new table rights, retain history and keep the API stopped until a compatible recovery package is reviewed**. This has downtime. No old binary was started or claimed authorization-compatible. An old case/assignment-only binary could expose complaint scope despite terminal handling; do not restart it. Never destructive Down/delete/backfill/restore over ordinary C4. A recovery needing continued applicant/officer availability requires a separately reviewed compatible read-only package.

## GO / NO-GO decision

| Gate | Status |
|---|---|
| Docker restored; six blocked tests | GO:6/6 passed |
| Focused combined regression | GO:241/241,0skipped |
| Fresh complete archive/readability/listing/SHA256 | GO |
| New isolated restore/all36tables/schema/history | GO |
| Both exact migrations/restricted grants/preservation | GO in isolated copy only |
| Both handling paths/exact grant policy/package/launcher | GO for reviewed preparation scope |
| Tested stopped-API grant-revocation recovery | GO, with explicit downtime |
| Ordinary activation now | **NO-GO: no separate activation approval; grant not yet valid** |
| Ordinary service/browser proof | Pending approved startup;5264/3000/8104 currently absent |

Earlier139frontend and5architecture passes are recorded, not rerun here. Three unrelated C1 TypeScript errors, existing NuGet feed warnings and manual browser visual/keyboard/responsive limitations remain. No model/rule/engine/C1/C2/C3/account/password/ordinary result/history/worker/blockchain/Groq or Git publication change occurred.

## Exact ordinary actions AFTER separate approval — NOT EXECUTED

1. Confirm approval covers both migrations, exact grant delta, exact time-limited identity/settings and the tested stopped-API recovery. Execute within the reviewed window. Recheck [PREPARATION_REVIEW_SHA256.json](PREPARATION_REVIEW_SHA256.json), [PREPARATION_SOURCE_SHA256.json](PREPARATION_SOURCE_SHA256.json), package hashes, backup checksum, current ordinary schema/history/grants/all36original table fingerprints and private settings. Any new ordinary records require an updated verified backup/baseline; preserve them. Confirm/read current API PID/path if any and stop only that verified process before changes. Reuse healthy services; do not duplicate listeners. No predecessor migration replay.
2. With separate operator credentials entered only at the psql password prompt, apply exactly these files to the named ordinary database. These commands are future activation instructions, not authorization to run now:

```powershell
Set-Location 'D:\AI-Driven-Digital-Lease-Governance-for-State-Land-Management\AI-Driven-Digital-Lease-Governance-for-State-Land-Management'
$psql = 'C:\Program Files\PostgreSQL\18\bin\psql.exe'
$docs = '.\StateLandGovernance\docs\verification\owner-proposals\central-complaint-intake'
& $psql -X -W -v ON_ERROR_STOP=1 -h localhost -p 5432 -U postgres -d c4_handover_local -c 'SELECT current_database(),inet_server_port(); SELECT "MigrationId" FROM governance_intelligence.__ef_migrations_history ORDER BY "MigrationId";'
# Independently confirm exact target/history/baseline before the following writes.
& $psql -X -W -v ON_ERROR_STOP=1 -h localhost -p 5432 -U postgres -d c4_handover_local -f "$docs\COMBINED_REVIEWED_FORWARD.sql"
if ($LASTEXITCODE -ne 0) { throw 'Stop: inspect actual migration state; do not retry blindly.' }
& $psql -X -W -v ON_ERROR_STOP=1 -h localhost -p 5432 -U postgres -d c4_handover_local -f "$docs\COMBINED_REVIEWED_ADDITIONAL_PERMISSIONS.sql"
if ($LASTEXITCODE -ne 0) { throw 'Stop: keep API off and reconcile exact grants.' }
```

3. Verify19migrations, both empty new tables, exact SELECT/INSERT-only additions, no ordinary historical-row change and unchanged prior column grants. Never use automatic startup migrations or broaden rights to fix failures. Candidate settings are already isolated in a new file; do not overwrite the old private file. Validate, then start only after all SQL/config gates and approval pass:

```powershell
$candidate = '.\.tmp\central-activation-preparation-20261023_053654\candidate-central-environment.NOT_ENABLED.local.json'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\StateLandGovernance\tools\development\Start-CentralComplaintsApprovedApi.ps1 -CandidateEnvironmentPath $candidate -ValidateOnly
# ONLY after explicit activation approval, successful immediate preflight/SQL checks,
# and within the approved grant window:
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\StateLandGovernance\tools\development\Start-CentralComplaintsApprovedApi.ps1 -CandidateEnvironmentPath $candidate -ApprovedActivation
```

4. Check one API listener/health and exact effective environment, no peer/account writes, sandboxes/worker false. Existing sessions require sign-in again. Restore ML/frontend only as approved and only if their ports are free; use existing launchers, frozen bundle verification and frontend settings, never duplicate services. Verify existing regulatory/complaint/research report retrieval and anonymous/foreign denials before any newly authorized handling action. Startup/HTTP failures require STOP/recovery, not unreviewed grants or old-binary fallback.
5. No ordinary complaint action is authorized by this preparation. After separate authorization for specific real operations, use `/component-04/central-complaints`, `/component-04/complaint-inbox` and applicant `/workflow-governance/applicant/complaints`. Check four queues, current versus historical responsibility, genuine classification, public-only applicant timeline, explicit revisioned direct/DS actions, terminal confirmations, revoked DS access, replay/refresh/sign-out isolation and responsive/keyboard behavior. Record actual ordinary IDs only after actual authorized actions; the isolated IDs above must not be substituted.

**STOPPED before ordinary changes. Separate explicit activation approval is required.**
