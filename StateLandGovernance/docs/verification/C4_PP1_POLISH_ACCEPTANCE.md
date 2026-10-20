# C4 PP1 frontend polish and runtime acceptance — 2026-10-20

Implemented the circular check-list / selected-detail design, restored the existing ML service, reproduced frozen-model evaluations and exercised the ordinary Development portal. This supersedes the earlier report's unavailable-ML status; it does **not** claim that all C4 operational contracts are complete.

## Implemented presentation

One sidebar replaces the duplicate horizontal workspace navigation. Assigned case context persists across C4 pages and refresh and is revalidated against the scoped C3 case list; an arbitrary saved/query UUID cannot grant access. Changing cases clears previous assessment/detail state. Screening requests reject late replies after source/input changes. History resets on case changes and opens in a native modal with keyboard focus containment, Escape and focus restoration, separately from the current result.

Regulatory, Conflict, Early Governance and Summary remain views of one consolidated assessment. Circular check rows select returned explanations, evidence, source and next action. Counts derive from actual results; section status is separate from overall status. The configured research catalogue has **one** evaluated regulatory rule, `DEV-RESEARCH-NPD-1`, not full legal compliance. Consolidated early governance currently returns a seven-indicator summary without individual details; the UI states that limitation rather than inventing passed checks. The standalone engine's actual indicator results use the same checks/detail presentation. Reused recorded assessment responses are labelled as reused.

The local sign-in page, overview, scoped queue, history, complaint analysis, post-workflow analysis, follow-up, integrity, Development controls and new Evaluation Evidence page use consistent cards, controls and collapsed technical details. Complaint analysis provides text entry and actual category/probabilities. Post-workflow testing displays a C3-shaped timeline beside actual score/threshold/flag/features. Raw predictions remain transient. IDs, hashes and raw responses stay expandable. Missing completed projections now explain the required actual workflow and DS notification instead of claiming the endpoint is absent.

Desktop and 390px layouts were browser-checked. No horizontal overflow was found across the nine primary C4 views; the embedded post-workflow timeline/model result was checked separately. [Render evidence](C4_POLISH_RENDER_EVIDENCE.json) records those checks. Its initial transport-error screenshot raced the Next.js announcer; the final explicit response/text assertion corrected that test in [final UI evidence](C4_POLISH_FINAL_UI_EVIDENCE.json). The 503 test is a clearly labelled browser transport fixture, not an actual failed model run or fabricated output.

## Restored runtime and actual evaluations

Created ignored `.tmp/c4-ml-venv` with the installed Python 3.11 interpreter and installed the repository's existing requirements after the restricted network install was retried with approval. No backend/model implementation or artifact was edited. `pip check` passed, the existing anomaly bundle verified and the existing `src.http_service:app` started on loopback **8104**. Actual health: `ready`, complaint model `v2`.

| Ordinary UI action | Actual result |
| --- | --- |
| Consolidated overlap | 200, **Blocked**, persisted assessment `28c7877b-2899-4bc4-be71-e8888081c1ad`; existing matching result reused |
| Consolidated no-conflict | 200, **Cleared**, persisted assessment `a9b0e9dc-55d0-4e82-a90c-bb94ea759919`; existing matching result reused |
| Standalone early engine | Actual new Clear / ReviewRequired / InsufficientInformation responses; existing history readback succeeded |
| Ordinary completed-trace input | 200, score **0.688338019923785**, threshold **0.5743688923115832**, **flagged: true** |
| Delayed/revised trace input | 200, score **0.7117890400418192**, same frozen threshold, **flagged: true** |
| Fictional unpaid-rent complaint | 200, **Lease Revenue / Payment / Enforcement**, returned class probability **0.5480595346457706**, model **v2** |
| Classifier persistence | Assessment `2c0dee57-5a04-4fed-9e03-b5e0243cbe91` found in history after a new normal sign-in and refresh |
| Operational completed projection | 404; no completed C3 projection was inserted or terminal event fabricated |
| Registered integrity receipt / verification | Actual AUDIT-V2 record; no transaction/block. Verification returned **Unavailable**, not verified/anchored success |
| External / applicant screening access | **403 / 403** through actual ordinary sessions |

Both constructed workflow traces were flagged. They were not changed to force a normal result. Scores are not probabilities, corruption findings or causal explanations. Decision justifications are not consumed by this model. Separate simulated DS Final Notification remains distinct from Commissioner approval.

The demonstration used assigned synthetic uploaded case **SL-LMS-2026-745967**, `c7ce679c-3559-4eac-b531-4f885a197bf1`. It did not replace its real OCR run, reviewed facts, snapshot, completeness or applicant ownership. No fact review, snapshot publication, application decision, authority grant, completed execution, complaint verification or handoff was automatically recorded. Synthetic contexts and individual engine evaluations remain explicitly Development research inputs. This is not real-case clearance or production authentication acceptance.

Evidence: [engine/ML UI responses](C4_POLISH_UI_EVIDENCE.json), [screening, early engines, integrity and denials](C4_POLISH_SCREENING_EVIDENCE.json), [final model/transport UI checks](C4_POLISH_FINAL_UI_EVIDENCE.json), [classifier refresh persistence and health](C4_POLISH_PERSISTENCE_EVIDENCE.json). Normal browser clicks/forms were used; no browser token editing or C4 test double supplied these engine/model outputs.

## Reproducible evaluation evidence

[Generated report](../../../output/evaluation/c4-pp1-20261020/reproduced-evaluation.json), [read-only reproduction runner](../../../output/evaluation/c4-pp1-20261020/reproduce.py), [limitations and runtime details](../../../output/evaluation/c4-pp1-20261020/README.md).

- Classifier: newly reproduced scoring of **69** reviewed prospective synthetic records / **23** families, held out from the **425** training rows. Accuracy **94.20%**, macro F1 **0.940976**, weighted F1 **0.942952**. Per-class metrics/confusion matrix are exposed on Evaluation Evidence. The historical Experiment 6 result is separately labelled and matches this reproduction.
- Anomaly: newly reproduced scoring of the reserved **108** synthetic traces, **80** references / **28** controlled perturbations. TP19, FP6, TN74, FN9; precision **0.76**, recall **0.678571**, F1 **0.716981**, ROC AUC **0.941071**, average precision **0.870329**. Labels are generator-designated, not independent real-world adjudication. No “accuracy” or real-world corruption detection claim is made.
- No fitting, test-set tuning, threshold adjustment, model regeneration or split reassignment occurred. Existing experiment scripts that also train were not run. The isolated installation uses the existing scikit-learn **1.7.2** pin. Package versions and source/model hashes are recorded; the different original Python environment is disclosed. Successful loader/bundle validation, reproduction and actual inference establish this local compatibility without reserializing artifacts.

## Screen / endpoint / engine map

| Screen | Existing contract | Actual execution/store |
| --- | --- | --- |
| Overview / assigned selector / queue | C3 `GET /api/workflow-governance/cases`, case `/screening` | Durable assignments and exact currentness/prerequisites |
| Progressive screening | C3 `POST /api/workflow-governance/cases/{caseId}/screening` | Existing C4 consolidated Regulatory/Conflict/Early orchestration and stores |
| History | C4 `GET /screening/cases/{caseId}/history-consolidated`; C3 currentness read | Existing persisted consolidated results; newest is not automatically current |
| Development early test / history | C4 `POST /screen-early-governance`; `GET /screening/history/cases/{caseId}` | Actual deterministic seven-indicator engine / existing assessment store |
| Complaint analysis / history | C4 `POST /classify-complaint`; `GET /complaints/history/cases/{caseId}` | Actual configured v2 classifier / existing classifier assessment store |
| Post-workflow test | C4 `POST /predict-workflow-anomaly` | Existing frozen Isolation Forest; transient raw prediction |
| Operational post-workflow | C4 `GET /workflow-projections/cases/{caseId}`, `POST /workflow-anomalies/assess`, history route | Existing trusted projection and persisted model assessment store; missing here |
| Follow-up | C3 `GET /cases/{caseId}/post-workflow` | Existing completed-workflow/authority/DS-notification prerequisites |
| Integrity | C4 audit history, `/audits/{id}/receipt`, `/audits/{id}/verify` | Actual registered records and configured ledger integration |
| Evaluation Evidence | Dated generated report, no invented backend endpoint | Read-only scoring of validated frozen artifacts and existing labelled splits |

C4 endpoint prefixes are `/api/governance-intelligence`; C3 case prefixes are `/api/workflow-governance`. The Development panel rechecks server synthetic designation and active assignment before every write. Stronger consolidated dispatcher authority is preserved. **Legacy C4 routes still need server-side owner guards; frontend guards do not secure direct HTTP callers.** See the exact [unapplied proposals](C4_PP1_UNAPPLIED_BACKEND_PROPOSALS.md).

## Input-to-live replacement guide

Keep the existing `C4-INDEPENDENT-INPUTS-1` adapter contract documented in [the original handover](C4_FRONTEND_HANDOVER.md#fixture-to-live-replacement-contract). Replace the complete simulated snapshot with the exact authorized C3 published snapshot, and simulated parcel/coverage/policy with trusted versioned owner sources. Never insert fixture entries into OCR, infer absent complaints from an empty collection, or treat input UUIDs as grants. Actual ProposalVersion remains the document-version identity.

For post-workflow use the whole trusted C3 trace/projection with execution/revision/fingerprint and its separate source-backed DS notification. The raw endpoint does not import or persist a workflow. For complaint evidence, a classifier suggestion must pass the missing authorized review/evidence contract before becoming trusted screening evidence. Evaluation reports are not operational case findings. The ordinary uploaded case remains gated by its real inputs when Development context is not explicitly selected.

## Status and safe handover

| Status | Scope |
| --- | --- |
| **Working** | Polished scoped primary C4 UI/sign-in; consolidated stored screening/refresh/history; actual standalone early engine; restored v2 classifier and frozen anomaly inference; classifier persistence; reproducible synthetic evaluation page/reports; truthful integrity/readiness states |
| **Runtime blocked** | Successful ledger anchoring/verification. Actual verification says unreachable; documented Besu Docker daemon is not running (`docker info` failed after permitted host inspection). No reset, volume deletion, invented deployment address or fake transaction was substituted. |
| **Backend contract missing** | Operational complaint registration/assigned verification/resolution; general trusted custom projection ingest; unrestricted independent consolidated fixture dispatch (strong dispatcher authority is retained); finalized C3-manifest/audit registration link; per-indicator details in consolidated early summary; explicit server cache-reuse disposition |
| **Backend security owner work** | Case/role/institution guards for legacy C4 endpoints before operational handover; preserve current protected consolidated routes. Upstream ML unavailable/timeouts also need typed 503/504 mapping. |

[Startup and 10–15 minute walkthrough](C4_PP1_WALKTHROUGH.md). Screenshots: [conflict checks](../../../output/screenshots/c4-pp1/progressive-conflict.png), [sign-in](../../../output/screenshots/c4-pp1/sign-in.png), [history drawer](../../../output/screenshots/c4-pp1/historical-drawer.png), [actual revised-trace model](../../../output/screenshots/c4-pp1/revised-workflow-actual-model.png), [evaluation](../../../output/screenshots/c4-pp1/evaluation.png). The screenshot directory also contains other primary screens and small-width captures, all local test/synthetic context.

Validation: frontend **96 passed**; production webpack build / TypeScript passed; focused existing C4 orchestration/authorization **29 passed**. Normal browser checks cover current/history separation, case switching plus refresh, actual models, persisted classifier reload, access denials, keyboard modal closure, real missing projection/integrity failures and an explicitly labelled 503 UI transport fixture. Whitespace checks passed. Backend boundary comparison: **1,278 source/configuration files, zero changed**; both model artifact hashes remain exactly pinned ([evidence](C4_POLISH_BOUNDARY_EVIDENCE.json)). No new C4 backend changelog entry is required for this frontend/runtime-only slice.

Changed frontend: `AssessmentReport*`, `EarlyIndicatorReport`, `ReportDialog`, `ComplaintAnalysis`, `IndependentGovernance*`, `ConsolidatedCaseScreening`, `GovernanceWorkspace*`, queue styling, route/sidebar entries, sign-in styling, presentation helpers/tests and the dated evaluation report. Tooling: `Start-C4Ml.ps1`, reproduction runner and reports. Existing accumulated work remains preserved. No commit, push, reset, deletion, schema change, production grant or retraining occurred.
