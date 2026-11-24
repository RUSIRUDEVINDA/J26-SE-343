# C4 Step 11 — fresh workflow and investigation layout

2026-10-24, Asia/Colombo. Fresh-opening and layout corrections implemented and verified in isolation. **Not activated in the ordinary frontend.** Compliant evaluation-level completion stopped at the contract review boundary required by Parts D3 and D5 of the request. No staging, commit or push.

## Fresh proposal opening and identifiers

Clicking **Start Screening** on the Proposals list now clears only the current officer's selected Early request and selected investigation. It clears active in-memory Regulatory/Conflict selections and opens Early Governance with an explicit fresh-workflow navigation token. That intent overrides stored Early selection. The form consumes the token, removes it from the URL and starts with seven empty choices, no selected investigation and no result. Opening or reopening makes no write request. Previous engine result lists are no longer fetched automatically by the main workspace; History / Reports still retrieves them deliberately.

The existing-investigation selector remains available with **Choose explicitly** selected. Loading that list does not select an investigation or retrieve its findings automatically on fresh opening. Selecting a listed investigation remains a deliberate action.

**New Investigation** clears the active selections and form after confirming abandonment of unsaved changes. It does not create a database record. The existing save API creates the new independent investigation when explicitly requested, with no old investigation ID or supersedes ID. The original proposal ID remains `kiulakale-demonstration-adaptation-v1`. No proposal, schema relationship or Conflict Detection relationship was fabricated or changed.

Exact identity remains:

`original Proposal ID → new saved Investigation ID → exact Early Request / Assessment ID → explicitly linked Regulatory Evaluation ID`.

Pending write submission keys are deliberately retained when selection is cleared: navigation does not discard uncertain write identity or invent a retry key. Server replay/conflict protections remain unchanged.

## Navigation, current results and unsaved changes

- After Run, Step 10's exact returned request/assessment selection and current-only result are preserved.
- Continue to Regulatory carries the current Early request ID. Return navigation and refresh preserve the active exact ID.
- Reopening from Proposals explicitly starts fresh again, taking precedence over actor-scoped session selection.
- Historical deep links continue to load their selected saved report and do not write active-workflow session selection.
- Unsaved findings/remarks trigger confirmation before New Investigation, deliberate investigation replacement, refreshing the saved snapshot, workspace stage navigation or following a link. Reload/tab close also gets the browser's native unsaved-change warning. A browser-history navigation listener requests confirmation as well. Cancelling New Investigation was verified to retain edits.

The current result still shows the exact investigation and assessment IDs, persisted finalization, all seven findings/remarks, evidence references and the unchanged engine outcome. Officer finalization remains distinct from independent evidence review. Neither the duplicate manual-verification form nor previous verification histories are mounted under the main result. Saved reports retain their existing historical functionality.

## Layout and accessibility

A dedicated CSS module scopes the corrections to the current investigation form:

- Responsive top toolbar: New Investigation, deliberate existing-investigation selector, History / Reports; one column below 760 px.
- All seven concern fieldsets use the same compact grid, heading, vertical labels, full-width dropdown, remarks textarea and file input.
- Consistent borders, padding, gaps, minimum control heights and resizable textareas.
- File controls remain within their panels; current attachments remain collapsible after saving.
- Focus outlines cover inputs, selects, textareas, buttons, links and summaries.
- Issue Found remarks validation appears beside its concern and is connected to the textarea through `aria-describedby` and `aria-invalid`.
- Existing implicit label/control associations and backend evidence/finalization requirements remain intact.

## Regulatory completion: exact contract blocker

The compliant-case interpretation in Step 11 is understood: review a genuinely compliant evaluation with no unresolved mandatory issues. It is distinct from the unresolved-issue closure rejected in Step 10.

The current backend is nevertheless a **single-finding** contract:

- `RegulatoryFollowupInput.CanRecordNoFollowup` permits only a saved Compliant finding with neither blocking nor mandatory-review flags.
- `RegulatoryFollowupStore.RecordAsync` resolves one saved rule/version, validates that rule, checks its revision and submission key, and saves one immutable event in one transaction.
- Its response confirms that one event and rule revision. It does not validate the evaluation's overall engine result or the complete applicable finding set, and does not save an evaluation-completion record.
- `ReadAsync` returns event history and write authority, not a backend-validated evaluation-level completion/eligibility decision.

Acknowledging only one finding cannot represent completion of the evaluation. Sending several browser requests would permit partial saves and would still leave the evaluation-level eligibility/coverage assertion in frontend code. The existing contract could support a separately specified explicit per-finding review process, but it cannot attest the requested single evaluation-level Save and Close action. No such claim or fake completion flag was added.

**No Changes Required — Save and Close As Is is therefore not implemented in this step.** Parts D3/D5 explicitly require stopping this part and reporting the proposed contract before implementing a necessary backend operation. No backend contract, migration, grant or policy was changed. Actionable filtering remains unchanged: only Insufficient Information and Human Review Required are selectable; compliant findings stay visible in saved reports.

### Proposed smallest backend contract for approval — not implemented

Add an application operation such as `CompleteCompliantRegulatoryEvaluation`, exposed separately from the existing per-finding action selector. Its request would contain the saved evaluation ID, a stable submission key, required officer remarks, and expected revisions for the complete applicable finding set.

The backend must:

1. Reload the immutable evaluation and full saved findings under existing responsible-DS ownership/write authorization.
2. Require the actual saved engine outcome to be compliant and reject missing/ambiguous findings, unknown/conditional/adverse outcomes, blocking flags and unresolved mandatory clarification/review. Define explicitly how saved Not Applicable findings participate in coverage; do not silently treat them as Compliant.
3. Verify every applicable rule ID/version and expected revision before writing anything.
4. Use one transaction, stable lock ordering and replay validation to save the complete acknowledgement set or none. Reuse existing `NoFollowupRequired` events only where each finding satisfies the existing contract.
5. Persist auditable batch/coverage identity and return a backend-validated completion view identifying the officer, time, evaluation, covered rules/versions and revisions. Approval must settle whether existing event fields can represent that manifest without a schema change; no migration is assumed or authorized.
6. Preserve original evaluation/findings/provenance, reject changed-content and stale submissions, and return the same completion on exact replay.
7. Provide a read/eligibility result that the UI can use for availability and the durable reviewed/completed presentation, without implying lease approval. Existing progression gates remain authoritative.

This proposed operation belongs to C4 and must not change regulatory evaluation logic. After contract approval, the evaluation-level button can invoke it, refresh saved completion history and permit only the next stage already supported by existing sequence checks.

## Verification

| Check | Actual result |
| --- | --- |
| Full frontend suite | 257 passed, 0 failed, 0 skipped; includes three new Step 11 regressions and Step 10/Combined Findings coverage |
| TypeScript | Passed, `npx.cmd tsc --noEmit` |
| Next.js 16.3.5 production build | Passed, separate snapshot, 37 static pages |
| Relevant API unit tests | 40 passed, 0 failed, 0 skipped |
| Governance backend regression selection | 947 passed, 0 failed, 0 skipped |
| Architecture tests | 7 passed, 0 failed, 0 skipped |
| Investigation PostgreSQL proof | 89 checks passed; OrdinaryWrites 0 |
| Regulatory follow-up PostgreSQL proof | 83 checks passed; OrdinaryWrites 0 |
| Focused ESLint | Passed, no diagnostics |
| Browser fixture journey | 23 checks passed; no page runtime errors |

The governance regression filter excluded the PostgreSQL integration classes, Phase3A persistence foundation, Phase3B PostgreSQL/smoke classes and blockchain live tests; none is counted as a pass. PostgreSQL persistence verification instead used existing guarded `InvestigationEvidenceProof` and `RegulatoryFollowupProof` against `127.0.0.1:57657/c4_step3_restore_20261024`. Applying the database-development skill, these programs were inspected and run with explicit isolated connection guards. No migration or grant ran. No ordinary database was contacted.

The investigation proof exercises new investigations, seven findings, evidence upload/download/validation, finalization, saved screening, exact Early/Regulatory binding, authorization, history immutability and mandatory-review progression holds. The regulatory proof exercises compliant **per-finding** no-follow-up audit events, clarification, human review, history/replay, stale/concurrent revisions, and rejection of unresolved/blocking cases. It does not verify the proposed unimplemented evaluation-completion contract.

The graphical journey used the real snapshot frontend, temporary ports 3159/58118 and in-memory API fixtures. It covered fresh/repeated proposal opening, no automatic old selection or historical-result-list fetch, no write on opening, seven fields, draft save/upload/finalize/Run interaction, independent fixture identifiers, current-only result, refresh, Regulatory return, historical deep-link isolation, and unsaved-change cancellation. Fixture API responses are not represented as PostgreSQL persistence evidence.

Desktop 1440×1000, tablet 768×1024 and mobile 390×844 checks verified all seven dropdown/textarea/file-input widths and vertical alignment, with no document horizontal overflow. Screenshots were captured and desktop/mobile images inspected. Keyboard Tab reached the remarks control from its finding selector; labels were associated. No screen-reader audit was performed.

Graphical artifacts: `.tmp/c4-step11-graphical/{desktop,tablet,mobile,current-result}.png`, `result.json` (23 checks), and `server.log`. Harness: `.tmp/c4-step11-browser.cjs`. Regression/persistence logs: `.tmp/c4-step11-{frontend-final,typescript-final,build-final,api,governance,architecture,investigation-proof,followup-proof,focused-lint-final}.log`.

An initial frontend run had one failure because an existing assertion expected the prior lowercase button text; it was updated for the requested New Investigation label. An initial browser check ran during the capability-loading transition; the harness now waits for all seven current-form controls. Final reruns passed. Repository-wide lint was not rerun; Step 10's unrelated 46 errors/23 warnings remain outside this scope.

## Files changed for Step 11

Relative to `StateLandGovernance.Web/`:

- `src/modules/component-04/components/EarlyGovernanceStepOne.tsx`
- `src/modules/component-04/components/EarlyGovernanceStepOne.module.css` (new)
- `src/modules/component-04/components/KiwulaAssessmentWorkspace.tsx`
- `src/modules/component-04/components/useInvestigationLeaveGuard.ts` (new)
- `src/modules/component-04/services/currentEarlySelection.ts`
- `src/modules/component-04/services/investigationLeave.ts` (new)
- `tests/component-04-step11.test.ts` (new)
- `tests/component-04-investigation-finalization.test.ts` (button-label expectation)

Also this report and temporary snapshot/harness/log/graphical artifacts under `.tmp`. Existing unrelated tracked/untracked work was preserved.

## Handoff and limitations

- Compliant evaluation-level completion requires review/approval of the proposed backend contract before implementation. No partial or frontend-only completion was substituted.
- Corrected ordinary frontend activation remains pending explicit approval. Temporary browser-fixture services were stopped by their harness; ordinary API/frontend/ML services were not stopped or restarted.
- Browser interactions were verified against controlled fixtures, with real persistence separately verified on isolated PostgreSQL. The user's planned ordinary application/record review remains pending activation.
- Historical investigations, assessments, evaluations, evidence, decisions and audit data remain preserved. No Kiwulakale/demo record was used for ordinary test writes. No schema, permissions, private configuration, backups, rule definitions, ML model, Conflict Detection engine, C1/C2/C3 or blockchain implementation changed.

Work stops after implementation and isolated verification. No Git index action, commit or push was performed.
