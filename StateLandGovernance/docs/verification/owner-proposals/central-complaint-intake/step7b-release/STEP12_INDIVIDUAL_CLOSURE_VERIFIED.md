# C4 Step 12 — individual finding verification and closure

2026-10-24, Asia/Colombo. Implemented and isolated verification passed. **Not activated in ordinary C4.** No ordinary migration, grant, database write or service restart. No staging, commit or push.

## Implemented behavior and authority

The actionable selector still contains only saved Insufficient Information and Human Review Required findings. It now offers **No Issues Found — Verify and Close** alongside the existing actions permitted for the selected saved finding. Request Clarification remains limited to Insufficient Information; Record Human Review retains its existing saved-status/flag eligibility. Their contracts were not broadened.

The new `FindingVerifiedClosed` follow-up operation closes one saved evaluation/rule stream only. It requires 40–2000 characters and at least eight words explaining the officer's conclusion, plus specific reviewed supporting evidence references of 20–500 characters and at least three words each. Existing bounds of ten references, actor/submission identity, required revisions and payload limits remain. References record supporting documents reviewed by the DS; they do not independently authenticate document contents or replace an external approval. This extension adds no new document-upload store.

The backend returns per-rule closure eligibility and a reason derived from the immutable saved finding and its follow-up history. The frontend does not infer closure authority. The initial explicit policy permits only:

| Saved rule | Eligible saved outcomes | Additional requirements |
| --- | --- | --- |
| NPD-LOC-001, version 1.0 | Insufficient Information or Human Review Required | Exact known location-completeness requirement, category, NPD source/document/section; nonblocking; meaningful reviewed supporting references |
| NPD-RAT-001, version 1.0 | Insufficient Information or Human Review Required | Exact known project-rationale requirement, category, NPD source/document/section; nonblocking; meaningful reviewed supporting references |

These are completeness/qualitative reviews for which the responsible DS can record a conclusion. A generic RequiresHumanReview flag on these supported requirements is fulfilled by the responsible DS's reasoned closure, rather than ignored. The saved flag and original engine outcome remain unchanged.

All other rules, changed versions/requirements/source metadata, non-applicable or unsupported outcomes and blocking findings fail closed. In particular, no authority was inferred to close readiness, land rights, environmental/social safeguards, external approval or independent-review obligations. Any recorded `FurtherInstitutionalLegalReview` referral in that same finding stream prevents this closure, even after a later generic no-concern note. An independent authoritative resolution contract would be needed before allowing such a closure. Direct POST attempts return an explanatory 400 validation response; the frontend also displays the backend reason and disables unavailable closure.

This intentionally conservative policy is necessary because saved findings do not encode a reliable reviewer-authority/independent-approval requirement. **The action is visible for every actionable finding, but successful closure is currently limited to the two supported rule/version policies above.** Expanding closure to another rule requires establishing its specific authority and evidence obligations; unknown authority is not treated as permission.

## Persistence, audit and concurrency

- Existing responsible-DS ownership, current directory eligibility and write-configuration checks still govern access. Commissioner/report-read authority does not grant closure writes.
- The existing short transaction and submission-key/per-rule advisory locks remain. The server validates the saved finding and expected revision before adding an event.
- The audit interceptor binds officer identity and UTC time. The immutable canonical event/hash includes evaluation ID, saved rule/version and finding hash, justification, references, predecessor and revision.
- Exact same-key replay returns the original event. Changed-content replay and stale revisions return 409. A new-key duplicate closure with the current revision returns a clear rejection. Closure is terminal for that saved finding; later clarification/review writes are also rejected. No reopening operation was introduced.
- A partial unique database index independently prevents more than one closure for an evaluation/rule stream.
- Storage event status remains `Recorded`. **Verified & Closed** is the separate current officer disposition derived from the saved closure event; the original engine result/evidence status is never rewritten.
- Other finding streams remain untouched and unresolved where appropriate. Sequence progression recognizes a valid closure only for the matching supported saved rule/version. Blocking and unrelated review holds remain. Existing no-concern human reviews retain their separate reviewed presentation, without being relabelled as this new closure.

The frontend shows original regulatory result, current officer status, remarks, officer, timestamp, references, revision and saved history. It refreshes the same evaluation after saving, retains exact pending requests on uncertain responses and prevents repeat submission once closed. Refresh/navigation retrieve the saved disposition. A progression recheck now retains the mounted follow-up panel so the success message is not lost; navigation remains disabled while progression is being checked.

Steps 10/11 fresh opening, exact current Early result, investigation layout, navigation and historical deep links remain intact. No regulatory engine definition, Conflict Detection, ML model, C1/C2/C3 or blockchain implementation changed.

## Required migration — isolated only

`20261024112715_AllowIndividualFindingClosure`:

1. Widens `ck_regulatory_followup_operation` for the new operation, null review/missing-information fields, at least 40 remark characters and at least one evidence reference.
2. Adds `ux_regulatory_finding_closure`, unique on evaluation/rule only for closure operations.

No column, historical row, grant or rule definition changes. Down explicitly refuses removal of closure protections/history; recovery must be reviewed and forward-only. The generated designer and model snapshot match; EF reports no pending model changes.

Reviewed SQL and isolated evidence are retained in `../step12-individual-closure-20261024/REVIEWED_FORWARD.sql` and `ISOLATED_MIGRATION_EVIDENCE.json`. SQL SHA256: `2393ae71134d1da30160c727d3d15687bba93a852ca10145a01071426ed6c9ef`.

The original generated SQL bytes were applied only to the guarded existing Docker database `127.0.0.1:57657/c4_step3_restore_20261024`, in a transaction with ON_ERROR_STOP. The helper verified the exact target, permitted SQL shape, unchanged row digests for all 39 non-migration-history tables in the C4 schema, preservation of every previous migration identity, the new migration identity and the partial unique index. Existing application row digests were unchanged by migration. Subsequent synthetic fixture writes occurred only in that isolated database. Ordinary PostgreSQL was never contacted.

## Actual verification

| Check | Final result |
| --- | --- |
| Full frontend suite | 262 passed, 0 failed, 0 skipped; five new Step 12 tests |
| TypeScript | Passed |
| Next.js 16.3.5 production build | Passed from separate snapshot; 37 static pages |
| Governance backend regression selection | 958 passed, 0 failed, 0 skipped; eleven new closure-policy cases |
| Relevant API tests | 40 passed, 0 failed, 0 skipped |
| Architecture tests | 7 passed, 0 failed, 0 skipped |
| Regulatory follow-up PostgreSQL proof | 110 checks passed, including 27 new closure checks |
| Investigation/sequence PostgreSQL regression proof | 89 checks passed |
| Migration preservation and model consistency | Passed |
| Focused frontend ESLint and diff whitespace check | Passed |
| Browser fixture journey | 28 checks passed, no page runtime errors |

The governance test selection excludes the PostgreSQL integration classes, Phase3A persistence foundation, Phase3B PostgreSQL/smoke classes and blockchain live tests; excluded tests are not counted as passes. PostgreSQL writes were exercised through the two guarded proof programs instead. No ML inference ran in those proofs. Repository-wide lint was not repeated; the unrelated failures reported in Step 10 remain outside this scope.

The extended PostgreSQL proof verifies closure of one missing-information finding and a separately permitted human-review finding; exact per-rule backend eligibility; meaningful remarks/evidence rejection; blocking and recorded institutional-review restrictions; unauthorized DS, Commissioner, applicant and anonymous denial; unchanged original evaluation and all saved findings; unchanged other streams; same-key replay, changed-content/stale revision rejection, current-revision duplicate rejection and concurrent one-winner closure; persisted reads across host restart; and canonical audit hashes. Existing clarification, human-review and compliant no-follow-up regression checks remain included.

Browser verification used the real snapshot frontend with controlled in-memory API fixtures, not ordinary APIs. It verifies actionable filtering, exact closure action, one Verified & Closed disposition beside the unchanged original result, the other finding remaining Unresolved, a clear success message, disabled repeated closure, saved history after refresh, and return to the exact active Early assessment. It also reruns fresh/repeated proposal opening, no write/history-result fetch on opening, all seven responsive concern layouts, unsaved-change cancellation and historical deep-link isolation. Real PostgreSQL persistence is established separately by the proof above.

Graphical evidence is `.tmp/c4-step12-graphical/`, including `regulatory-one-closed.png`, desktop/tablet/mobile/current-result screenshots and `result.json`. The closure screenshot was visually inspected. Harness: `.tmp/c4-step12-browser.cjs`. Final logs use `.tmp/c4-step12-` with suffixes `frontend-final`, `typescript-final`, `build-final`, `governance`, `api`, `architecture`, `followup-proof-final`, `investigation-proof`, `focused-lint-final`, and `model`.

Initial verification caught outdated action-list assertions, test fixture label/category omissions, and a raw JSON comparison that was sensitive to PostgreSQL JSONB property ordering. Assertions now compare saved finding values. Browser verification also caught the lost success message described above and an early locator assertion during capability loading; the harness waits for the actual current-form controls. Final reruns passed; these initial failures are not counted as successful runs.

## Files changed

Under `StateLandGovernance/src/Modules/GovernanceIntelligence/`:

- `Application/RegulatoryReports/FindingClosurePolicy.cs` (new)
- `Application/RegulatoryReports/RegulatoryFollowups.cs`
- `Application/OfficerFlows/OfficerSequence.cs`
- `Infrastructure/Repositories/RegulatoryFollowupStore.cs`
- `Infrastructure/Persistence/Configurations/RegulatoryFollowupConfiguration.cs`
- `Infrastructure/Persistence/Migrations/20261024112715_AllowIndividualFindingClosure.cs` (new)
- `Infrastructure/Persistence/Migrations/20261024112715_AllowIndividualFindingClosure.Designer.cs` (new)
- `Infrastructure/Persistence/Migrations/GovernanceIntelligenceDbContextModelSnapshot.cs`

Under `StateLandGovernance/tests/`:

- `UnitTests/GovernanceIntelligence/FindingClosureTests.cs` (new)
- `IntegrationTests/RegulatoryFollowupProof/IndividualFindingClosureProof.cs` (new)
- `IntegrationTests/RegulatoryFollowupProof/Program.cs`

Under `StateLandGovernance.Web/`:

- `src/modules/component-04/services/regulatoryFollowups.ts`
- `src/modules/component-04/components/RegulatoryFollowups.tsx`
- `src/modules/component-04/components/RegulatoryFollowupHistory.tsx`
- `src/modules/component-04/components/KiwulaAssessmentWorkspace.tsx`
- `tests/component-04-step12.test.ts` (new)
- `tests/component-04-followups.test.ts`
- `tests/component-04-no-followup.test.ts`
- `tests/component-04-step10.test.ts`

Documentation/artifacts: `STEP12_CLOSURE_DESIGN.md`, this report, the reviewed SQL and migration evidence directory above, plus temporary Step 12 scripts, snapshot, screenshots and logs under `.tmp`. All unrelated existing tracked/untracked work was preserved.

## Remaining activation steps and limits

1. Review the additive migration and the explicit supported rule/version closure policy. Other rules remain prohibited until their authority is established.
2. Separately authorize ordinary migration application and corrected API/frontend activation. No new grants are required by this extension. None was applied or activated in this task.
3. Manually verify a separately authorized synthetic local workflow and PostgreSQL records before any commit; do not use existing Kiwulakale/demo records for writes.

Supporting references are DS-reviewed descriptions/references, not newly uploaded or independently authenticated evidence. This feature does not claim external approval or lease approval. Ordinary configuration, permissions, backups, model hashes, business records and existing services were untouched.

STOP: isolated implementation/verification complete. No stage, commit, push or ordinary service restart performed.
