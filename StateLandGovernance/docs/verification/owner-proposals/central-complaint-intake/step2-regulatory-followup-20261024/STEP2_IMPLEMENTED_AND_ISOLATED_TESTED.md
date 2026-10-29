# Step 2 — regulatory follow-up correction, isolated only

2026-10-24, Asia/Colombo. **Complete for the approved implementation and isolated automated verification scope. No ordinary deployment. Stop before Step 3.** Graphical browser checks remain pending; whole-repository/frontend production-build success is not claimed.

## Behavior and policy

No Follow-up Required records a third append-only operation, `NoFollowupRequired`, using the existing required trimmed Remarks rationale (20–2000 characters). ReviewOutcome and MissingInformation must both be null, including rejection of an explicitly supplied empty missing-information string. Existing bounded descriptive references remain optional and confer no evidence authority.

The authoritative saved finding must have Status=Compliant, IsBlocking=false and RequiresHumanReview=false. InsufficientInformation, RequiresHumanReview/RequiresReview, Conditional, NonCompliant and unknown outcomes cannot use this action. Mandatory flags also deny it even if the status says Compliant. This deliberately fails closed on unresolved/adverse/unknown conditions. It is a follow-up permission check over immutable saved metadata, not another regulatory engine or an evaluation of free-text requirements. A previous human-review note cannot waive the saved flags; reassessment remains separate. Direct API rejection includes a clear message explaining the non-waivable conditions.

The officer records an administrative rationale only. No rule result, rule/version/source, findings, overall compliance outcome or mandatory requirement is changed. No applicant notification, screening, inference or legal approval is implied. Existing clarification and human-review behavior remains unchanged.

Frontend changes are limited to the existing selector, rationale label/explanation, inapplicable-field hiding, server validation message and saved-history operation label. Fields not applicable to the selected action are sent as null. History still shows the original screening outcome separately from the officer action, revision and rationale. No CSS/page redesign or navigation change.

## Reuse and exact changes

The existing `RegulatoryFollowupStore`, entity, audit interceptor, controllers, ownership/report policies, creator/key and evaluation/rule locks, expected revision, predecessor FK, unique indexes, canonical finding/event hashes, pagination and history retrieval were reused without modification. No revision logic was rewritten. Eligible responsible DS remains the only writer; Commissioner keeps explicit read-only report authority. Foreign DS, applicants and anonymous users remain denied.

Exact paths and before/after SHA256 are in [CHANGED_FILES.json](CHANGED_FILES.json). Repository-relative paths:

| Changed file | Purpose |
|---|---|
| StateLandGovernance/src/Modules/GovernanceIntelligence/Application/RegulatoryReports/RegulatoryFollowups.cs | Third-operation validation and saved-finding eligibility guard |
| StateLandGovernance/src/Modules/GovernanceIntelligence/Infrastructure/Persistence/Configurations/RegulatoryFollowupConfiguration.cs | Append only the reviewed CHECK clause |
| StateLandGovernance/src/Modules/GovernanceIntelligence/Infrastructure/Persistence/Migrations/20261024022733_AllowRegulatoryNoFollowupRequired.cs | CHECK-only forward migration; Down refuses history-damaging narrowing |
| Same migration .Designer.cs and GovernanceIntelligenceDbContextModelSnapshot.cs | EF model metadata |
| StateLandGovernance.Web/src/modules/component-04/services/regulatoryFollowups.ts | Saved-finding action options, labels and clear server validation messages |
| StateLandGovernance.Web/src/modules/component-04/components/RegulatoryFollowups.tsx | Existing form action/rationale and field visibility |
| Same components/RegulatoryFollowupHistory.tsx | Exact saved No Follow-up Required label; original outcome/revision retained |
| StateLandGovernance/tests/UnitTests/GovernanceIntelligence/RegulatoryFollowupTests.cs | New operation, mandatory flags/outcomes, rationale, field shape and migration Down tests |
| StateLandGovernance/tests/IntegrationTests/RegulatoryFollowupProof/Program.cs | Extend existing actual HTTP/repository proof for the new operation and NEW isolated target |
| StateLandGovernance/tests/IntegrationTests/InvestigationEvidenceProof/Program.cs | Test-only allowlist addition for the exact NEW Step 2 restore; Step 1 application/schema/UI unchanged |
| StateLandGovernance.Web/tests/component-04-followups.test.ts | Update the intentionally changed eligible action and shared-label expectation |
| StateLandGovernance.Web/tests/component-04-no-followup.test.ts | Four new action-policy, label, revision and saved-history tests |

Review/evidence files in this folder and ignored local scripts/logs were also written. Earlier unrelated work was preserved. Nothing staged, committed or pushed.

## Exact migration and permissions

Migration **20261024022733_AllowRegulatoryNoFollowupRequired** follows Step 1 migration `20261024000805_AddInvestigationEvidenceAndFinalization` in isolation. [REVIEWED_FORWARD.sql](REVIEWED_FORWARD.sql), SHA256 **bf2bf502852ecc08e4abdd63f108955cb7a397ffae27a5bdb2020f070f029489**, contains only:

1. Begin transaction.
2. Drop and replace `ck_regulatory_followup_operation`, preserving both original branches and appending `(Operation='NoFollowupRequired' AND ReviewOutcome IS NULL AND MissingInformation IS NULL)`.
3. Insert this migration's own history row; commit.

No new table/column/index/FK, data backfill, existing-row update/delete, journey store or grant change. Remarks, status, reference/hash bounds, revision indexes and predecessor constraints remain untouched. EF reports no pending model changes. Do not edit or replay earlier applied migrations.

**Step 2 adds no grants.** [PERMISSIONS_REVIEW.sql](PERMISSIONS_REVIEW.sql) documents that existing regulatory follow-up SELECT/INSERT suffice. The isolated role was checked to have only those two table privileges. Actual runtime UPDATE/DELETE/DDL were denied. The database CHECK independently rejected a NoFollowupRequired payload with a non-null ReviewOutcome. The server, rather than the CHECK, enforces saved-rule policy and ownership.

## Verified backup and NEW isolated restore

The ordinary read-only baseline, current archive and model/package/private-file hashes were verified before implementation/testing. Ordinary current rows still matched the complete Step 1 archive, so no duplicate archive or baseline replacement was necessary.

Archive: `.tmp/c4-step1-backup-20261024-finalization/state_land_governance_c4.dump`; SHA256 **1b5f7ba0872f0cf59fe9f118c06cb3079f9c6a965120b568e31647e3f3032dd0**. Secure ACL, checksum and archive listing remain verified. Older archives unchanged.

NEW database **127.0.0.1:57657/c4_step2_restore_20261024**. Restore skipped cluster-specific ownership/ACL execution; original ACL definitions remain in the archive. All 41 original tables/rows, normalized schema and 21 migrations matched before changes. Applied the reviewed Step 1 prerequisite, then this Step 2 CHECK delta only. Result: **42 tables / 23 migrations**, no new table from Step 2. Existing-shaped restricted privileges plus Step 1 attachment rights were reconstructed only in isolation for synthetic `step1_runtime`; no ordinary credentials were used for test writes.

After the proofs, every original row-hash multiset remained in the copy, alongside clearly fictional isolated fixtures. All other follow-up constraints and every follow-up index match the ordinary baseline. See [NEW_RESTORE_AND_MIGRATION.json](NEW_RESTORE_AND_MIGRATION.json) and [FINAL_ISOLATED_PRESERVATION.json](FINAL_ISOLATED_PRESERVATION.json). Row comparison uses MD5 of canonical full rows; archive identity uses SHA256. No original record was overwritten to make the comparison pass.

## Tests and security evidence

| Gate | Final result |
|---|---|
| Focused backend/regressions | **395 passed, 0 failed/skipped** |
| Actual regulatory controller/repository proof | **83 checks passed**, restricted isolated PostgreSQL |
| Step 1 actual HTTP evidence/finalization regression on Step 2 copy | **60 checks passed** |
| Focused frontend follow-up + Step 1 tests | **11 passed, 0 failed/skipped** |
| Full frontend | **196 passed, 2 pre-existing failures, 0 skipped** |
| Architecture | **7 passed, 0 failed/skipped** |
| Focused lint | **0 errors** |
| EF model | No pending changes |
| TypeScript | Only the three pre-existing C1 errors |

The new-operation tests demonstrate successful explicit rationales, rejected short/blank/oversized rationale and non-null irrelevant fields, mandatory review/clarification/hold denials through direct API calls, owner/foreign DS/Commissioner/applicant/anonymous restrictions, revisions 1/2/3, revision-4 concurrency with one winner, stale revision rejection, same-key historical replay returning original event plus current revision, changed-content conflict, independent rules/assessments, new-host retrieval and canonical hashes bound to unchanged saved finding/version. Existing clarification, all three human-review outcomes, current/previous history and live read-grant/directory revocation remain covered.

Step 1 compatibility proves exact-scope uploads/downloads, all seven findings, persisted finalization, replay/concurrency, conservative unchanged Early engine evaluation, independent reruns, later manual verification and host-recreated downloads. No classifier/anomaly call was made. Synthetic authentication/directory/grant adapters are isolated test fixtures, not ordinary login evidence. Fixture findings are explicitly fictional saved data, not fabricated model predictions or claimed real screening outputs.

Initial new tests exposed two test-fixture mistakes: whole-record equality over a newly normalized reference array, and an incomplete frontend finding missing its category/requirement fields. Fixtures/assertions were corrected to check semantic fields and complete metadata, then rerun. No application policy was weakened. Those initial logs remain local.

The two unchanged full-frontend failures are Post-Workflow presentation expectations in component-04-handover.test.ts (older case-sensitive anomaly heading) and component-04-polish.test.ts (older technical-details label). Post-Workflow source/tests were not edited to force a green Step 2 report. TypeScript failures remain RecommendationsView.tsx:309 TS2345 and ParcelCatalog.tsx:131/143 TS18048. Existing NuGet vulnerability-feed/nullable warnings remain. No full production frontend build or whole-repository green claim.

No graphical browser/keyboard/responsive proof is claimed. Manual verification needs an explicitly isolated frontend/API setup; no ordinary frontend configuration was changed to create one.

## Ordinary preservation, services and models

[READ_ONLY_PREFLIGHT.json](READ_ONLY_PREFLIGHT.json) and [FINAL_ORDINARY_PRESERVATION.json](FINAL_ORDINARY_PRESERVATION.json): ordinary **localhost:5432/state_land_governance_c4, OID 34144, 41 tables / 21 migrations**. Every captured original row, schema, migration, sequence and durable table/column grant remains unchanged. Both Step 1 and Step 2 remain undeployed. Historical temporary-schema grant noise is excluded only as previously documented; no durable baseline was relaxed or repaired.

Private candidate/permission documents and the currently approved published API package are unchanged. No ordinary complaint, follow-up, evidence or screening fixture was written; no ordinary engine/model request ran. No API package was published or ordinary launcher/configuration changed in this task.

Frozen complaint v2 SHA256 **63b37eb7175e74a8dcde82bf042fcf9dcbb4f0432fbe3b4677aac037007a20ae**; anomaly v1 SHA256 **59ed4a73d56d9f06798c4ebc4684a633afe72841c41ed0b3ba4e7bb658782b46**. Models, ML/inference/threshold/feature contracts, 24 regulatory rules and deterministic engine, Conflict/Post-Workflow engines, C1/C2/C3 and blockchain were not changed. Source before/after inventory confirms the existing regulatory follow-up repository and Step 1 application files were preserved.

Final listeners: native PostgreSQL **5432/PID 7024**; **no API 5264, frontend 3000 or ML 8104 listener**. No ordinary service was started, stopped, restarted or deployed. The isolated container was started only for testing and stopped successfully (exit 0) afterward, retaining data. See FINAL_ENVIRONMENT_STATUS.json. Source changes may be served if a development frontend is later started; that would not deploy the compatible backend migration/package.

## Remaining boundary

Ordinary deployment needs separate explicit approval and a new immediate source/package/configuration/data/grant preflight, appropriate verified backup/NEW restore, reviewed successor package/launcher, migration-chain review (ordinary still lacks Step 1), preservation checks and controlled startup. Do not apply this migration alone without its verified predecessor. No recovery may narrow the CHECK after new operation rows exist, delete history, execute destructive Down or restore over ordinary storage.

**Stop after Step 2. No Step 3–7 work, staging, commit, push or ordinary activation.**
