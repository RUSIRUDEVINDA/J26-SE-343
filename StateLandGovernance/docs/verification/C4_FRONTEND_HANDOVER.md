# Component 4 frontend handover - 2026-10-20

**Later update:** [PP1 polish and runtime acceptance](C4_PP1_POLISH_ACCEPTANCE.md) records the subsequent circular-check UI, restored isolated ML runtime, successful real inference and reproduced evaluation reports. The unavailable-ML observations below are retained as historical evidence. Remaining backend security/lifecycle and ledger dependencies are still explicit.

## Result and scope

The ordinary Governance Intelligence frontend now has an assigned-case overview, progressive consolidated screening, current/historical assessments, a separate Development input panel, complaint classifier/history boundaries, post-workflow model actions, follow-up readiness and actual integrity receipts. Figma file q0Ax9sGn66UaeMBYITGzMK was accessible: frames 7:1985 (dashboard), 7:1681 (post-workflow) and related C4 frames were inspected. The existing shared header, burgundy navigation, tokens and card components were reused as inspiration rather than copying the design or its illustrative counts/results.

**Implemented and browser-demonstrated:** actual consolidated Regulatory/Conflict/Early Governance sections, separate early-governance evaluations, history/currentness, refresh retrieval, scoped denial, unavailable ML/classifier requests and unavailable ledger verification. **Blocked:** successful ML/classifier inference, operational complaint registration/review/resolution, standalone protected consolidated-fixture dispatch, general workflow-projection import and successful blockchain anchoring. These are not labelled complete.

Only frontend/test/docs files were edited. Existing C4 backend algorithms, policies, persistence and migrations, C3 backend/Domain, host configuration and C1/C2 business logic were preserved. A before/after hash comparison found **zero changes among 1,278 backend source/configuration files**: [boundary evidence](C4_FRONTEND_BOUNDARY_EVIDENCE.json). Ordinary authorized API evaluations did persist labelled test records; that is not a persistence implementation/schema change. No new authority grant, commit, push, reset or deletion occurred.

## Ordinary startup

From the workspace root, keep the existing PostgreSQL database and local API configuration:

```powershell
powershell -File StateLandGovernance/tools/development/Start-LocalTestApi.ps1 -EnableLocalTestSignIn
```

In another terminal:

```powershell
Set-Location StateLandGovernance.Web
npm.cmd run dev -- --webpack --hostname 127.0.0.1 --port 3000
```

Open http://127.0.0.1:3000/sign-in. Use **ds / Local-Test-2026!** for the existing explicitly assigned Development officer. These are public local test credentials, not production authority. The Development input panel additionally requires Next.js Development, localhost/127.0.0.1 and the API's explicit opt-in. It is unavailable in a production build. No browser-console token editing is required.

The frontend never mints a Component 1 arbitrary-profile session for C4 engine calls. Existing authenticated local session headers are reused. Direct consolidated dispatch still requires its existing service capability. C3 gates and the active durable assignment remain authoritative.

## Manual click-through

1. Sign in as **ds**, open **Governance Intelligence** (`/component-04`), and select **SL-LMS-2026-745967** in Assigned case. The selector comes from C3's scoped queue; typing a fixture ID cannot grant access. This case is an explicitly designated synthetic uploaded Proposal, not an operational application.
2. Click **Continue screening**. Select the labelled overlap or no-conflict Development spatial context, separately check synthetic governance/complaint/concurrent coverage, then **Check prerequisites -> Run consolidated screening**. Open **Regulatory -> Conflict -> Early Governance -> Summary**. These four tabs display one consolidated result, not four fake requests.
3. Refresh. Inspect the persisted assessment. Another run requires fresh explicit coverage consent. **Assessment history -> View assessment** shows findings and expandable exact snapshot/document/policy/evidence bindings. The current marker comes from fresh C3 currentness, not from being the newest row. Changing context makes the previous input binding historical/stale. Focus rechecks currentness.
4. Open **Development input scenarios**. The source must already have an available synthetic designation and active case/document review assignment. Inspect **C3 fixture contracts and version bindings**. Select **Complete supplied input**, consent, then **Run actual early governance engine**. Repeat with reviewed complaint/governance concerns or incomplete evidence when deliberately creating another test evaluation. No result is promised by selecting an input.
5. Open **Assessment history -> Load early-governance assessment history**. These separately persisted individual-engine evaluations carry C4-INDEPENDENT-INPUTS-1 input versions. They are not consolidated C3 clearance or verified complaint records. Refresh and reload to retrieve actual stored assessments.
6. In Development inputs select **Completed workflow / ordinary recorded events** or **delays and decision revisions**, explicitly consent, and click **Run actual post-workflow model**. The request goes to C4's actual configured ML client. In this environment it fails because the model service cannot start; no score/explanation is supplied. The raw prediction endpoint is transient, so a successful future prediction would not become a persisted workflow assessment automatically.
7. **Run actual complaint classifier** sends explicitly synthetic complaint text for the designated case. Currently the service is unavailable. **Complaint intelligence** shows classifier history and the unsupported registration/review/resolution stages. The disabled operational actions do not invent a complaint lifecycle. A classification never verifies an allegation.
8. **Post-workflow analysis -> Load workflow projection and history** reads the actual persisted projection. This case has none (404). Completing real C3 execution and recording actual DS notification evidence is required for operational scoring; a Commissioner approval cannot be renamed to that event. The independent test trace does not complete this case.
9. **Governance follow-up** reads existing C3 readiness; open the C3 workspace and **Post-workflow governance & audit** for existing authorized actions. Missing execution, notification or reviewer capabilities remain blockers.
10. **Integrity and audit -> Load case audit history -> Read actual receipt**. To test verification, explicitly consent then click **Verify registered audit record**. Actual receipts currently say NotAnchored/Unavailable. A local record hash is not a ledger transaction. No anchor is fabricated or automatically requested.
11. Sign out, sign in as **external** and try the same screening URL. The case screening API returns 403 and no full Proposal is released. Institutional accounts retain their restricted request/evidence workspace. Applicants cannot pass the officer screening-read guard used before legacy history reads; an actual applicant browser check returned 403 and sent zero classifier-history requests ([evidence](C4_APPLICANT_ACCESS_EVIDENCE.json)).

Existing supporting tools (risk, explanations, consensus, conditions, timing and Commissioner/summary views) remain linked under **Existing diagnostic and supporting tools** on Overview. Their prior contract limitations remain; they are not claimed as newly completed. Old unscoped case-assessment navigation now leads to the scoped history view. Static demo-result mode is no longer exposed; old files/tests were preserved.

## Screen -> endpoint -> actual engine/store

| Screen/action | Actual endpoint | Execution / persistence |
| --- | --- | --- |
| Assigned source selector | GET `/api/workflow-governance/cases` | Existing C3 active assignments / owner-safe projection |
| Overview/currentness | GET `/api/workflow-governance/cases/{caseId}/screening` | C3 snapshot, dual-completeness and current input/policy binding checks |
| Supported consolidated run | POST same `/screening` | Existing C3 adapter invokes C4 `OrchestrateGovernanceScreeningCommandHandler`; Regulatory Compliance, Conflict Detection and Early Governance engines; existing C3/C4 result stores |
| Consolidated history/details | GET `/api/governance-intelligence/screening/cases/{caseId}/history-consolidated?limit=30` | Existing protected consolidated store; exact returned assessment metadata/findings |
| Synthetic designation/access | GET `/api/development-auth/cases/{caseId}/screening-scenarios` (existing legacy fallback retained) | Existing server-owned Development context and durable case/document assignment; frontend creates no grant |
| Individual early-engine input | POST `/api/governance-intelligence/screen-early-governance` | Actual seven-indicator deterministic engine; persists existing individual engine assessment |
| Individual early history | GET `/api/governance-intelligence/screening/history/cases/{caseId}` | Existing early screening assessment store, distinct from consolidated history |
| Complaint classifier test | POST `/api/governance-intelligence/classify-complaint` | Actual configured complaint ML HTTP client; successful assessments use existing store; unavailable now |
| Classifier history | GET `/api/governance-intelligence/complaints/history/cases/{caseId}` | Existing classifier assessments, not complaint submissions/reviews/resolutions |
| Raw completed-trace test | POST `/api/governance-intelligence/predict-workflow-anomaly` | Actual configured frozen Isolation Forest client; transient response, no persisted trace/result on this route |
| Persisted workflow projection | GET `/api/governance-intelligence/workflow-projections/cases/{caseId}` | `IComponent3WorkflowGovernanceProjectionStore`; no general public import route |
| Persisted scoring | POST `/api/governance-intelligence/workflow-anomalies/assess` | Projection ID + case ID + workflow-run ID; real model handler/store; available only when actual eligible projection exists |
| Persisted model history | GET `/api/governance-intelligence/workflow-anomalies/history/cases/{caseId}` | Existing model assessment store |
| Follow-up readiness | GET `/api/workflow-governance/cases/{caseId}/post-workflow` | Existing C3 execution/DS-notification/currentness/authority prerequisites and follow-up records |
| Audit history/receipt | GET `/api/governance-intelligence/audits/history/cases/{caseId}` and GET `/audits/{id}/receipt` | Existing registered C4 audit records/receipts |
| Integrity verification | POST `/api/governance-intelligence/audits/{id}/verify` | Existing blockchain verification integration; no frontend hash-to-transaction substitution |

## Fixture-to-live replacement contract

Input adapter: `StateLandGovernance.Web/src/modules/component-04/services/developmentInputAdapter.ts`; version **C4-INDEPENDENT-INPUTS-1**. It contains no model score, classification prediction, engine finding, clearance promise, official citation, real identity or extracted-fact provenance. Every actor/document/snapshot fixture UUID is an input identifier only.

| Adapter part | Inspected actual contract | Fixture semantics / live replacement |
| --- | --- | --- |
| `verifiedSnapshot` | C3 Application/DTOs/VerifiedFactSnapshotDto.cs, VerifiedFactEntryDto, FactValueDto | Exact serialized property names and actual outcome strings OutputsProduced/VerifiedFacts. Simulated human-origin entries, SourceExtractedFactId null and explicitly synthetic quotes; not OCR/Gemini. Replace with the exact published authorized C3 snapshot, never insert fixture entries into an uploaded analysis. |
| `screeningInput` | C4 Application/Screening/Contracts/GovernanceScreeningContracts.cs | GovernanceScreeningCaseInputDto document/snapshot/proposal version, parcel/spatial evidence, explicit supplied governance flags, complaint and concurrent evidence. Actual ProposalVersion identity is preserved. Trusted dispatch must supply its full-input/policy bindings; this preview is not a grant or a published C3 source. |
| `missingInputs` / null consolidated payload | Same C4 non-null boolean fields; actual C3 readiness adapter | Unknown governance states cannot become false. Incomplete scenario constructs no consolidated payload. Its supported early indicators explicitly use Unavailable and produce unresolved output. |
| `postWorkflowTrace` | C3 Application/Interfaces/IPostWorkflowAssessmentGateway.cs and PostWorkflowGovernanceService.TraceAsync | Same case/execution/revision/fingerprint/events/Development-mock fields. Uses supported stage Started / decision activity strings, current/prior decision histories and a separate simulated DS Final Notification. Its UNBOUND-FIXTURE fingerprint is explicitly a preview marker, not a trusted C3 hash; no source execution is created. Replace the whole trace with the trusted C3 service output. |
| `historyAnnotations` | C3 execution decision history semantics | Extra fixture-only provenance for stages, institutions, actors, decisions, revisions and reasons; not an invented C4 DTO or ML field. |
| `toAnomalyRequest` | Actual Component4PostWorkflowAssessmentGateway + WorkflowAnomalyEvent | Maps sequence/activity/institution/actor/AtUtc to eventSeq/activity/institution/resource/naive timestamp. Explicit UTC instants are required; UTC wall-clock semantics are declared, not guessed. Rejects a missing DS Final Notification. No Commissioner decision is renamed. |
| `indicators` | ScreenEarlyGovernanceCommand and EarlyGovernanceIndicatorDto | The seven actual indicator names and supported VerifiedPresent/VerifiedAbsent/Unavailable states. Synthetic reviewed concerns remain input assertions, not real complaint verification. |
| Complaint text | ClassifyComplaintCommand | Clearly labelled fictional English test text. A model suggestion is not promoted into ScreeningComplaintEvidenceDto or a trusted C3 coverage provider. |
| `provenance` | Frontend fixture envelope only | DevelopmentInputFixture is an explicit frontend label, not a guessed backend enum/authority snapshot. Source metadata is displayed separately from real backend output. |

Scenarios cover complete supplied evidence, overlap/concurrent evidence, synthetic reviewed governance/complaint concerns, incomplete evidence, ordinary completed trace and delayed/revised trace. Only supported action contracts run. The existing ML event contract consumes five event fields and nine derived statistical features; it does **not** consume decision justifications. Missing-justification detection cannot be claimed. Annotation reasons are retained for human review only. The two completed-trace fixtures are not persisted as completed C3 applications.

Independent consolidated fixture dispatch is blocked: C4's existing POST `/screening/orchestrate` requires **ConsolidatedScreeningDispatcher**, while these officer identities legitimately have scoped review access only. The UI does not mint a grant, substitute fixture IDs for case scope or bypass C3 prerequisites. Use the supported C3 consolidated screen for the existing no-conflict/overlap context. An owner-provided trusted peer adapter is needed to submit arbitrary complaint/concurrent consolidated fixture payloads under the actual dispatcher contract.

## Actual evidence, separated from fixtures

[UI/network evidence](C4_INDEPENDENT_UI_EVIDENCE.json): actual deterministic early engine responses for complete inputs **Clear**, reviewed concerns **ReviewRequired**, unavailable evidence **InsufficientInformation**. The records were read back from the existing persistence route. Raw model prediction and complaint classification each returned HTTP **400** with actual bodies `The workflow anomaly service is unavailable.` / `The complaint classifier is unavailable.` Operational projection read returned **404**. No score/category was substituted.

[Consolidated/history/integrity evidence](C4_INDEPENDENT_SCREENING_INTEGRITY_EVIDENCE.json): overlap returned **Blocked**, assessment **28c7877b-2899-4bc4-be71-e8888081c1ad**. No-conflict returned the existing matching **Cleared** engine assessment **a9b0e9dc-55d0-4e82-a90c-bb94ea759919** through the existing safe cache contract; it is not claimed as a new uncached inference. Four sections opened, refresh retrieval passed, and history retained both (currentness determined by C3 input binding, not row timestamp). The case was left on the no-conflict context. Original Proposal, OCR run, reviewed snapshot and completeness were not replaced.

Actual registered audit record **c5503233-ea93-490f-9c4a-1f6aaed15ec5** returned HTTP 200, AUDIT-V2, **NotAnchored**, null transaction/block. Actual verification returned HTTP 200 with **Unavailable / NotAnchored**, explanation that the blockchain ledger service was unreachable. HTTP 200 is not treated as successful ledger verification. No anchor operation was issued. External institutional access to this case's screening returned **403**.

Screenshots remain under ignored `.tmp/c4-independent-*` because they include case evidence. The committed evidence contains sanitized responses/statuses and synthetic case data, no tokens/passwords/credentials. Browser action fixtures and unit fixtures are not evidence of production identity issuance or real-world clearance.

## Exact owner dependencies / safe handover limits

1. **ML runtime:** API configuration points complaint and anomaly clients to `http://127.0.0.1:8104`. The documented existing launcher was attempted; Python failed importing `joblib`. Both installed local Python interpreters lack joblib and a runnable configured ML dependency stack. Model artifacts were not retrained, regenerated, replaced or bypassed. Prepare an approved isolated runtime using the repository's existing ML requirements, verify the frozen artifact bundle, start the existing service, then repeat the same UI actions. Configured complaint default is v2; workflow artifact is `ML/anomaly_detection/runs/isolation_forest_baseline_v1/isolation_forest.joblib`, pinned by `bundles/workflow_anomaly_model_v1/local_inference_config.json` / manifest. No successful model inference is claimed.

   Owner commands (not run by this slice; install only in an approved isolated environment):

   ```powershell
   # From workspace root; .tmp is ignored. Existing files are not deleted.
   $c4Repo = (Get-Location).Path
   py -3.11 -m venv .tmp/c4-ml-venv
   & "$c4Repo/.tmp/c4-ml-venv/Scripts/python.exe" -m pip install -r StateLandGovernance/src/Modules/GovernanceIntelligence/ML/requirements.txt
   Set-Location StateLandGovernance/src/Modules/GovernanceIntelligence/ML
   & "$c4Repo/.tmp/c4-ml-venv/Scripts/python.exe" -m anomaly_detection.cli verify-bundle
   & "$c4Repo/.tmp/c4-ml-venv/Scripts/python.exe" -m uvicorn src.http_service:app --host 127.0.0.1 --port 8104
   # In another terminal, GET http://127.0.0.1:8104/health before the UI retry.
   ```

2. **Complaint lifecycle:** the only write route is classifier evaluation, not registration/review/resolution. C4 owner must define authenticated submission/tracking, assigned review and verified-evidence/resolution contracts, their concurrency/version semantics and the trusted link to C3 IScreeningEvidenceProvider. No invented endpoint is advertised as implemented.
3. **Server security:** legacy C4 evaluate/classifier/early/raw-model/history/audit actions currently lack the case/role guards present on consolidated case routes. The new UI checks C3 officer readiness before legacy case-history reads and rechecks synthetic designation before each Development write, but frontend checks do not repair a public backend route. Owner must add server-side actor/case/institution capability enforcement before operational handover. Existing consolidated protections are preserved; no arbitrary identity/session is minted to bypass them. Production identity integration is not claimed complete.
4. **Trusted dispatch/projection ingest:** define an authenticated server-owned peer input/provisioning path. Persist complete valid synthetic projections through the existing IComponent3WorkflowGovernanceProjectionStore or normal C3 completed-execution producer, with real supported terminal semantics and provenance. The HTTP assess DTO accepts only projection/case/run IDs; raw prediction accepts events but does not persist. Existing POST `/api/governance-intelligence/pp1/seeds` was also inspected: ApplyPp1SeedScenarioCommandHandler supports only its six fixed PP1 IDs and exact seed versions when GovernanceIntelligence:Pp1:EnableSyntheticIntegration is enabled in Development. It is not a general custom-event importer, cannot accept this uploaded case UUID, and does not grant C3/C4 case authority. It was not invoked or enabled to bypass assignment or source restrictions. Adding an events field to the frontend request would not import a projection. Do not weaken dispatcher authority or pretend input fixture UUIDs are grants.
5. **Integrity:** restore the configured blockchain verification service and reconcile C4's existing audit outbox/migration deployment through its owner. This slice did not apply or reverse any migration. C3 finalized-manifest registration/link to a C4 audit record remains a separate missing contract; a local C3 hash cannot satisfy it. Destructive rollback is not proposed as safe.
6. **Transport status:** current C4 ML/classifier controllers map upstream service-unavailable errors to 400. Owner can minimally map typed unavailability/timeouts to 503/504 in existing controller actions while preserving real validation failures as 400. Frontend currently shows the actual failed evaluation, with the precise upstream body recorded in evidence; it does not claim malformed user input proves service health.

## Validation and handover checklist

- [x] Actual normal-browser sign-in, assigned source selection, engine requests, actual findings, current/history state and refresh retrieval.
- [x] Complete / concern / incomplete early-engine inputs and persisted readback.
- [x] Real consolidated overlap result plus matching no-conflict result retrieval; no engine-result mocks.
- [x] Operational projection absence, unavailable model/classifier requests, registered receipt and unreachable ledger response shown truthfully.
- [x] Institutional denial; no full Proposal exposed by the C4 screens. Input IDs do not grant authority.
- [x] Frontend tests **93 passed**; production webpack build / TypeScript passed; focused C4 contract/orchestration/authorization tests **29 passed**; whitespace checks passed.
- [x] Backend source boundary comparison: **1,278 checked, zero changed**. No C4 backend changelog entry is needed because this slice made no backend change.
- [ ] Successful actual ML/classifier outputs after the owner restores the pinned runtime.
- [ ] Operational complaint review/resolution and trusted screening evidence connection.
- [ ] Trusted standalone consolidated fixture dispatch and persisted synthetic projection ingest.
- [ ] Server-side protection for legacy C4 routes, production identity handover and successful ledger anchoring/verification.

The unchecked items are explicit dependencies, not completed features. No production screening, application approval, complaint resolution or blockchain delivery is claimed.
