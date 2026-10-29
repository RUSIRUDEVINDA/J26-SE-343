# C4 Step 10 — workflow corrections and isolated verification

2026-10-24, Asia/Colombo. Implemented in the working tree. Ordinary services were not restarted; their running release has not been replaced. No staging, commit or push.

## Root cause and Early Governance changes

`OfficerFlows.cs`, `OfficerInvestigationInput.Indicators`, maps a recorded finding to `Unverified`; only an untouched `NotYetVerified` finding without remarks becomes `Missing`. Finalization is a separate durable event. It deliberately does not promote either Issue Found or No Issue Found to independently verified evidence. `OfficerAssessmentResult` previously displayed that engine evidence summary as the main result and unconditionally mounted the separate manual-verification form, which also loaded previous verification histories.

The main current-workflow result now reads the exact saved investigation, its evidence/finalization, and the selected request's independent verification records. It displays:

- Investigation and assessment identifiers, persisted finalization status, responsible officer and timestamp.
- All seven saved findings and officer remarks.
- Supporting filenames, attachment identifiers and SHA256 references.
- Only the latest independent review per concern, when recorded. Unavailable review records are explicitly unknown; finalization never implies independent verification.
- The unchanged overall engine outcome. Original engine evidence states and recommendations remain available in a collapsed technical section.
- A reminder that unresolved required evidence review remains outstanding and separate review records/actions are accessible in the saved report.

The Missing/Unverified/Verified count summary and duplicate manual-verification form are absent from the main current result. Historical reports retain the original review functionality, saved data and backend requirements. No verification record or engine state is rewritten.

The investigation draft, seven concern controls, evidence upload, explicit finalization and screening APIs remain intact. Their authorization, validation and idempotency contracts are unchanged. The draft's completion label now says finalized rather than completed/verified.

## Exact current selection and history

After Run, the workspace uses the exact returned request ID and its saved assessment ID; it does not pick a result by list order. It persists the selected request per officer in session storage and carries it in navigation URLs. An explicit URL selection takes precedence. Refresh and return navigation restore that same selection. A stale loaded report cannot render unless its request ID matches the selection. New investigation explicitly clears the selection. No history fallback occurs on failed reads.

Retrieving the selected report is independent of progression checks, so a failed prerequisite check cannot suppress retrieval or cause a historical substitution. Until the exact selected report is available, the page displays a loading/error state rather than another investigation or older result.

The current-workflow renderer does not mount the history-loading verification component. History / Reports still includes `OfficerSavedReports` and the earlier assessment workspace, linking the preserved reports. Neither historical assessment records nor audit history were deleted or overwritten.

## Regulatory contract and behavior

The actionable saved-finding selector now accepts only `InsufficientInformation` and `RequiresHumanReview`. Compliant findings remain in the original saved report, and all saved follow-up events remain readable.

| Original finding | Permitted current action |
| --- | --- |
| Insufficient Information | Request Clarification; Record Human Review also available when the saved mandatory-review flag permits it |
| Human Review Required | Record Human Review; clarification is not permitted by the existing contract for this status |
| Compliant | Excluded from actionable selector; original report remains visible; no unnecessary follow-up |

**No Changes Required — Save and Close As Is was not implemented as an actionable closure.** `RegulatoryFollowupInput.CanRecordNoFollowup` permits `NoFollowupRequired` only for a saved Compliant finding with neither blocking nor mandatory-review flags. Every finding allowed in the requested actionable selector therefore fails this closure contract. Changing its label would not make the action permissible. The UI explicitly explains this conflict instead of offering an invalid action. Historical No Follow-up Required records and their labels remain intact.

The smallest safe solution under the current contract is to retain the two existing permitted actions and exclude compliant findings, as implemented. If administrative closure of actionable findings is still required, it needs a separately reviewed governance contract that defines what closes administratively and how mandatory clarification/review remains enforced. No such policy change was inferred or implemented.

Follow-up saving continues to retain the exact pending submission for deliberate replay, show success/validation messages, refresh current history, preserve the selected evaluation, and display saved remarks, actor, timestamp and revision. Backend rule results, provenance, concurrency, authorization and immutable event handling are unchanged.

## Files edited or added for Step 10

Paths below are relative to `StateLandGovernance.Web/` unless stated otherwise. Several files already existed as untracked work before Step 10; this list does not claim ownership of their prior content.

- `src/modules/component-04/components/CurrentEarlyResult.tsx` — new current investigation/result presentation and guarded reads.
- `src/modules/component-04/components/OfficerAssessmentResult.tsx` — explicit current-workflow rendering mode; historical behavior retained.
- `src/modules/component-04/components/EarlyGovernanceStepOne.tsx` — finalized label and current result mode.
- `src/modules/component-04/components/KiwulaAssessmentWorkspace.tsx` — exact selection persistence, stale-report guard, independent selected-report read, current result mode.
- `src/modules/component-04/components/RegulatoryFollowups.tsx` — actionable filtering and contract-conflict explanation.
- `src/modules/component-04/services/currentEarlySelection.ts` — new actor-scoped selection helpers.
- `src/modules/component-04/services/regulatoryFollowups.ts` — actionable status predicate; operation permissions unchanged.
- `tests/component-04-step10.test.ts` — eight regression tests.
- This verification document.

No C# backend, migration, grant, rule engine, ML model, Conflict Detection, C1, C2, C3 or blockchain implementation changed for this task.

## Verification results

| Check | Actual result |
| --- | --- |
| Full frontend suite | 254 passed, 0 failed, 0 skipped; includes eight new Step 10 regressions and existing Combined Findings/provenance tests |
| TypeScript, `npx.cmd tsc --noEmit` | Passed |
| Next.js 16.3.5 production build | Passed; 37 static pages generated |
| Relevant API unit suite | 40 passed, 0 failed, 0 skipped |
| Governance backend regression selection | 947 passed, 0 failed, 0 skipped |
| Architecture suite | 7 passed, 0 failed, 0 skipped |
| InvestigationEvidenceProof, existing isolated PostgreSQL fixture | 89 checks passed; OrdinaryWrites 0; classifier/anomaly calls 0 |
| RegulatoryFollowupProof, existing isolated PostgreSQL fixture | 83 checks passed; OrdinaryWrites 0; inference calls 0 |
| ESLint across eight changed frontend files | Passed, no diagnostics |
| Full repository frontend ESLint | Failed: 46 errors and 23 warnings outside the eight changed frontend files; left untouched |

The backend regression selection excluded the PostgreSQL integration classes, Phase3A persistence foundation, Phase3B PostgreSQL/smoke classes and blockchain live tests. Those were not counted as passes. PostgreSQL write verification instead used the existing guarded proof programs against `127.0.0.1:57657/c4_step3_restore_20261024`, isolated from ordinary C4 on port 5432. No migration or grant ran. Tests created synthetic fixtures only in that isolated database. The investigation proof also exercised the existing sequence gate and actual unchanged 24-rule regulatory evaluation.

Investigation checks cover draft creation, seven findings, evidence validation/upload/download, finalization, screening, saved retrieval, authorization, audit immutability, exact Early binding and unresolved-review holds. Regulatory checks cover clarification, human-review outcomes, existing legitimately permitted compliant no-follow-up records, saved history, replay, concurrent/stale revisions, authorization and rejection of closure for missing information or mandatory review. Frontend regressions cover exact selected identity, refresh/navigation storage semantics, actor isolation, new-investigation clearing, seven saved findings, latest current verification, absent duplicate form, and actionable filtering.

The production build ran from `.tmp/c4-step10-web`, a separate source snapshot. Only its temporary configuration sets a broader Turbopack filesystem root to allow the dependency junction; the first snapshot build failed because that root was too narrow, and the corrected final build passed. Ordinary `.next` output was not replaced. A PowerShell `npx.ps1` policy failure was resolved by using `npx.cmd`; final TypeScript passed.

Evidence logs are `.tmp/c4-step10-{frontend-final,typescript-final,build-final,api,governance,architecture,investigation-proof,followup-proof,focused-lint,lint}.log`.

## Remaining limits and handoff

- The requested administrative closure conflicts with the existing reviewed governance contract and is intentionally unavailable for actionable findings.
- Refresh/navigation are covered by selection regression tests; a new graphical end-to-end browser journey was not run in this task. The ordinary corrected application and its PostgreSQL records still require the user's planned manual verification after separately approved activation.
- Full repository lint remains failing outside this change scope.

No connection to or write against ordinary PostgreSQL was made by this task. Ordinary Kiwulakale/demo records, saved assessments, investigations, grants, audit history, rules and models were not used for test writes. Existing services were not stopped or restarted. Work stops here after implementation and isolated verification; no Git index, commit or push action was performed.
