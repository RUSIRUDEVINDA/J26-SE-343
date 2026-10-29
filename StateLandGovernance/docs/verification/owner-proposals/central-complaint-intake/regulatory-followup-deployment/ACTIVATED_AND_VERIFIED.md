# C4 regulatory follow-up — ordinary deployment verified

2026-10-23 Asia/Colombo. **ACTIVATED. All mandatory ordinary deployment and read-only verification gates passed.** Graphical browser observations remain manual. No ordinary fixture, follow-up decision, regulatory reassessment or ML inference was submitted. No staging/commit/push.

## Database and exact changes

Target remains **localhost:5432/state_land_governance_c4**, same PostgreSQL **OID34144**, owner/settings unchanged. It now has **41 user tables and21EF migrations**.

Applied only **20261023121903_AddRegulatoryFollowupEvents**, from unchanged reviewed forward SQL, while the verified API was stopped. The only added runtime permissions are **SELECT and INSERT** on `governance_intelligence.regulatory_followup_events` to existing `slg_c4_regulatory_runtime`, both non-grantable. No predecessor migration, broader permission, existing-table rewrite or startup migration ran. The new table was empty after SQL and remains empty after verification.

All original row fingerprints, existing objects, prior table/column privileges, database settings and sequence state match the fresh baseline. The resulting schema matches the successfully rehearsed current-data isolated restore. C1/C3/accounts fingerprints are unchanged. Existing complaint/classification/handling/evidence/research/audit/outbox history is preserved, including the regulatory submission added since the previous archive.

[ORDINARY_SQL_AND_PRESERVATION.json](ORDINARY_SQL_AND_PRESERVATION.json) and [FINAL_ORDINARY_PRESERVATION.json](FINAL_ORDINARY_PRESERVATION.json) record the actual comparisons.

## New secure complete backup

Archive:

```text
D:\AI-Driven-Digital-Lease-Governance-for-State-Land-Management\AI-Driven-Digital-Lease-Governance-for-State-Land-Management\.tmp\c4-followup-deployment-backup-20261023\state_land_governance_c4.dump
```

**361178bytes**, SHA256 **`5f7c8362491e6195ab663bd091c5b5a296d3e34aa57e9213d02e423439040681`**. New directory restricted to the current Windows account and SYSTEM; previous archives untouched. The dump used a forced-read-only connection. Activation-account readability, archive listing and checksum passed and were rechecked at final verification.

Restored into NEW Docker PostgreSQL `c4_followup_deploy_restore_20261023`, loopback57657. All40original tables/schema/20migration history matched the current ordinary baseline before rehearsal. Ownership/ACL execution was skipped only in isolation; original ACL definitions remain in the archive and ordinary permissions were independently captured/compared.

The exact migration/grant delta was rehearsed there first. All34real controller/repository HTTP checks passed against this fresh restore under the restricted test role. Test harness differs from the approved proof only in its strict isolated database-name guard. Fictional follow-up writes occurred only in that copy. All restored original rows remain unchanged. Docker was stopped successfully after verification, retaining the restore and test history.

[BACKUP_AND_PREFLIGHT.json](BACKUP_AND_PREFLIGHT.json), [ISOLATED_MIGRATION_REVIEW.json](ISOLATED_MIGRATION_REVIEW.json), [FRESH_RESTORE_HTTP_EVIDENCE.json](FRESH_RESTORE_HTTP_EVIDENCE.json) and [PRE_CUTOVER_ISOLATED_PRESERVATION.json](PRE_CUTOVER_ISOLATED_PRESERVATION.json).

## Process, package and private configuration

Verified prior API **PID19008**, exact `.tmp/c4-navigation-access-api/StateLandGovernance.Api.exe`, was stopped only after all preflight/isolated gates passed. Exactly one successor **PID13300** now listens on5264, using `.tmp/regulatory-followup-review-api` through **Start-C4RegulatoryFollowupApprovedApi.ps1**. The executable path/hash and one-process listener check passed. API health returned200/Healthy.

API DLL SHA256 **`d02c242af42b82081914794c7067e3503ecb1d6d019e89789fc60fdbf0d60b2f`**. All reviewed package file hashes match. Full manifest and successor launcher hash are versioned in this evidence directory; old manifests/launchers/packages remain historical. The successor changes only package/manifest/log destinations from the security-compatible launcher and retains exact target/runtime identity, private-delta, separate grant sources, read-only peers/accounts, disabled sandbox/worker and duplicate-listener guards. ValidateOnly passed before cutover. No private candidate or credential changed.

The unchanged candidate is `.tmp/c4-rename-backup-20261023_0840/renamed-target-environment.local.json`. Actual successor process environment matches it. Commissioner complaint/report permission documents retain their exact approved bytes after live independent revocation checks. No account, password, session policy, actor identity or permission scope changed.

Frontend **PID16692/3000** and frozen ML service **PID36612/8104** were reused. No frontend/ML restart or duplicate listener. Startup source contains no automatic schema migration; the remaining account migration is only in the existing explicitly invoked setup command, which was never invoked. New startup/runtime logs contain no schema or data-writing SQL. Final fingerprints corroborate no runtime data write.

## Actual authenticated verification

| Check | Observed result |
|---|---|
| Existing report/complaint/navigation access |509checks passed;50saved reports retrieved |
| Applicant protected legacy API requests |69denied403 before handlers |
| Applicant direct C4 frontend routes |10redirected before governance rendering |
| New follow-up workspaces |182checks passed across21existing owned assessments |
| Responsible DS write eligibility |21workspaces returnedCanWrite=true under fresh eligibility and creator ownership |
| Commissioner follow-up access |Read200,CanWrite=false; no write submission performed |
| Applicant/otherDS/anonymous follow-up |Denied through actual authenticated HTTP |
| Existing manual DS evidence |79checks passed across11workspaces; creator write eligibility retained, Commissioner read-only, otherDS/applicant denied |
| Independent live report/complaint revocation |Denied as expected; exact original grant bytes restored |
| Existing central complaint queues/owner history |Readable with current actual lifecycle and public-only applicant privacy; no actions submitted |
| Repeat GET/refresh |Saved records retrieved without engine calls or row changes |

Ordinary checks assert eligibility/read restrictions without recording a clarification/review. Actual write correctness, all three human-review outcomes, idempotent/concurrent replay, stale revision conflict, canonical typed hashes, immutable findings, host recreation and restricted denied UPDATE/DELETE/DDL were proven in the fresh isolated copy. No ordinary test fixture was used to simulate these paths.

Authenticated frontend route200/redirect checks establish HTTP readiness, not graphical browser clicks or layout. Commissioner login/navigation remain Governance Overview `/component-04` and Complaint Inbox `/component-04/central-complaints`; DS complaint assignment scope and applicant complaint ownership remain intact.

## Tests, models and limitations

Previously completed matching implementation regressions remain **319backend,158frontend,7architecture passed**,0failed/skipped. Those broad suites were not falsely counted as rerun in this deployment. The fresh restore34-check HTTP/storage proof was rerun successfully for this deployment. Release package and current source hashes match that tested implementation; no application code was changed now. Focused implementation lint passed. Three known unrelated C1 TypeScript errors remain; no full frontend production-build green claim.

Frozen complaintv2 SHA256 `63b37eb7175e74a8dcde82bf042fcf9dcbb4f0432fbe3b4677aac037007a20ae` and anomaly SHA256 `59ed4a73d56d9f06798c4ebc4684a633afe72841c41ed0b3ba4e7bb658782b46` match. Threshold, nine features, rules and saved outputs unchanged. No inference call was requested. No C1/C2/C3 business code, Groq, blockchain or worker activation.

A local comparison-helper copy initially misdecoded a UTF-8 BOM and could not execute. It was regenerated with explicit UTF-8; the complete actual comparison then passed. No preservation/permission/schema mismatch was bypassed, no baseline changed to hide data loss, and cutover waited for the successful comparison.

Remaining observations: manual graphical/focus/mobile/account-switch checks; known unrelated C1 TypeScript failures and existing NuGet feed warnings. No mandatory deployment blocker remains. Current active package and private machine-specific paths must be transferred/reviewed securely for owner handover; a dirty workspace is not claimed to be a reproducible release commit.

## Recovery

On a mandatory failure, stop only the verified successor and fail closed. Revoke only the new table's SELECT/INSERT if rolling back that feature; retain the table, migration and all history. Never destructive Down, historical deletion, ordinary restore or incompatible pre-handling/pre-security package restart. Any previous package requires explicit compatibility review with current complaint/identity/renamed-target policies. The existing isolated recovery evidence demonstrates transaction rollback and revocation retaining history; it is not an ordinary rollback action performed in this deployment.

Manual startup/read-only verification: [MANUAL_WALKTHROUGH.md](MANUAL_WALKTHROUGH.md). Exact original implementation/SQL/grants and file diff remain in the sibling regulatory-followup-implementation checkpoint. No files were staged, committed or pushed.
