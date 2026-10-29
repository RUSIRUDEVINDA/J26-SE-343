# C4 database rename completed; report-read deployment stopped

2026-10-23, Asia/Colombo. **PARTIAL COMPLETION / NO-GO. The in-place rename succeeded. The new API was stopped after a mandatory authenticated report-read check failed. Port5264 is offline. C4 is not ready to freeze as successfully completed.**

The attached user approval authorized the exact rename and reviewed Commissioner report-read package. Its mandatory instruction was “If any mandatory gate fails: STOP.” No controller workaround, automatic rename-back, incompatible older API, migration, PostgreSQL grant expansion or ordinary fixture action was attempted.

## Actual rename and preservation

`localhost:5432/c4_handover_local` became `localhost:5432/state_land_governance_c4` through exactly:

```sql
ALTER DATABASE "c4_handover_local" RENAME TO "state_land_governance_c4";
```

Connected as the existing operator to maintenance database `postgres`; zero C4 sessions existed before rename, and no sessions were terminated. Same OID **34144**, same owner **postgres**, database ACL/encoding/collation/settings, schema, all **38 user tables**, sequence state, all **19 migrations**, schema/table/column grants and every captured row fingerprint were verified before API start and again after the stopped deployment. No migration or GRANT ran. C1/C3 `state_land_governance` and `portal_accounts_local` fingerprints match the preflight; no peer/account writes occurred. Existing complaints/classifications, assignment/handling history, research/assessment/audit/outbox/receipt records are preserved. The old name is now absent; the new database is the original object.

## Fresh verified backup

Absolute archive:
`D:\AI-Driven-Digital-Lease-Governance-for-State-Land-Management\AI-Driven-Digital-Lease-Governance-for-State-Land-Management\.tmp\c4-rename-backup-20261023_0840\c4_handover_local.dump`

- Bytes **334801**; last write UTC **2026-10-23T08:37:07.728148+00:00**.
- SHA256 **`e702a55f70fe1e22059b65f9672cb98dea2a380b89e9aba10c33de54400d530f`**, rechecked after stopping.
- Full read-only pg_dump; readable by the activation account; archive listing valid.
- New directory ACL restricted to the current Windows account and SYSTEM; all older archives untouched.
- Restored to NEW isolated Docker PostgreSQL database **c4_rename_restore_20261023_0840**, loopback57657. All38tables, normalized schema, migration history and sequence state match. Restore skipped cluster-specific ownership/ACL execution; original definitions remain in the archive and exact ordinary ACL metadata was compared across the rename. No isolated-role setup was applied to ordinary storage.
- Isolated container stopped successfully after verification; storage retained. No new model calls or ordinary test fixtures.

## Configuration, launcher and package

New secure private candidate `.tmp/c4-rename-backup-20261023_0840/renamed-target-environment.local.json` changes exactly the C4 connection Database field plus the two approved CommissionerReportReads keys. All other candidate values are preserved, including read-only peers/accounts, disabled sandboxes/worker, JWT settings and the independent persistent complaint source. The historical private candidate/baselines remain unchanged. Root `.env` changes only the C4 Database field, with its original bytes retained securely.

Separate report permission file is the exact approved `.tmp/commissioner-report-read-review/report-read-permissions.local.json`: actor `0ab669fe-d0ab-5695-ad55-10f6b3b5aab3`, grant `77092597-0570-4e16-a71e-ea8d264cb462`, only C4AssessmentReadAll, persistent/unrevoked, empty ReviewOperations, no expiry. Complaint permission actor/grant/operations and exact document bytes are unchanged. Both files exist on disk; **there is currently no API process loading them**.

New launcher `StateLandGovernance/tools/development/Start-C4RenamedTargetApprovedApi.ps1` retains exact target/runtime guards and adds the separate report permission validation. ValidateOnly passed. Package `.tmp/central-report-read-review-api` matches all88reviewed file hashes. API DLL SHA256 **`9de98eb677f90eba47646d1236f0d27f98ff91d3ee256a027a25597f8c19e9fc`**. Candidate SHA256 **`406bc96165e33c6c8d778322e2470d7abc3978da605b6791af72bb5aaaededd0`**. Source/package/config/launcher manifests are new versions; historical manifests were not rewritten.

Verified old API27588 was stopped; exactly one new API37768 started and returned Healthy on5264. Frontend16692/3000 and ML36612/8104 were reused. New API37768 was subsequently stopped after the blocker; no API listener remains. Automatic startup migration source checks and compiled-startup architecture tests pass; startup log contained normal host/SELECT activity, no migration execution. Final unchanged data/schema/peer fingerprints support that observation. No setup command was invoked.

## Mandatory runtime blocker

Existing `commissioner` login succeeded with unchanged credentials. Central complaint capabilities, all four queues, preserved complaint/classification retrieval, applicant public-only history and unassigned DS denial checks passed. Saved regulatory/Kiwula conflict/early/original workflow report GETs and corresponding owner/foreign denials passed before reaching the officer-request family. This is a partial run, **not a complete runtime-suite success**.

Authenticated Commissioner GET:

`/api/governance-intelligence/officer-assessments/29f53e56-9e9a-4e04-9ed3-350d1a7518d3`

returned **403**. `OfficerFlowsController.Execute` requires `IsDs()` for all actions, including saved report/history GETs. The reviewed repository policy accepts C4AssessmentReadAll, but this HTTP controller rejects Commissioner first. Earlier isolated repository tests did not exercise this controller boundary. This is a genuine package defect, not missing data, a bad password, or permission to broaden DS/write authority.

No ordinary complaint submit/assignment/review/decision or classifier/anomaly call occurred. Controlled live grant revocation and remaining frontend HTTP/refresh checks were **not reached**. Both grant files remain exact approved bytes. No ordinary authenticated browser rendering was performed.

## Regression evidence and helper corrections

Final focused backend **275/275**, frontend **143/143**, architecture **7/7**, zero skipped. The first backend invocation used the wrong test environment variable, causing six missing-configuration failures; the corrected isolated-only COMPONENT4_TEST_POSTGRES_CONNECTION run passed all275. Original log is retained. No application change was made to fix the test harness. Previously documented three C1 TypeScript errors remain historical limitations; TypeScript/full production build was not rerun or claimed green here.

Two preflight helper issues were diagnosed without mutation: PostgreSQL OID JSON is a string (`"34144"`), and terminal-only COLORTERM/LESS were absent from the existing process. Numeric identity normalization and excluding only those irrelevant terminal variables allowed the complete application-state check to pass; no database/model/application configuration mismatch was bypassed. The early diagnostic file STOPPED_DATABASE_IDENTITY.json contains the initial incorrect mismatch label; it is explicitly superseded by IMMEDIATE_PREFLIGHT.json and this explanation, and retained as incident evidence.

## Recovery and continuation boundary

Current safe state is **API stopped, original database preserved under the new name**. Frontend and ML remain running. No automatic rollback or permission removal was needed: this deployment introduced no SQL grant/migration delta. Do not use old database-name launchers or restart an older incompatible package. Do not restore over ordinary storage, replay migrations, delete events or rename back blindly.

Continuation needs an explicitly reviewed narrow controller correction: allow only saved-report GET/history access via the existing explicit read policy, retaining DS-only creation/rerun/draft guards, live eligibility and post-read checks. Add authenticated controller/HTTP coverage; republish/review the successor hashes. Then repeat current baseline/backup/deployment checks before a single compatible restart. No correction has been implemented under this stopped attempt. If records have changed, verify a new backup/isolated restore. Rename-back would require its own current safety checks and reviewed decision.

Read-only operator checks now (do not start the known-blocked package):

```powershell
Get-NetTCPConnection -State Listen -LocalPort 5264,3000,8104 -ErrorAction SilentlyContinue |
    Select-Object LocalPort,OwningProcess
Get-FileHash -Algorithm SHA256 -LiteralPath '.tmp/c4-rename-backup-20261023_0840/c4_handover_local.dump'
$env:PGOPTIONS = '-c default_transaction_read_only=on'
try {
  & 'C:\Program Files\PostgreSQL\18\bin\psql.exe' -X -W -v ON_ERROR_STOP=1 `
    -h localhost -p 5432 -U slg_c4_regulatory_runtime -d state_land_governance_c4 `
    -c 'SELECT current_database(), current_user; SELECT oid FROM pg_database WHERE datname=current_database(); SELECT count(*) FROM governance_intelligence.__ef_migrations_history;'
} finally { Remove-Item Env:PGOPTIONS -ErrorAction SilentlyContinue }
```

After an approved corrected deployment only, sign in again at http://localhost:3000/sign-in with existing Commissioner credentials; inspect Proposals/View Proposal Data/Regulatory Screening, conflict/early/workflow/anomaly navigation, History and cross-creator saved reports. Confirm central queues, applicant public history, DS restrictions and independent grant revocation. Refresh saved reports without Run/Retry. Keyboard/focus/narrow-layout and nine-feature visual checks remain manual. Do not submit ordinary complaint actions as test fixtures. With the API currently offline, authenticated browser flow is unavailable.

Evidence: IMMEDIATE_PREFLIGHT.json; FRESH_BACKUP_AND_RESTORE.json; CUTOVER_READY.json; RENAME_AND_PRESERVATION.json; MANDATORY_RUNTIME_GATE_STOP.json; FINAL_STOPPED_PRESERVATION.json; CONFIGURATION_REVIEW.json; DEPLOYED_PACKAGE_SHA256.json; SOURCE_AND_LAUNCHER_SHA256.json. No commit/push, C1/C2/C3 source change, model/rule/feature/threshold change, worker/blockchain/Groq action occurred.
