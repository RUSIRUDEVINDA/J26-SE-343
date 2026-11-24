# C4 previous regulatory follow-ups and complaint form alignment

2026-10-23 Asia/Colombo. **Both fixes implemented, tested and activated.** Graphical browser/viewport/keyboard observations remain manual. No migration, grant, ordinary record change, complaint action, screening or inference was performed. No staging/commit/push.

## Regulatory history retrieval

The existing current-assessment GET/form/write path is unchanged. New authenticated read-only endpoint:

`GET /api/governance-intelligence/regulatory-assessments/{id}/follow-ups/previous?limit=25&cursor=...`

The server first authorizes the current assessment and derives its exact stored `ProposalId`. It joins saved follow-up events to their original evaluations with equality on that internal identifier, excludes the current evaluation, and applies the established exact-report readable-ID policy **before** bounded keyset pagination. No display-name, substring or browser-supplied proposal ID is used. Missing stored identity returns no related events. Limit1–50 (default25); timestamp/event-ID cursor has validated format and deterministic ordering. One lookahead determines whether another page exists. No global first100 results are filtered afterward.

Every selected original report is independently authorized, its finding/version/canonical hash is validated, and original-report/current-anchor access is rechecked after asynchronous reads. Sharing a proposal ID confers no authority. Commissioner remains governed by explicit live C4AssessmentReadAll. Applicants fail the controller role gate; unrelated DS cannot read the anchor or its prior events. The endpoint offers no write operation and introduces no new authority or permission.

The UI section **Previous Regulatory Follow-ups** sits below the current history. It groups paginated events by original assessment, merges groups split across pages and deduplicates event IDs. It displays original assessment ID/time, rule/version, action/outcome, missing information, remarks/references, officer/time/revision, and an authorized original-report link. It says **Historical — Read-only**, explaining independent current revisions. Account/assessment changes discard old data, abort requests and reject stale responses. Refresh is GET-only; loading/empty/error/load-more states are explicit. Current assessment revisions remain independent and no event is copied, transferred or rewritten.

### Actual ordinary clarification verified

Original assessment **d4e358c1-148c-41e0-b80c-6c8899413056**, original event **719c94ae-bf27-42a2-b98a-e17a374ce869** remains stored once. Read-only runtime proof verified it is discoverable from **23other authorized same-proposal assessments**, across24owned anchors. **227history-specific checks passed**, including exact persisted IDs, original assessment binding, independent report scope, Commissioner reads, unrelatedDS/applicant/anonymous denials. No remarks or complaint contents are published in evidence. See [ORDINARY_HISTORY_READ_EVIDENCE.json](ORDINARY_HISTORY_READ_EVIDENCE.json).

## Complaint form correction

New C4-local `ComplaintActionForm.module.css` is shared by the handling panel and DS triage form. Fields use one-column minmax(0,1fr) grid labels; controls have100%width/min-width0/border-box/max-width100%. Reason, applicant-safe summary and triage reason have100pxminimum height; notes112px. Other dynamic text areas/references share the same bounded layout. Buttons align to the start and wrap within the panel. Focus indicators remain visible. Confirmation checkboxes use a separate inline layout with auto width so Commissioner terminal forms do not inherit full-width checkboxes.

`ComplaintHandlingFields` is a pure presentation extraction of the existing fields, allowing render tests for TakeDirect, StartInvestigation, RecordNote, RequestInformation, RecordFindings, Resolve, Close and Reopen. Field values, payload keys, allowed actions/outcomes, required/max-length/disabled states, pending-key handling, terminal confirmations and save functions are preserved. Wrapped labels retain label/control associations; applicant-summary/reference helper text now has unique aria-describedby links. Triage uses the same styling and the explicit **Triage Reason** label. Only publicSummary remains applicant-visible; internal reason/notes/reference privacy contracts are unchanged. No complaint backend/lifecycle/classifier change.

## Tests and limitations

| Verification | Observed result |
|---|---|
| Focused backend/authorization regression |324passed,0failed/skipped |
| Frontend |162passed,0failed/skipped |
| Architecture |7passed |
| Real HTTP/repository/restricted PostgreSQL |47checks passed |
| Focused lint |0errors/warnings |
| EF model check |No pending changes; no migration generated |
| Release publish |Succeeded |
| Broader ordinary runtime |530checks,53saved reports,69protected applicant API denials,10direct C4 applicant redirects |
| Existing DS evidence |79checks across11workspaces; responsibleDS access/Commissioner read-only/other-user denial preserved |

The isolated proof creates A with five follow-up events and B as a distinct saved same-proposal assessment without events. B's current history stays empty (revision0), while paginated previous history returns exactly A's five original events without duplicates. An unrelated creator's same-proposal event is denied to DS but readable by explicitly authorized Commissioner. A different exact proposal with the same fixture display title is excluded. Cursor bounds/format, refresh/retrieval after host recreation, live report revocation, original finding immutability and existing write/replay/concurrency protections pass. All writes use fictional isolated fixtures only; no ordinary screening was run to create B. Existing screening's distinct-result persistence path was not changed.

Frontend tests render fields for all eight action states, verify required/disabled/helper associations, read-only history grouping and account/abort handling, plus CSS width/height/grid/focus/responsive rules. These checks do **not** establish computed desktop/tablet/mobile layout or keyboard interaction. Browser inventory returned apps=[]/browsers=[]; no graphical verification is claimed. Manual observations remain in the walkthrough. An initial new render test expected capitalized field labels; it was corrected to compare the existing label casing without changing product semantics. No application outputs were forced.

Full TypeScript still reports only the three existing C1 errors: RecommendationsView.tsx309TS2345, ParcelCatalog.tsx131/143TS18048. No full production frontend-build success claimed. Existing NuGet vulnerability-feed warnings remain. Logs and [ISOLATED_HTTP_EVIDENCE.json](ISOLATED_HTTP_EVIDENCE.json) record actual results.

## Database, backup and deployment

Ordinary remains **localhost5432/state_land_governance_c4, OID34144,41tables/21migrations**. Every captured row fingerprint, original follow-up, schema, grants, database settings and sequence state match the fresh baseline after runtime verification. C1/C3/accounts and frozen complaint/anomaly hashes are unchanged. No schema/permission delta.

Fresh secure full read-only archive:

`D:\AI-Driven-Digital-Lease-Governance-for-State-Land-Management\AI-Driven-Digital-Lease-Governance-for-State-Land-Management\.tmp\c4-history-form-backup-20261023\state_land_governance_c4.dump`

**371780bytes**, SHA256 **60dba06d5f736a3395d0eb23bea13dff0607162be8fa72539215758c994701a6**. New account/SYSTEM-only directory; old archives untouched. Readable/listed/checksummed and restored to NEW Docker `c4_history_form_restore_20261023` on57657. All41tables/schema/21migration history match. Ownership/ACL execution skipped only in isolation; original ACL definitions remain in archive and actual ordinary grants were separately compared. Existing-shaped restricted test rights were prepared only there. The original saved ordinary clarification is included and unchanged. Isolated test storage is retained; Docker stopped after verification.

Validated successor **Start-C4HistoryFormsApprovedApi.ps1** preserves exact private configuration, separate Commissioner permission sources, read-only peers/accounts, disabled workers/sandboxes, target/runtime identity and duplicate-listener guards. Verified API13300was stopped; one reviewed `.tmp/c4-history-form-api` started as **PID16456/5264**. Frontend16692/3000andML36612/8104were reused. No startup migrations; no runtime write SQL in the new API log. Actual process environment, executable identity and package file hashes match. See [PACKAGE_SHA256.json](PACKAGE_SHA256.json), [PROCESS_IDENTITY.json](PROCESS_IDENTITY.json), [FINAL_PRESERVATION.json](FINAL_PRESERVATION.json).

Existing independent live grant revocation tests restored exact document bytes. Existing reports, complaint public-only history and manual evidence scope pass. No complaint assignment/review/decision, regulatory reassessment, evidence upload or model inference was initiated.

## Exact changed files

Repository-relative paths; full before/after hashes are [CHANGED_FILES_SHA256.json](CHANGED_FILES_SHA256.json).

- `StateLandGovernance/src/Modules/GovernanceIntelligence/Application/RegulatoryReports/IndependentRegulatoryReports.cs` — reuse established report-scope IDs for database filtering.
- `.../Application/RegulatoryReports/RegulatoryFollowups.cs` — bounded page DTO/cursor/store query contract.
- `.../Infrastructure/Repositories/RegulatoryFollowupStore.cs` — exact-proposal join, authorized keyset pagination and per-report rechecks.
- `.../Presentation/Controllers/RegulatoryFollowupsController.cs` — authenticated previous-history GET.
- `StateLandGovernance.Web/src/modules/component-04/services/regulatoryFollowups.ts` — authenticated no-store history paging.
- `.../components/PreviousRegulatoryFollowups.tsx` — grouped read-only history and safe loading/account switching.
- `.../components/RegulatoryFollowups.tsx` — mount previous history below unchanged current history.
- `.../components/ComplaintActionForm.module.css` — shared C4 stacked field layout.
- `.../components/ComplaintHandlingPanel.tsx` — local styles, pure presentation fields and helper associations.
- `.../components/RegisteredComplaints.tsx` — DS triage form alignment/label.
- `StateLandGovernance/tests/IntegrationTests/RegulatoryFollowupProof/Program.cs` — isolated A/B/scope/pagination proof.
- `StateLandGovernance/tests/UnitTests/GovernanceIntelligence/RegulatoryFollowupTests.cs` — cursor validation/roundtrip tests.
- `StateLandGovernance.Web/tests/component-04-history-form.test.ts` — grouping/read-only/render/layout regressions.
- `StateLandGovernance/tools/development/Start-C4HistoryFormsApprovedApi.ps1` — compatible package-specific successor launcher.
- This evidence directory and updated completion/handover pointers document the current result. Extensive pre-existing dirty work is not staged or attributed to this task.

No migration, model, C1/C2/C3 business code or complaint API change. No staging, commit or push. Remaining limits are manual graphical checks and unrelated C1 TypeScript failures; no implementation or runtime blocker remains.

## Commands and manual verification

Backend regression used the established filtered GovernanceIntelligence test command with explicit isolated COMPONENT4_TEST_POSTGRES_CONNECTION on57657, including RegulatoryFollowup/GovernanceAccess/ManualEvidence/Complaint/CommissionerReportRead/OfficerReportController/IndependentRegulatoryReport/DependencyInjection/OfficerFlow/Kiwula/PostWorkflow. Architecture project ran with --no-restore. Frontend ran `npm.cmd test`, focused lint and `npx.cmd tsc --noEmit`. Actual proof used strict isolated `c4_history_form_restore_20261023`, rejecting ordinary5432.

See [MANUAL_WALKTHROUGH.md](MANUAL_WALKTHROUGH.md) for the current launcher and exact visual checks. Do not create ordinary demonstration records. Recovery keeps the verified API fail-closed if needed; any earlier package requires policy compatibility review. Never restore over ordinary storage, delete follow-ups or replay migrations.
