# Step 15 - pending attachments and current workflow UI verified; activation pending

2026-10-24, Asia/Colombo. All five requested issue groups were addressed and verified. **Corrected frontend prepared only; Step 14 remains live.** No ordinary business-record write, migration, grant, service replacement, ML/PostgreSQL restart, Git stage, commit or push.

## Root causes and fixes

### 1. Attachments before first Save

The original input was disabled until a saved investigation ID and writable evidence view existed. File selection immediately uploaded through a separate action, while Save Draft saved only findings.

The form now accepts multiple pending supported files for each concern before the first save. Each concern lists its selected filenames and sizes with Review (download original selected bytes) and Remove controls; replacement is Remove followed by choosing the replacement. Identical selections for the same concern are deduplicated by filename, content type, size and SHA256. The inputs and lists remain compact across all seven cards. PNG/JPEG/PDF signature, UTF-8 text, safe filename and 1-byte through 5-MiB limits are checked before queuing; existing authoritative server validation remains unchanged.

One Save Investigation Draft action freezes the exact seven findings and file identities, creates/replays the investigation through its existing endpoint, then coordinates uploads through existing attachment endpoints. Each file retains its own submission key. The investigation ID is checkpointed immediately; subsequent failures/retries read its current evidence and skip exact files already committed, including an upload whose response was lost. A final read must confirm every intended attachment before complete success is reported. No Issue Found still needs no attachment. Explicit finalization remains separate.

Incomplete submissions retain the frozen draft key, payload, file keys and descriptors in actor-scoped session storage. Findings/removals are locked while confirmation is pending; the same Save is deliberately retried. Pending content remains in memory and triggers navigation/unload warnings. A reload cannot preserve browser File objects: the form explains that limitation, restores descriptors and findings, reconciles saved files, and requires exact original-file reselection for any missing files. It never claims descriptors alone are saved attachments. A deliberate confirmed fresh start discards this local selection; it does not delete saved database records.

### 2. Disabled Regulatory action

Read-only reproduction against the actual Step 14 UI/API inspected evaluation `b60cf15c-a1f5-47db-8721-311e6f6194dd`, bound Early request `41abccd6-6db3-458c-aa06-a357d73b5c4f`. All ten actionable findings already had terminal FindingVerifiedClosed events. `canWrite` was true. Individual controls were disabled because a second closure is prohibited, Run was disabled because this evaluation was already saved, and Continue was disabled because `canProceedToConflict` was false with ten saved progression holds. This was not missing closure remarks/evidence or a rule whitelist failure.

Selecting an already-closed finding now selects its closure operation and labels the disabled submission **Already Verified & Closed**, with the exact terminal explanation. Saved Run explains duplicate-evaluation prevention. Progression is explained separately from individual disposition. Eligible unresolved InsufficientInformation/RequiresHumanReview actions retain the unchanged backend eligibility, ownership/revision and replay rules: no mandatory remarks, references, evidence, second verification form or hardcoded rule/category restriction. No disabled attribute was blindly removed. Both eligible statuses were exercised in browser control tests, and arbitrary-rule/version, durable closure and legal/blocking holds remain covered by the 114-check PostgreSQL proof.

### 3. Confusing Unverified emphasis

Original engine evidence states were immutable but remained visually prominent in the original finding explanation list, ahead of officer dispositions. The current Regulatory view now places follow-ups, count of saved Verified & Closed findings and individual current officer statuses before the original engine explanation. Each concern retains its original outcome separately, actor/time/revision and audit history. Original detailed engine results/evidence/recommendations remain under collapsed Technical Details. Independent/statutory obligations remain explicitly separate; closure never means lease approval or external clearance. Only confirmed saved events render Verified & Closed, and other streams retain their own status.

### 4. Repeated raw NPD progression lists

KiwulaAssessmentWorkspace rendered the backend sequence holds verbatim above every active stage, including Early and Regulatory. That produced the unrelated wall of repeated rule messages.

A concise stage-specific progression summary replaces that main list. Exact backend rule codes and hold reasons remain in collapsed Technical Details. Regulatory finding cards show a plain-language required action for their own saved requirement, with exact original wording and recommendations under their own Technical Details. The backend rules, findings and holds are not modified or suppressed. Early primarily shows its seven saved officer findings, remarks, attachments, finalization and outcome. Regulatory primarily shows current follow-ups/dispositions; original detailed machine results are available on demand. Original audit history remains readable.

### 5. Preserve previous workflow behavior

The Step 14 validated actor/proposal/investigation/Early/Regulatory active-context logic is unchanged. Read-only preview verified explicit deep links, query-free sidebar Early -> Regulatory -> Early -> refresh, in-workflow navigation, exact IDs, nonexistent/stale-ID rejection, actor isolation and fresh independent Start Screening. No history toolbar/previous-result selector was reintroduced. Separate navbar History remains. Existing Combined Findings provenance and disabled unsupported C3 handoff logic were not edited; the full frontend suite retains those regressions. Conflict capability limitations, complaints/anomaly code, frozen models, rule definitions and peer components are unchanged.

## Exact source files changed

Frontend, relative to `StateLandGovernance.Web/`:

- `src/modules/component-04/components/EarlyGovernanceStepOne.tsx`
- `src/modules/component-04/components/EarlyGovernanceStepOne.module.css`
- `src/modules/component-04/services/pendingInvestigationFiles.ts` (new)
- `src/modules/component-04/components/ProgressionSummary.tsx` (new)
- `src/modules/component-04/components/KiwulaAssessmentWorkspace.tsx`
- `src/modules/component-04/components/RegulatoryScreening.tsx`
- `src/modules/component-04/components/RegulatoryFollowups.tsx`
- `src/modules/component-04/components/RegulatoryFollowupHistory.tsx`
- `tests/component-04-step15.test.ts` (new; eight regressions)
- `tests/component-04-investigation-finalization.test.ts` (updates the obsolete save-before-attachment assertion)

Test infrastructure, relative to `StateLandGovernance/`:

- `tests/IntegrationTests/InvestigationEvidenceProof/Program.cs`: opt-in guarded temporary proof host for the frontend coordinator, restricted to the existing isolated Step 3 PostgreSQL target. No product API code or contract changed.

Also this report and temporary scripts, test logs, source/build snapshots, manifests, screenshots and browser evidence under `.tmp/c4-step15-*`, `.tmp/c4-step15-local/` and `.tmp/c4-step15-final/`. Unrelated working-tree content was preserved.

## Actual final verification

| Check | Actual result |
| --- | --- |
| Full frontend suite | 279 passed, 0 failed, 0 skipped; eight new Step 15 tests plus Step 14 regressions |
| TypeScript | Passed |
| Focused lint | 0 errors; one pre-existing cleanup-ref warning in RegulatoryScreening |
| Next.js 16.3.5 production build | Passed; 37 static pages; separate snapshot |
| Selected governance regression suite | 959 passed, 0 failed, 0 skipped |
| API unit suite | 40 passed, 0 failed, 0 skipped |
| Architecture suite | 7 passed, 0 failed, 0 skipped |
| Existing isolated investigation/sequence proof | 89 checks passed; ordinary writes 0; classifier/anomaly calls 0 |
| Existing isolated Regulatory follow-up proof | 114 checks passed; ordinary writes 0; inference calls 0 |
| New actual frontend save coordinator -> PostgreSQL | 4 checks passed; image/PDF byte retrieval, partial-response retry and duplicate prevention |
| Final prepared frontend browser -> isolated PostgreSQL | 7 checks passed; pre-save selection, leave warning, reload/reselection, lost response, retry, saved retrieval |
| Final prepared frontend read-only preview -> actual local API | 19 checks passed; ordinary business writes attempted/blocked 0; page errors 0 |
| Controlled browser closure responses | Both requested statuses saved with empty remarks/references, no upload form, reload retains closed state; original outcomes/unrelated findings preserved and progression still held |

The selected governance suite excludes PostgresIntegration classes, Phase3APersistenceFoundation, Phase3BPostgresAndSmoke and BlockchainRealEndToEndLive. Excluded tests are not counted as passes. PostgreSQL write paths use only the guarded `127.0.0.1:57657/c4_step3_restore_20261024` test database. Synthetic investigations use the existing contract's permanent proposal ID inside that isolated database only; no ordinary Kiwulakale/demo row was changed. The proof host uses synthetic test identities.

The controlled closure browser test stubs follow-up responses only and is not presented as real durability. Real durability, original engine immutability, unrelated streams, arbitrary rule IDs/versions/categories, authorization, stale/duplicate/concurrent actions and mandatory external/blocking holds are verified separately by the PostgreSQL proof. The actual-API preview views genuine saved closed findings and never saves ordinary follow-ups.

The final isolated browser journey selected PNG and PDF before Save, cancelled navigation to retain them, reloaded and received an explicit reselection warning, reselected the original bytes, saved once, simulated response loss after an actual PostgreSQL upload commit, retried the same save, and confirmed exactly one investigation/two attachments on reload. Saved desktop/mobile screenshots were captured; the mobile concern-card layout was visually inspected. The separate coordinator proof downloads both original byte buffers and verifies exact equality.

Initial tests exposed missing optional metadata in minimal rendering fixtures and one JSX apostrophe lint error; corrected final checks passed. The isolated test host initially lacked a hosting extension import; corrected build passed. A first synthetic proposal ID was rejected by the unchanged permanent-proposal contract; the corrected test used that ID only inside isolation. Initial governance smoke attempts used Python runtimes without uvicorn; the final suite passed with the existing isolated `.tmp/c4-ml-venv` runtime, launching its own temporary smoke process without restarting the ordinary ML service. Offline NuGet vulnerability-feed warnings did not prevent test/build success. Preview browser origin accommodations apply only to temporary nonstandard preview ports; no application authentication policy changed. Harness timing/label/terminal-finding assumptions were corrected; initial failures are not counted as passes. Full repository lint was not repeated; existing unrelated failures remain outside this scope.

## Prepared release and actual running services

Final prepared package: `.tmp/c4-step15-final/web`, BUILD_ID **xnViROYZlEEuJ2PkpGBks**. Packaged source equals current source; SHA256 manifest is retained. Browser assets use localhost:5264 and contain no fixture port 58118. Preview was served on temporary port 3016 against the actual API; the earlier intermediate preview used 3015. Both previews and the isolated test host have been stopped.

| Surface | Actual handoff state |
| --- | --- |
| Frontend 3000 | Original Step 14 PID 28244; served build `2Bxxi3RSI_TErOeHjBq04` |
| API 5264 | Original PID 37484; Healthy, Live; not restarted |
| ML 8104 | Original PID 27388; ready, model_version v2; not restarted |
| PostgreSQL 5432 | Original PID 7024; not restarted or migrated |
| Corrected Step 15 UI | Prepared/preview-verified only; not activated on 3000 |

No API package update, migration or grant is needed for these frontend fixes. After separate owner approval, recheck source/package manifest, live PID/port identities and prepared BUILD_ID; replace only the identified frontend with the final package using `C4_INTERNAL_API_BASE_URL=http://localhost:5264`, and repeat read-only port-3000 acceptance. Preserve the old package for recovery. Ordinary write testing still requires its own approval and controlled synthetic records. No new backup or ordinary migration is proposed.

Limitations: draft creation and file upload are sequential existing API operations, not one database transaction; partial progress is reported honestly and reconciled through deliberate retry. Browser File objects cannot survive reload, so missing contents require exact reselection. Incomplete submissions lock payload editing/removal until reconciled to avoid changed-content replay. Officer closure does not discharge the ordinary API's legitimate progression holds; authoritative institutional review or supported reassessment may still be required. Step 15 has not been manually tested with ordinary write records or activated live.

Evidence: `.tmp/c4-step15-diagnosis/diagnosis.json`; `.tmp/c4-step15-{frontend-qualified,typescript-final,lint-qualified,governance-qualified,api,architecture,investigation-proof,followup-proof,proof-build-final}.log`; `.tmp/c4-step15-local/pending-upload-postgres.json`; `.tmp/c4-step15-final/{build.log,package-sha256.json,service-handoff.json,browser-preview/initial.json,browser-isolated/result.json,browser-closure-fixture/result.json}` and screenshots. Temporary helper scripts are retained for review.

**STOP: corrected release prepared and verified; live activation awaits the owner's separate approval, as explicitly required in Step 15.** No staging, commit or push.
