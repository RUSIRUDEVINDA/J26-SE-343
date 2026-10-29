# Step 5 — independent Post-Workflow Anomaly verified

2026-10-24, Asia/Colombo. **Step 5 complete. Test corrections only; production UI/backend unchanged. No ordinary deployment. Stop before Steps 6–7.**

## Independent access and behavior

Both DS and Commissioner navigation retain `/component-04/post-workflow`. The existing catch-all route `StateLandGovernance.Web/src/app/component-04/[...view]/page.tsx` renders PostWorkflowDemonstrations directly for that route, and saved legacy reports at `/component-04/post-workflow/reports/{id}`. No EarlyRequestId, RegulatoryEvaluationId, Step 3 readiness or Combined Findings completion is required. The sequential workspace contains no Anomaly stage; Steps 1–4 implementation files and behavior remain unchanged.

The default page renders NamedWorkflowPreview: the existing seven-event illustrative institutional timeline, source/limitations, OfficerAnomalyPanel and saved history. Run Anomaly Analysis is a deliberate action with server flags/permissions and Pending/Unknown protections. Mount/refresh/history retrieval never executes inference. Named-route runs and legacy demonstration scenario replay retain their separate established behavior; no rerun, retry, storage or scoring logic was changed.

The named officer APIs retain Officer role plus DS-only command/catalogue guards; saved GET/history can additionally use the independent live C4AssessmentReadAll Commissioner policy. Owner scope, Saved-only cross-creator reads, live revocation and post-read rechecks remain unchanged. Commissioner report permission does not grant named-route execution authority. The older Development-only post-workflow-demonstrations endpoints retain their existing Officer/ComplianceOfficer/LandOfficer and feature/write gates; this task does not silently change their established role scope or equate that legacy sandbox contract with the DS named-route command. Applicant/anonymous/foreign saved-report denials remain enforced server-side.

Execution authorization was checked with existing controller/handler tests and denied/gated HTTP requests. No genuine inference was run to verify a new score; actual saved model outputs were retrieved instead.

## Two frontend failures: cause and exact correction

1. `component-04-handover.test.ts` expected lowercase `workflow activity flagged` in the heading for both flag values. The intended existing UI renders uppercase **UNUSUAL WORKFLOW ACTIVITY FLAGGED** for true and **No unusual workflow activity flagged** for false. The corrected test checks each exact verdict, rejects the flagged advisory/data attribute for false, verifies advisory limitations and keeps all nine values/units.
2. `component-04-polish.test.ts` expected the old `Model and technical details` text. The current intended label is **Technical details**. The assertion now checks the actual details/summary structure, exact retained diagnostics and their absence from the main panel.

Assertions were strengthened rather than reduced: score and threshold numbers must remain outside the main presentation and inside initially collapsed technical details. Large flagged styling is verified through the actual `.flag` / `[data-flagged=true]` contracts, replacing an assertion about obsolete unused `.scores` styling. New tests cover true, false and absent saved flags, including a false flag with large numerical diagnostics to ensure the UI does not independently infer the verdict from score/threshold. These are synthetic in-memory presentation fixtures, not new model predictions or ordinary records.

Production MeasuredWorkflowFeatures, CSS, preview, page, navbar, stores, engines and permissions needed **no change**. Actual true results retain the prominent uppercase advisory and warning styling. False/unknown results do not display it. Scores, threshold, model/input/artifact identity and advisory note remain available in collapsed technical details; all nine measured features retain exact values and units.

## Tests and saved persistence evidence

| Gate | Fresh result |
|---|---|
| Focused frontend presentation/route/preview/history tests | 19 passed |
| Full frontend including Steps 1–4 | **233/233 passed**, zero skipped; both old failures resolved |
| Focused Post-Workflow/Anomaly handler/client/DI, officer and Commissioner policy backend regressions | **87 passed**, zero skipped |
| Dedicated architecture project | **7 passed** |
| Existing restored-copy repository and authenticated HTTP proof | **198 checks passed** |
| Focused lint | 0 errors / 0 warnings |
| TypeScript | Only the same three existing C1 errors; no whole production build success claim |

The 198-check proof reads existing saved reports from `c4_step3_restore_20261024`, loopback57657, under the restricted isolated runtime role with `default_transaction_read_only=on`. It covers original creator/result/provenance retrieval, saved Anomaly histories, DS-owner reads, independent DS/Commissioner preview, explicit Commissioner cross-creator saved reads, denied unrelated officers/applicants/anonymous users, immediate report grant revocation/expiry/ineligible identity and restoration of the exact isolated grant. Named-route Commissioner execution is denied; legacy execution is disabled in the proof before store/engine calls. Test-only authentication passes through actual ASP.NET authorization; this is not a fresh ordinary password/JWT login or graphical-browser claim.

All isolated row fingerprints, schema/history and grants match before/after: **zero database writes and zero model calls**. The proof's temporary grant document is an isolated permission fixture, not an ordinary/private permission change. Saved artifact/score/creator associations were read, never reconstructed or recomputed.

The first reused proof assumed grant revocation must make every list empty. The larger restored fixture includes legitimate Commissioner-owned reports; revocation correctly retained those. The proof was corrected to require every remaining row to be owned by the actor, and to select a genuinely foreign report for cross-creator revocation checks. It passed afterward. No application authorization was weakened or changed; a private failed log is retained.

## Diagnosis of the 13 Step 4 broad failures

The broad 978-test suite was **not** rerun with real inference. Its original 965 passes/13 failures remain historical; detailed classification is in BROAD_BACKEND_FAILURE_DIAGNOSIS.json.

- **11 configuration preconditions**: two Phase3B tests, two Early PostgreSQL tests, six complaint PostgreSQL cases and one Phase3A DI/model test require COMPONENT4_TEST_POSTGRES_CONNECTION and a dedicated component4_screening_test_ database. They failed before exercising configured persistence/engine behavior. No ordinary target was supplied and no inference orchestration was rerun to clear these failures.
- **1 Python environment precondition**: the real FastAPI smoke launcher uses `py -3.13` unless explicitly configured. Step 4 recorded exit112, No installed Python found, before inference. The model itself was not implicated. No Python installation, dependency upgrade or model service restart was attempted.
- **1 unresolved test-harness path error**: ProjectFiles_ShouldNotContainForbiddenPackageReferences was rerun alone and still fails at XDocument.Load(string) with UriFormatException, before forbidden-package assertions. A separate ignored .NET8 read-only diagnostic successfully parses all five exact project files via normal path and stream; the failing test-context URI/path cause remains unresolved. Neither its test code nor project dependencies were modified to hide the error.

No new C4 runtime regression was identified in the focused checks. This is not proof that the blocked broad integration tests would pass after configuration; they remain explicitly unverified. Dedicated architecture tests pass, but that additional project-file test still fails.

## Ordinary preservation and model integrity

Final SELECT-only verification: `localhost:5432/state_land_governance_c4`, original **OID34144, 41 tables, 21 migrations**. All original row fingerprints, durable schema/history/sequences/grants, existing private configuration/permission documents and published package hashes match the baseline. The baseline's vanished temporary-table grants remain explicitly excluded only from durable-object comparison. No new database table, migration, grant, ordinary fixture or deployment was introduced.

Frozen model SHA256 rechecks:

- Complaint v2: `63b37eb7175e74a8dcde82bf042fcf9dcbb4f0432fbe3b4677aac037007a20ae`, `StateLandGovernance/src/Modules/GovernanceIntelligence/ML/models/v2/governance_classifier_v2.joblib`.
- Anomaly v1: `59ed4a73d56d9f06798c4ebc4684a633afe72841c41ed0b3ba4e7bb658782b46`, trusted bundle resolution to `StateLandGovernance/src/Modules/GovernanceIntelligence/ML/anomaly_detection/runs/isolation_forest_baseline_v1/isolation_forest.joblib`.

No model, feature, threshold, inference client, regulatory rule, Conflict engine, C1/C2/C3 or blockchain code changed. All production frontend/backend and Steps 1–4 implementation hashes match the start-of-Step-5 snapshot. The existing complete archive checksum/readability was revalidated: `1b5f7ba0872f0cf59fe9f118c06cb3079f9c6a965120b568e31647e3f3032dd0`; no new backup/restore was needed for this read-only task.

Final listeners: native PostgreSQL5432/PID7024 only. API5264, portal3000 and ML8104 were already offline and remain untouched. The isolated PostgreSQL container was started for saved-read proof and stopped successfully afterward, retaining storage. No ordinary service start/stop/replacement. No stage, commit or push; Git index unchanged.

## Exact changed test files

Before/after SHA256 are in CHANGED_FILES.json:

- `StateLandGovernance.Web/tests/component-04-anomaly-verification.test.ts`
- `StateLandGovernance.Web/tests/component-04-handover.test.ts`
- `StateLandGovernance.Web/tests/component-04-polish.test.ts`
- `StateLandGovernance/tests/IntegrationTests/ReportReadProof/Program.cs`

Task documentation/evidence in this folder: STEP5_VERIFIED.md, CHANGED_FILES.json, TEST_RESULTS.json, BROAD_BACKEND_FAILURE_DIAGNOSIS.json, ISOLATED_SAVED_REPORT_HTTP.json, ISOLATED_PRESERVATION.json, FINAL_ORDINARY_PRESERVATION.json, SOURCE_PRESERVATION.json, SERVICE_STATUS.json and REVIEW_SHA256.json. Ignored diagnostics and logs are not staged.

## Remaining limitations and stop

No visual/keyboard/responsive browser observation or ordinary endpoint deployment is claimed. The three existing C1 TypeScript errors remain: RecommendationsView.tsx309 TS2345 and ParcelCatalog.tsx131/143 TS18048. The broader configuration failures and XML-path harness defect remain documented above. The ordinary package is unchanged; still-isolated Steps 1–4 are not deployed by this verification.

**Step 5 complete within scope. Stopped before Steps 6 and 7.**
