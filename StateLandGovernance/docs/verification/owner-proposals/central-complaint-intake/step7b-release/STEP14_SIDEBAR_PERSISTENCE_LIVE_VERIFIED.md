# Step 14 - active workflow sidebar persistence fixed and live verified

2026-10-24, Asia/Colombo. The specific Step 13 sidebar-restoration failure is resolved in the running local frontend. No backend or database change was required.

## Cause and change

The workspace restored explicit deep-link IDs into page state but only the Save/Run callback path wrote actor-scoped session storage. Opening an existing saved chain therefore left the active-workflow key absent. Query-free sidebar navigation had nothing to restore.

The selected-workflow reader now loads the exact authorized Early and Regulatory reports and the referenced investigation. Before storing selection it verifies the saved Early request ID, kind, saved state, creator, investigation identity, proposal, assessment identity and Regulatory ownership/proposal/precise Early provenance. It stores the proposal ID, investigation ID, Early assessment ID, Early request ID, Regulatory evaluation ID and existing Conflict selection under the current actor's key. These are saved identities, never an arbitrary latest report.

Failed reads or invalid/stale bindings clear only that actor's local workflow selection and leave the selected read in an explicit error state. No historical substitution occurs. Effect cleanup and the existing session epoch prevent a late response from establishing a selection after navigation or an account change. Sequence/progression reads remain independent and do not determine which historical report is selected.

Start Screening still explicitly clears the actor's active workflow and draft selection before opening seven unselected concerns. History-only routes do not establish active-workflow context. No investigation, assessment or evaluation is created by navigation. Existing saved findings, closures and history remain unchanged.

## Changed files

Product changes, relative to StateLandGovernance.Web:

- `src/modules/component-04/components/KiwulaAssessmentWorkspace.tsx`: establish context through guarded authorized saved-workflow reads; remove the separate unvalidated Regulatory-result assignment from the sequence reader.
- `src/modules/component-04/services/currentEarlySelection.ts`: retain the three additional saved identities and validate the exact actor/proposal/investigation/Early/Regulatory chain before persistence.
- `tests/component-04-step14.test.ts`: five regressions for deep-link/sidebar/refresh identity, fresh intent, actor isolation, invalid/stale bindings and malformed local state.

Also added this report and temporary build/browser/activation scripts, package snapshot, source diff, manifests, screenshots and logs under `.tmp/c4-step14-local` and `.tmp/c4-step14-*`. Comparison against the activated Step 13 package confirms exactly the two product source files above changed. Existing unrelated working-tree content was retained.

## Verification

| Check | Actual result |
| --- | --- |
| Full frontend suite | 271 passed, 0 failed, 0 skipped; five new Step 14 tests |
| TypeScript | Passed |
| Focused ESLint | Passed, no diagnostics |
| Architecture suite | 7 passed, 0 failed, 0 skipped |
| Separate Next.js 16.3.5 webpack production build | Passed; 37 static pages |
| Packaged source and artifact hashes | Passed before preview and again before replacement |
| Browser endpoint assets | localhost:5264 present; fixture port 58118 absent |
| Preview against real API | 14 read-only acceptance checks passed |
| Actual localhost:3000 against real API | 14 read-only acceptance checks passed, normal browser security |

The live journey opened an existing owned saved chain by explicit URL, verified its current result, navigated sidebar Early -> Regulatory -> Early, and refreshed without losing the exact saved identity. It also checked in-workflow Back/Continue and explicit-URL refresh, separate navbar History, rejected a nonexistent Early ID and stale Regulatory ID without report substitution, deliberately started a fresh empty investigation after an existing chain was active, and switched to another authenticated officer in the same tab with the DS storage key still present to verify actor isolation. No old toolbar, previous assessments or duplicate verification form reappeared.

The browser intercepted all requests: only GET/HEAD/OPTIONS plus sign-in and the authentication-session bridge were allowed. Zero business-write requests were attempted or blocked; zero page runtime errors occurred. Existing ordinary reports and follow-up events were viewed only. This task ran no database command, migration, grant, ordinary write test or ML inference.

The preview's nonstandard origin initially failed authentication-origin checks. Only the temporary browser harness used relaxed browser origin enforcement and the authentication bridge's actual local server origin for preview testing. The final port 3000 journey used normal browser security and unmodified application authentication. Other initial harness assumptions about implicit labels, fresh-form loading/default values and an unresolved first finding were corrected; these attempts are not counted as passes. No application change was made to accommodate those harness issues.

## Running release

| Surface | Actual state |
| --- | --- |
| Frontend | PID 28244, port 3000, `.tmp/c4-step14-local/web` |
| Served BUILD_ID | `2Bxxi3RSI_TErOeHjBq04` |
| API | Original PID 37484, port 5264, Healthy, Live; not restarted |
| ML | Original PID 27388, port 8104, ready, model_version v2; not restarted |
| Old frontend | PID 19524 stopped only after successful build, manifest and real-API preview checks |
| Temporary preview | PID 15700, port 3014, stopped during frontend replacement |

The old release remained on port 3000 throughout building and preview verification. Only the identified frontend was replaced. PostgreSQL was not accessed or restarted. Step 13's 25-migration state was not changed or replayed. No source changes to other C4 features, ML, rule definitions, peer components or blockchain. No Git staging, commit or push.

Evidence: `.tmp/c4-step14-{frontend-final,typescript,lint,architecture}.log`; `.tmp/c4-step14-local/build.log`, `package-sha256.json`, `source.diff`, `activation.json`, `live-pid.json`, `browser-preview/initial.json`, `browser-live/initial.json` and browser screenshots. The previously documented Step 13 live sidebar limitation is superseded by these Step 14 live checks.
