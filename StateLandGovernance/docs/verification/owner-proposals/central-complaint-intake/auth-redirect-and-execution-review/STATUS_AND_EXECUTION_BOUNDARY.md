# Applicant sign-in corrected; Commissioner execution boundary unresolved

2026-10-23, Asia/Colombo. **PARTIAL COMPLETION.** Applicant complaint sign-in presentation is corrected and active through the existing Next development server. The requested Commissioner officer-assessment execution change is **not implemented or deployed**. No backend gate was relaxed, private grant changed, assessment created or inference invoked.

## Completed applicant correction

The complaint page's own Sign in button already carried the exact allowlisted `returnTo`. The compact header and the header's fallback link always used `/sign-in`, dropping the intended destination before the login handler could see it. Both header variants now use a small pathname-aware SignInLink. Only the exact `/workflow-governance/applicant/complaints` pathname produces `/sign-in?returnTo=%2Fworkflow-governance%2Fapplicant%2Fcomplaints`; all other header destinations remain `/sign-in`.

The existing login handler and safe-return policy are unchanged. The authenticated server-provided applicant home determines navigation eligibility; friendly role labels are not promoted to authority. Non-applicants ignore the complaint return and keep established routing. External, protocol-relative, encoded, malformed, fragment/query variants and other non-allowlisted return values remain rejected. Failed login keeps the URL/error handling. Existing API ownership checks remain authoritative.

Both actual rendered anonymous complaint-page links now contain the exact return URL. The sign-in GET preserves it; unit tests exercise successful authentication/session retention, safe destination computation and failed authentication without navigation. Actual applicant API list retrieval succeeds repeatedly. **No graphical login-click/refresh success is claimed:** browser inventory is unavailable, so that final observation remains manual.

## Exact execution blocker

1. `OfficerFlowsController.Execute` grants Commissioner only saved-report GET/history via `ICommissionerReportReadAccess`; catalogue, investigation save/read, Early and Anomaly commands retain `IsDs()`. The running Commissioner catalogue GET is403. The attached request requires appropriate **existing execution authority**, rather than evidence-write authority.
2. The independent live permission document authorizes exactly `C4AssessmentReadAll`. `CommissionerReportReadAccess` requires that exact permission array with no review operations. Complaint Read/Assign/Review is separately scoped to complaints. There is no independently scoped Commissioner officer-assessment execution permission in this implementation. Treating either read or complaint permissions as general assessment writes would change its reviewed meaning.
3. `EarlyRequest` requires an InvestigationId. `OfficerFlowStore.Run` loads it through `InvestigationAsync`, which enforces `CreatorActorId == executing actor`. Commissioner has no authorized investigation-save contract and cannot read arbitrary DS drafts. Removing only the controller gate would either still fail or require broadening this ownership policy. Creating a Commissioner investigation through the DS form would contradict the requested evidence/remarks restriction.
4. The current named-route anomaly command accepts only `NamedOfficerWorkflow.Scenario` and builds the already-approved prepared PP1 sequence. It is not a general real-case/completed-workflow selection API. A missing real workflow cannot be converted into this example merely to enable Run.
5. Older Kiwula/static-catalogue and prepared-demonstration endpoints already permit Officer-role execution under their own gates. They were **not** used as fallbacks: they have different input/source semantics and do not establish the requested Commissioner execution contract for DS-owned investigation inputs/current named-route flow.

Manual evidence's independent repository boundary already denies Commissioner writes: it requires current eligible DS identity **and** request ownership. That protection remains unchanged. Saved-result immutability, report revocation and complaint powers remain intact.

The stop follows the attached request's explicit instruction: **“If the current permission architecture cannot safely distinguish assessment execution from evidence editing, report the blocker rather than granting broad write permissions.”** The current officer command contract has no Commissioner execution/source boundary; no broad permission workaround was applied.

## Concrete continuation scope for review — not implemented

An independent execution policy should authorize only the existing exact Commissioner actor `0ab669fe-d0ab-5695-ad55-10f6b3b5aab3`, with fresh eligibility and independently revocable `C4EarlyGovernanceExecute` / `C4WorkflowAnomalyExecute` permissions. Preserve the two current complaint/report documents byte-for-byte; use a separately reviewed permission source/configuration. Read authority alone must never suffice. No database privilege/role/migration expansion is proposed.

For Early Governance, the proposed source is an **already Saved, currently readable early officer request** whose immutable investigation supplies the observations. A new Commissioner command would accept that saved source request ID and a submission key, internally bind its exact investigation/hash/version, and attribute a distinct new request/result to Commissioner. It would not expose/save/edit DS drafts or promote observations to verified evidence. The source request and investigation must remain unchanged. Current request FK and append-only action-event details appear sufficient, but generated implementation must stop if that proves false.

For anomaly, confirm whether execution is limited to the **existing explicitly disclosed prepared PP1 named scenario**, or must select **existing eligible persisted real workflow projections**. The latter requires a reviewed source-selection contract; the current command does not provide it. Neither route may manufacture missing real-case events/final notification. The frozen extractor, nine features, model, threshold, saved output and prerequisite validation stay unchanged.

After that execution/input scope is explicitly settled, implement narrow commands/capabilities and read-only Commissioner controls, keep DS evidence/clarification writes denied, and prove success/denial/idempotency/revocation in isolated storage before any compatible backend deployment. No successful execution or inference test is claimed by this report.

## Actual verification and deployment

- **173 frontend tests pass**, zero failed/skipped. Five added regressions cover both header variants, page-link parity, safe returns, role-specific routing, failed authentication and session persistence. The first run exposed a Node test-loader resolution gap for `next/navigation`; it now resolves the installed `.js` module exactly like existing next/link and next/server handling. Final full run passes.
- **35 focused backend authorization/ownership tests pass**, zero failed/skipped. Exact filter and log retained. **7 architecture tests pass**, zero failed/skipped. These are preservation tests, not proof Commissioner execution has been implemented.
- Focused lint: **0 errors / 2 existing session-hook warnings**. TypeScript still reports only three known C1 errors; no whole production-build success is claimed.
- **69 read-only authenticated/SSR checks pass**, including the two actual links, applicant tracking/admission denial, unchanged Commissioner complaint capabilities/four queues, 53 saved report body hashes and the explicit403 execution-catalogue boundary.
- All current row fingerprints, normalized schema/history, grants/settings/sequences match the initial task baseline. Database remains **localhost:5432/state_land_governance_c4, OID34144, 41 tables, 21 migrations**. Both frozen model byte hashes, private candidate, independent grant documents and all API package hashes match. FINAL_PRESERVATION.json records it.
- Services were reused: **API16456/5264**, **frontend16692/3000**, **ML36612/8104**. API remains `.tmp/c4-history-form-api` with `Start-C4HistoryFormsApprovedApi.ps1`. No API publish/restart, migration, SQL grant, ordinary fixture, assessment run, complaint action, evidence write or model inference occurred.

Exact task files/before-after hashes and task-only patch are in CHANGED_FILES.json / TASK_DIFF.patch. No staging, commit or push. C4 is not reported fully complete for this request while Commissioner execution remains unresolved.
