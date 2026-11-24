# Step 1 — implemented and isolated-tested; no ordinary deployment

2026-10-24, Asia/Colombo. **Step 1 implementation complete within the isolated-only approval.** Graphical browser verification remains pending. Steps 2–7 have not been started. This supersedes the implementation stop in IMPLEMENTATION_STOPPED_DESIGN_BOUNDARY.md after the owner's explicit CHECK/index approval; that historical review and evidence remain intact.

## Functional behavior and boundaries

Seven explicit Issue Found / No Issue Found selections are required for finalization. Untouched / historical NotYetVerified remains readable and valid as an incomplete draft, never silently converted or finalized. Saving a draft does not mean finalization, screening success or later evidence verification.

Issue Found requires meaningful existing remarks and at least one confirmed persisted attachment for the exact investigation, concern and responsible DS. A browser-selected file, another concern's file or another investigation's file cannot satisfy this. No Issue Found permits optional evidence. Original file bytes, SHA256, safe names, four existing supported content types and the 5 MiB limit use ManualEvidenceInput.Validate unchanged. Downloads require current eligibility/ownership or explicit saved-report read authority and force octet-stream attachment, nosniff and sandbox headers.

An explicit InvestigationFinalized event binds the immutable investigation input hash, seven findings and exact attachment IDs/concerns/content hashes. The interceptor supplies trusted actor and microsecond-normalized UTC and hashes the canonical typed event. A short investigation transaction lock serializes upload/finalization; a database unique partial index permits only one finalization per investigation. Same-key/content replays the saved event, changed input or another deliberate finalization conflicts. Finalized pre-screening attachments are immutable; the existing later assessment-based attachment/review workflow remains independently available. Corrections save a new investigation snapshot and do not copy files.

New Early request reservations require persisted finalization and fresh eligible owner checks. Existing saved-request same-key replays return before the new gate; saved GETs remain readable. Deliberate new requests still receive independent saved assessments through the unchanged Early engine. Finalization does not promote its conservative Unverified/Missing evidence categories. It does not establish allegation truth, automated compliance, legal approval, another institution's approval or completion of later evidence review.

Commissioner keeps existing explicit saved-report read-only authority. That permission cannot expose unscreened investigation drafts or permit upload/finalization/Early execution. Other DS, applicant and anonymous denials remain. Existing proposal identity and previous-assessment grouping are unchanged.

## Minimal frontend changes

The API capability `officer-investigation-evidence-capabilities` gates the new form. An old API without that capability retains the legacy form, avoiding incompatible upload/finalization requests against the ordinary deployment. The new form provides untouched choices, save draft, concern-scoped upload/download, explicit finalize, run and refresh. New investigation clears all findings and current files. Reload resumes only the investigation explicitly selected under that actor's session-storage key; it does not choose the newest historical investigation. Editing creates a new immutable snapshot with no inherited evidence. Uncertain writes retain submission keys; refresh/replay does not automatically invoke screening.

Saved Early reports can display persisted manual completion and their original pre-screening downloads separately from existing manual evidence verification/history. No CSS redesign or unrelated navigation change was made. Existing ordinary Next development source may hot-serve the capability check/legacy fallback; no ordinary frontend process or private setting was restarted or replaced. The new feature is not deployed against the old API.

## Exact implementation files

Full paths and SHA256: [CHANGED_FILES.json](CHANGED_FILES.json). Paths below are relative to the repository root.

| Files | Purpose |
|---|---|
| StateLandGovernance/src/Modules/GovernanceIntelligence/Application/OfficerFlows/InvestigationEvidence.cs | DTOs, persistence port and explicit seven-finding validation |
| StateLandGovernance/src/Modules/GovernanceIntelligence/Infrastructure/Repositories/InvestigationEvidenceStore.cs | Fresh authorization, exact-scope uploads/downloads, finalization, replay and locking |
| StateLandGovernance/src/Modules/GovernanceIntelligence/Infrastructure/Repositories/OfficerFlowStore.cs | Require finalization before a new Early reservation; preserve existing replay and anomaly behavior |
| StateLandGovernance/src/Modules/GovernanceIntelligence/Infrastructure/Persistence/Entities/InvestigationAttachmentEntity.cs | Approved 11-column entity, named OfficerInvestigationAttachmentEntity to reuse existing officer index convention |
| StateLandGovernance/src/Modules/GovernanceIntelligence/Infrastructure/Persistence/Configurations/InvestigationAttachmentConfiguration.cs | Exact reviewed table keys, bounds, content/hash CHECKs and history index |
| StateLandGovernance/src/Modules/GovernanceIntelligence/Infrastructure/Persistence/Configurations/OfficerFlowConfigurations.cs | Approved operation CHECK extension and unique finalization index |
| StateLandGovernance/src/Modules/GovernanceIntelligence/Infrastructure/Persistence/InvestigationEvidenceAuditInterceptor.cs | Trusted actor/time, canonical hash and immutable write guards |
| StateLandGovernance/src/Modules/GovernanceIntelligence/Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs | Module-local store/interceptor registration |
| StateLandGovernance/src/Modules/GovernanceIntelligence/Presentation/Controllers/InvestigationEvidenceController.cs | Authenticated capability, read, upload, download and finalize routes |
| StateLandGovernance/src/Modules/GovernanceIntelligence/Infrastructure/Persistence/Migrations/20261024000805_AddInvestigationEvidenceAndFinalization.cs | Reviewed additive migration; Down refuses destructive removal |
| Same migration .Designer.cs and GovernanceIntelligenceDbContextModelSnapshot.cs | EF model metadata |
| StateLandGovernance.Web/src/modules/component-04/services/investigationEvidence.ts | Typed calls, confirmed response handling, download and readiness helper |
| StateLandGovernance.Web/src/modules/component-04/components/EarlyGovernanceStepOne.tsx | Minimal draft/evidence/finalize/run form |
| Same components/EarlyGovernanceInvestigation.tsx | Capability-gated compatibility wrapper; existing legacy form retained |
| Same components/InvestigationCompletion.tsx | Persisted saved-report manual completion and pre-screening evidence |
| Same components/OfficerAssessmentResult.tsx | Mount completion only for saved Early reports |
| StateLandGovernance/tests/UnitTests/GovernanceIntelligence/InvestigationFinalizationTests.cs | Seven-choice, evidence and conservative engine-state regressions |
| Same tests/EarlyGovernanceScreeningPostgresIntegrationTests.cs | Correct historical fixture: assert old DDL, then apply its existing referral dependency before using today's repository mapping |
| StateLandGovernance/tests/IntegrationTests/InvestigationEvidenceProof/Program.cs and InvestigationEvidenceProof.csproj | Actual authenticated HTTP/repository proof, synthetic identity adapters and restricted isolated credentials; no new package dependencies |
| StateLandGovernance.Web/tests/component-04-investigation-finalization.test.ts | Five readiness and initial-form tests |

Only this folder's review/evidence documents and ignored local helpers/logs/backups were additionally written. Earlier unrelated working-tree edits were preserved. No staging, commit or push occurred.

## Backup, migration and permissions

Fresh secure complete read-only archive:

`.tmp/c4-step1-backup-20261024-finalization/state_land_governance_c4.dump`

1,815,584 bytes; SHA256 **1b5f7ba0872f0cf59fe9f118c06cb3079f9c6a965120b568e31647e3f3032dd0**. The new directory is restricted to the current account and SYSTEM. Checksum and archive listing were rechecked. Older backups remain untouched. The older early-evidence archive predates records already in the current documented baseline, so it was not used as the current restore proof.

NEW restored copy: `127.0.0.1:57657/c4_step1_restore_20261024_finalization`. All 41 original user tables, normalized schema and 21-migration history matched before the additive migration. Ownership/ACL execution was skipped at restore; original definitions remain in the archive. Existing-shaped rights were reconstructed only in isolation for synthetic step1_runtime; no ordinary credentials were used for test writes.

Migration **20261024000805_AddInvestigationEvidenceAndFinalization** creates only officer_investigation_attachments, extends ck_officer_operation while retaining all six existing operations and their parent/hash/FK constraints, and adds ux_officer_investigation_finalization. No journey table, new columns on existing tables, regulatory CHECK, data backfill, UPDATE or DELETE. EF's initial extra automatic FK index was removed by using the existing officer entity convention, not by changing global conventions. The final model has no pending changes. Down refuses destructive history removal.

Exact [REVIEWED_FORWARD.sql](REVIEWED_FORWARD.sql) SHA256: **5af0fe8d358b9c22cad2c8a68007b8503f492230d155e46a0c7b34a191916c6f**.

Exact [REVIEWED_ADDITIONAL_PERMISSIONS.sql](REVIEWED_ADDITIONAL_PERMISSIONS.sql): SELECT/INSERT only on officer_investigation_attachments for slg_c4_regulatory_runtime. Existing officer_action_events SELECT/INSERT suffice. No new UPDATE/DELETE/DDL/schema/role/sequence/account/peer/outbox rights. Isolation uses the equivalent two grants for step1_runtime. Isolated result: 42 tables / 22 migrations; the new table was empty before fictional fixtures.

## Actual tests and limitations

| Verification | Result |
|---|---|
| Focused C4 backend regressions | 385 passed, 0 failed/skipped |
| Two existing PostgreSQL regressions, run separately in isolated fixtures | 2 passed, 0 failed/skipped |
| Final current-source authenticated HTTP/repository proof | 60 checks passed |
| Architecture | 7 passed, 0 failed/skipped |
| New frontend tests | 5 passed, 0 failed/skipped |
| Full frontend suite | 192 passed, 2 failed, 0 skipped; failures are unchanged Post-Workflow presentation expectations |
| Focused lint | 0 errors |
| TypeScript | Only three existing C1 errors; no full production-build success claimed |
| Browser inventory | apps=[] / browsers=[]; no graphical, keyboard or responsive success claimed |

The full frontend failures are component-04-handover.test.ts (case-sensitive old anomaly heading expectation) and component-04-polish.test.ts (old “Model and technical details” expectation). Their component was already changed by earlier work before Step 1 and is unchanged by this task. Neither source nor those expectations were altered to force a green Step 1 report. TypeScript failures remain RecommendationsView.tsx:309 TS2345 and ParcelCatalog.tsx:131/143 TS18048.

Initial backend broad selection had two missing-test-connection failures. An old regression then exposed current repository use against pre-referral historical DDL (workflow_run_id absent); its isolated fixture was corrected after the historical assertions. Both regressions passed separately afterward. Initial new-project build required restoring cached existing dependencies; no package was added. These failed attempts remain in local logs and are not counted as successful runs.

The proof exercises all-negative and mixed finalization, missing/untouched/wrong-concern/foreign-investigation evidence, malformed/oversized files, owner/foreign/Commissioner/applicant/anonymous boundaries, same-key/content replay, changed-content and hash conflict, concurrent upload/finalization, fresh directory/report revocation, actual unchanged Early execution and independent saved reruns, existing later evidence verification/downloads, new-host retrieval and canonical typed hashes. No classifier/anomaly client or artifact was invoked. The fictional adapters are test authentication/identity fixtures, not proof of ordinary sign-in. FINAL_ISOLATED_PRESERVATION.json additionally proves that every original row remains in the restored copy and that PostgreSQL itself rejects duplicate finalization and finalization using a request parent. Existing original rows are compared separately rather than treating fixtures as ordinary records.

## Ordinary preservation and current service status

Final read-only evidence: [FINAL_ORDINARY_PRESERVATION.json](FINAL_ORDINARY_PRESERVATION.json). **localhost:5432/state_land_governance_c4, OID 34144, 41 tables / 21 migrations**. Every original record fingerprint, schema, migration history, sequence and durable table/column grant matches the preflight. Both new schema changes remain absent in ordinary storage. Private settings, permission documents, existing published API package and model hashes match. No ordinary fixture, upload, finalization, screening or ML call occurred.

The resumed comparison initially flagged raw privilege metadata because a temporary pg_temp_38.evidence_search_results table had disappeared. Exact differences are retained in RESUMED_FINAL_PRESERVATION_STOP.json. Comparing durable application grants excludes only those documented temporary-schema entries; there was no C4 permission change or baseline data reset.

At the resumed final observation, **ports 5264/3000/8104 had no listeners and their earlier PIDs were absent**. This task did not stop or restart ordinary services and does not claim they are currently healthy/running. Native PostgreSQL remained on 5432. Docker was initially unavailable after resume, then restored from the installed per-user Docker Desktop; only the retained isolated PostgreSQL container was started for the final proof. No ordinary application service was started. The isolated container was stopped successfully (exit 0) after final verification; test storage is retained. Final listener/container observations are in FINAL_ENVIRONMENT_STATUS.json.

Frozen model SHA256: complaint v2 **63b37eb7175e74a8dcde82bf042fcf9dcbb4f0432fbe3b4677aac037007a20ae**; anomaly v1 **59ed4a73d56d9f06798c4ebc4684a633afe72841c41ed0b3ba4e7bb658782b46**. No model, inference/feature/threshold contract, Conflict/Regulatory/Post-Workflow engine, C1/C2/C3 or blockchain implementation was changed. Existing standalone model outputs were not rerun.

## Remaining decisions

Ordinary deployment remains unauthorized. Any future deployment needs its own immediate current-data/grant/source preflight, backup/NEW restore if changed, reviewed compatible published package/launcher, exact additive SQL/grants, preservation verification and controlled single API activation. The old ordinary package does not enforce this new finalization gate; capability fallback is compatibility, not deployment of Step 1.

Manual graphical testing needs an explicitly isolated frontend/API configuration and fictional fixtures; do not change ordinary frontend private settings or upload ordinary evidence for demonstration. Recovery must retain additive tables/history and refuse destructive Down. Existing unrelated frontend test/build failures and currently offline ordinary application services remain explicit handover limitations.

**STOP after Step 1. No Step 2 implementation or ordinary deployment is authorized or performed.**
