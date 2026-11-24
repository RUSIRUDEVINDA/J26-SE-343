# Regulatory current-workflow and Unicode correction - prepared and verified

2026-10-24, Asia/Colombo. All four requested fixes are implemented and verified in a separate frontend package. **Not activated.** Live Step 15 remains on port 3000. No ordinary business write, migration, grant, live service replacement, ML/PostgreSQL restart or Git stage/commit/push.

## Root cause and focused changes

The active workspace retained a previously loaded Regulatory report in React state while restoring a different Early selection. When the new selection had no Regulatory ID, the render guard only waited for a selected Regulatory ID when it was non-null; the old in-memory report could therefore be passed to the fresh Regulatory screen. Earlier source had no active-page latest-history lookup; this was a stale-state/render-identity gap, not permission to substitute the latest proposal report. Returning to the same exact saved chain legitimately restored its own closed findings, which must be distinguished from opening a new chain.

The workspace now clears loaded reports when restoring selection, and renders Regulatory data only when both its evaluation ID and persisted Early provenance match the exact active selection. The Regulatory component independently checks the Early binding. The screen is keyed by actor/Early/evaluation, and follow-up state is keyed by evaluation, preventing prior form state from surviving a workflow switch. A new Early result already clears Regulatory/Conflict selection; the new guard makes that transition safe before asynchronous reads finish. No history fallback is introduced.

Uncertain Regulatory submissions previously shared one actor-level pending key. The active workflow now saves/resumes pending submissions per actor and exact Early request. An older uncertain request for a different Early remains retained separately and cannot populate, block or replay into the fresh workflow. Exact matching legacy pending requests remain recoverable. The standalone assessment editor keeps its previous default pending-storage contract; unrelated flows were not redesigned.

The previous progressive UI was located in the retained Step 7b3 and Step 14 snapshots. Step 15 had moved that entire list into collapsed original-engine details. The main Regulatory result again uses the original `ProgressiveSteps`, `ProgressiveStep`, `RegulatoryFindingExplanation` and `ProgressiveSteps.module.css`: their source hashes match the Step 14 snapshot exactly. All 24 saved findings appear in the saved engine-returned order with the retained connected indicators, status colors, card spacing and expand/collapse interactions. Compliant findings are visible in this list and excluded from the actionable follow-up selector. This restores the retained progressive interface; no alternative layout or rule ordering was invented. Step 15's concise progression summary and collapsed exact technical rule/hold information remain.

Fresh evaluations query only their own follow-up stream and start with zero saved events. The new browser/database journey exercised fresh clarification, human review and no-text/no-evidence per-finding closure; only a successful saved event displays Verified & Closed. Original engine findings, unrelated streams, actor/time/revision/hash auditing and institutional/legal holds remain unchanged. Navigation/refresh retrieves the exact selected evaluation rather than running another one.

## Unicode root cause and correction

A reversible UTF-8/Windows-1252 double-encoding chain was present in literal text in `KiwulaAssessmentWorkspace.tsx`, including middle dots, ellipses and continuation arrows. The browser/build correctly emitted those already-corrupted literals. The source was repaired through verified reversible byte decoding and saved as UTF-8. The corrected button is **Continue to Conflict Detection →**.

An audit of all frontend TS/TSX/CSS/JSON, including evidence JSON, found no remaining reversible mojibake sequences. Evidence/rule payloads were inspected without editing them. The only source file needing confirmed Unicode repairs was the workspace. A source scan regression rejects common corrupted/replacement sequences. Actual rendered desktop/mobile browser text was checked for corruption and the correct arrow. No valid Sinhala/Tamil text, backend rule definition or historical database string was rewritten.

## Exact files changed

Relative to `StateLandGovernance.Web/`:

- `src/modules/component-04/components/KiwulaAssessmentWorkspace.tsx`: reset restored state, guard exact active Regulatory identity, key component state, repair confirmed UTF-8 literal corruption.
- `src/modules/component-04/components/RegulatoryScreening.tsx`: restore retained main progressive 24-rule rendering, independently guard Early binding, use workflow-scoped pending requests, retain collapsed technical details.
- `src/modules/component-04/services/currentEarlySelection.ts`: exact Early/evaluation rendering predicate; Step 14 validated actor-scoped context remains intact.
- `src/modules/component-04/services/regulatorySubmission.ts`: workflow-scoped pending keys with safe legacy recovery and unchanged standalone default behavior.
- `tests/component-04-step16.test.ts` (new): seven regression tests for stale/fresh rendering, exact provenance, progressive order, pending actor/workflow isolation, uncertain legacy recovery and Unicode.

Also this report and temporary scripts, separate package/build snapshot, SHA256 manifest, screenshots, browser results and logs under `.tmp/c4-step16-*` and `.tmp/c4-step16-regulatory/`. No Early Governance/attachment implementation, product API, schema, ML, complaints, C1/C2/C3 or blockchain source was changed. Existing unrelated working-tree changes were retained.

## Actual verification

| Check | Final result |
| --- | --- |
| Full frontend suite | 286 passed, 0 failed, 0 skipped; seven new regressions |
| TypeScript | Passed |
| Focused lint | 0 errors; one existing cleanup-ref warning in RegulatoryScreening |
| Production build | Passed, Next.js 16.3.5 webpack, 37 static pages; separate snapshot |
| Selected governance suite | 959 passed, 0 failed, 0 skipped |
| API tests | 40 passed, 0 failed, 0 skipped |
| Architecture tests | 7 passed, 0 failed, 0 skipped |
| Existing isolated investigation/sequence regression | 89 checks passed |
| Existing isolated follow-up/closure regression | 114 checks passed |
| New frontend -> real isolated PostgreSQL browser journey | 7 grouped checks passed; no page errors |
| Read-only preview -> actual ordinary API | 20 checks passed; business-write attempts/blocks 0; no page errors |
| Unicode/source/design audits | Passed; retained progressive components/CSS byte-identical to Step 14; no remaining reversible mojibake |

Governance selection excludes PostgresIntegration classes, Phase3APersistenceFoundation, Phase3BPostgresAndSmoke and BlockchainRealEndToEndLive; excluded tests are not counted as passes. Persistence proofs use only the existing guarded `127.0.0.1:57657/c4_step3_restore_20261024` isolated database. No ordinary PostgreSQL query or write was used in this task. The smoke test uses its own temporary test process with the existing isolated ML runtime; the ordinary ML listener is not restarted.

The new browser integration creates two synthetic finalized Early screenings under the isolated contract's permanent proposal ID, runs the actual unchanged 24-rule engine for each, and verifies zero initial follow-ups, distinct evaluation IDs and preserved historical events. It records clarification and human review, closes only one finding without remarks/evidence, reloads the saved closure, and compares original finding JSON unchanged. The second Early chain never fetches the first evaluation or its follow-ups. Sidebar Early/Regulatory restoration and legal progression denial are exercised against the real guarded API/database host. No ordinary demo record is used for these writes.

The separate read-only preview signs in through the existing local flow against the actual API, checks 24 current cards in the exact saved finding order, expands/restores navigation, validates stored disposition counts against that evaluation's actual history, tests stale IDs and actor isolation, fresh Start Screening, attachment selection/warnings and separate navbar History. It does not save an ordinary assessment, evaluation or follow-up. Its current evaluation happened to have zero saved closures; the harness compares actual current history rather than assuming previous closures should appear.

Screenshots: `browser-isolated/regulatory-24-rules.png`, `browser-isolated/regulatory-mobile.png`, and `browser-preview/{fresh-early,current-early,closure-unsaved,history}.png` under the prepared release. The mobile progressive screenshot was visually inspected. The original design recovery is evidenced by `source-and-unicode-evidence.json`; exact rendered order is checked against the actual API findings array.

Initial minimal test fixtures lacked applicability and the additional storage test initially omitted the global sessionStorage binding; final fixtures/tests were corrected and passed. Browser harness edits initially had duplicate local variable names and an ANSI-encoded expected-arrow literal; the UTF-8 harness was corrected, while the rendered application already had the proper arrow. Initial failures are not counted as passes. No product behavior was altered for harness assumptions. Full repository lint was not repeated; prior unrelated issues remain outside scope.

## Release and activation handoff

Prepared frontend package: `.tmp/c4-step16-regulatory/web`.
Expected BUILD_ID: **vUoBXkxli3IRWEvDsD-uh**.
Browser assets target **http://localhost:5264** and contain no test fixture endpoint 58118. Package manifest/source checks passed. Preview used temporary port 3017; the preview and isolated proof host were stopped after verification.

Live services remain Step 15 frontend PID **28724**, build **xnViROYZlEEuJ2PkpGBks** on port 3000; API PID **37484**, Healthy/Live on 5264; ML PID **27388**, ready v2 on 8104; PostgreSQL PID **7024** on 5432. No live listener was replaced.

Activation requires separate owner approval to recheck the retained manifest/source/PIDs and replace only the frontend with this prepared package using `C4_INTERNAL_API_BASE_URL=http://localhost:5264`. No API deployment, migration or grant is required. Repeat read-only port-3000 acceptance afterward. Ordinary write testing remains separately authorized only; current write-path evidence is isolated.

Evidence: `.tmp/c4-step16-{frontend-qualified-final,typescript-qualified,lint-final,governance,api,architecture,investigation-proof,followup-proof}.log`; `.tmp/c4-step16-regulatory/{build.log,package-sha256.json,source-and-unicode-evidence.json,handoff.json,browser-isolated/result.json,browser-preview/initial.json}` and screenshots. Temporary scripts are retained for review.

**STOP after preparation/verification. No stage, commit or push. Live activation awaits separate approval.**
