# C4 Step 7A — integrated verification and deployment readiness

2026-10-24, Asia/Colombo. **NOT READY for a complete production deployment: three existing C1 TypeScript errors block the frontend production build. No ordinary deployment was performed.** Standalone C4 integrated journeys pass in isolated services after the minimal corrections below. Stop at Step 7A; no further step or ordinary activation is authorized by this report.

## Confirmed defects corrected

1. Regulatory progression treated an actual `NotApplicable` / `NotApplicable` finding as an unsupported outcome. It now accepts that explicit pair only after the existing blocking and mandatory-review checks. Inconsistent applicability, unresolved information, adverse outcomes and outstanding required reviews remain held. No regulatory rule, engine or saved finding changed.
2. A Commissioner-authorized saved Early report disappeared when its page also requested the DS-only investigation draft. Supplemental draft retrieval now occurs only for the report creator. The saved result and independently authorized read-only evidence history render for the Commissioner without granting draft/write access. Other DS denial remains enforced by the primary API.
3. One invalid Windows-1252 byte in the Early component prevented frontend compilation. It is now a UTF-8 ellipsis; a strict C4 source-encoding regression test protects this boundary.
4. The project-reference architecture test used `XDocument.Load(string)`, which interpreted the Windows path as a URI. Opening the exact file as a stream fixes the harness while retaining every package-reference assertion.

Exact eight source/test paths and before/after SHA256 values: [CHANGED_FILES.json](CHANGED_FILES.json). Paths relative to repository root:

- `StateLandGovernance/src/Modules/GovernanceIntelligence/Application/OfficerFlows/OfficerSequence.cs`
- `StateLandGovernance/tests/UnitTests/GovernanceIntelligence/ArchitectureTests.cs`
- `StateLandGovernance/tests/UnitTests/GovernanceIntelligence/OfficerSequenceTests.cs`
- `StateLandGovernance.Web/src/modules/component-04/components/EarlyGovernanceInvestigation.tsx`
- `StateLandGovernance.Web/src/modules/component-04/components/OfficerSavedReports.tsx`
- `StateLandGovernance.Web/src/modules/component-04/services/officerFlows.ts`
- `StateLandGovernance.Web/tests/component-04-saved-report-draft-scope.test.ts`
- `StateLandGovernance.Web/tests/component-04-source-encoding.test.ts`

Additional task files are the evidence/report files in this directory and ignored isolated tooling/artifacts under `.tmp`. No new migration, permission, dependency or production-host startup change was introduced. The much larger existing dirty working tree is not this task's diff and was not staged.

## Tests and build

| Check | Actual result |
| --- | --- |
| Final configured broader C4 backend partition | 970 passed, 0 failed/skipped |
| Dedicated PostgreSQL partitions | Early 4, complaint 6, Phase3B 2 tests have passing evidence |
| Unique backend tests with passing evidence | 982 across partitioned runs |
| Focused progression tests | 14 passed, included in the broader count |
| Frontend full suite | 245 passed, 0 failed/skipped |
| Architecture project | 7 passed, 0 failed/skipped |
| Authenticated security/history/replay HTTP checks | 33 passed |
| Actual restricted-role UPDATE/DELETE/schema-DDL denial | 3 passed |
| Focused lint | 0 errors/warnings |
| TypeScript | Only the three existing C1 errors below |
| Production build | Webpack compilation succeeded in 8.8s; TypeScript failed; no successful full build |

The original 13 broad failures are accounted for: 11 required isolated PostgreSQL configuration, one required the supported Python executable, and one was the XML-path harness defect above. They now have passing configured/corrected evidence. A subsequent Early PostgreSQL class run exposed test ordering: one method attempts a backward migration after another method advances their shared fixture. The forward-only Down correctly refused. Three class methods passed, and the remaining method passed alone in a NEW database. This is partitioned passing evidence, not a claim that one unified command is green. No assertions were weakened or tests disabled. See [TEST_RESULTS.json](TEST_RESULTS.json).

Unchanged C1 blockers: `RecommendationsView.tsx:309` TS2345; `ParcelCatalog.tsx:131` and `:143` TS18048. C1 was not edited. The build used an isolated frontend copy; no ordinary frontend service was started or replaced.

## Genuine graphical browser verification

Headed Google Chrome through Playwright used the isolated browser origin `http://localhost:3157`, Next on 3158, normal API host on 5807 and ML on 8187. This is graphical execution, not SSR evidence. Secure screenshots and raw fictional fixture evidence remain in `.tmp/c4-step7a`; sanitized scenarios and screenshot hashes are in [BROWSER_VERIFICATION.json](BROWSER_VERIFICATION.json).

- DS: seven explicit concern selections; saved investigation; exact-concern upload; explicit server-validated finalization; authenticated download matching the persisted SHA256; actual Early screening; selected-ID navigation; unresolved Regulatory holds after reload.
- Manual review: responsible DS attachment and verification; saved history after reload. Independent Early rerun, separate current/previous evidence groups and same-key finalization replay additionally passed real HTTP checks.
- Regulatory: actual rule evaluation of explicit fictional structured facts, administrative No Follow-up Required, every applicable required human review, and authoritative progression after those persisted reviews. The earlier incomplete evaluation remains held and unchanged.
- Conflict and summary: existing capability-limitation screen; forward navigation without a new Conflict gate; exact Early/Regulatory chain after reload and browser back/forward; initially no Conflict selected. A separately saved actual existing decision comparison was explicitly selected and survived reload. Its historical nature and lack of parcel clearance remain disclosed. Send to Workflow Governance stays disabled; no C3 transmission exists.
- Anomaly: independent navbar access despite unfinished stages; genuine analysis and saved flagged advisory; numeric diagnostics inside Technical details; Commissioner saved-report read access.
- Complaints: applicant free-text address and optional unvalidated parcel reference; genuine classifier result; saved location after reload; Commissioner four queues/assignment; DS investigation/findings; central reasoned resolution; terminal DS scope removal; public-only applicant history.
- Privacy and presentation: applicant direct C4 redirect/navigation exclusion; cross-officer report denial without evidence filenames; account switching; desktop 1440px and mobile 390px layouts without document horizontal overflow; keyboard Tab reaching controls. Loading/error checks deliberately delayed/aborted an isolated request and are labeled simulations.

The exact Commissioner login targets `/component-04`, which the current Steps 1–6 routing intentionally resolves to `/component-04/assessments`; it does not open complaints automatically. Complaint Inbox opens `/component-04/central-complaints`. The earlier Overview-only expectation is superseded by that existing routing. The exact report policy remains restricted to actor `0ab669fe-d0ab-5695-ad55-10f6b3b5aab3`; only isolated synthetic account storage mirrored that identity, with a synthetic password. Ordinary accounts/credentials were not used or changed.

Limits: no comprehensive screen-reader/accessibility audit, touch-device testing or graphical ordinary deployment check. A genuine saved non-flagged underlying anomaly assessment exists in the restore (score 0.517010184975016), but it has no standalone officer/demo report association. Non-flagged presentation is covered by frontend regressions; no graphical authorized saved-report success is claimed for that orphan result. Both deliberately executed prepared workflow examples actually flagged; their outcomes were not forced.

## Real ML evidence

The existing verified Python environment `.tmp/c4-ml-venv/Scripts/python.exe` launched the unchanged service. Its port-8187 counters record exactly **one successful `/predict`** and **two successful `/workflow-anomaly/predict`** requests. GET, reload, evidence actions and summary retrieval did not invoke ML. Separate configured backend smoke tests launched their own isolated model processes; these counters do not purport to count those additional test calls.

| Actual isolated result | Returned value |
| --- | --- |
| Named route v2, anomaly model `workflow_anomaly_model_v1` | score 0.6704623651406526; threshold 0.5743688923115832; flagged true |
| Existing administrative example, same anomaly model | score 0.688338019923785; same threshold; flagged true |
| Complaint model v2 | Lease Revenue / Payment / Enforcement; exact four returned probabilities saved in evidence |

Model SHA256: complaint `63b37eb7175e74a8dcde82bf042fcf9dcbb4f0432fbe3b4677aac037007a20ae`; anomaly `59ed4a73d56d9f06798c4ebc4684a633afe72841c41ed0b3ba4e7bb658782b46`. Artifacts, extractor, nine features, thresholds and inference implementation remain unchanged. These are advisory outputs, not accuracy or proof of allegations. Actual isolated assessment IDs, provenance and outputs: [REAL_MODEL_HTTP_EVIDENCE.json](REAL_MODEL_HTTP_EVIDENCE.json). No ordinary inference occurred.

## Persistence and ordinary preservation

Native ordinary target remains **localhost:5432/state_land_governance_c4, OID34144, 41 tables, 21 migrations**. All original row fingerprints, normalized schema, migration/sequence metadata and durable grants match the baseline. Existing private configuration, independent permission documents, published ordinary package and protected sources/models remain unchanged. Historical temporary `pg_temp_38` grant disappearance is explicitly excluded only from durable ACL comparison; no persistent mismatch was ignored.

Verified archive: `.tmp/c4-step1-backup-20261024-finalization/state_land_governance_c4.dump`, 1,815,584 bytes, SHA256 `1b5f7ba0872f0cf59fe9f118c06cb3079f9c6a965120b568e31647e3f3032dd0`. It was readable/listable and restored into NEW isolated `c4_step7a_restore_20261024`; original 41-table schema/rows matched before applying the two approved prerequisite migrations. The resulting isolated schema is 42 tables/23 migrations. A second NEW comparison restore, `c4_step7a_preservation_20261024`, proves every original row-hash multiset remains present after fictional writes. Schema/history/grants/sequences remain equal to the isolated post-migration baseline. See [FINAL_ISOLATED_PRESERVATION.json](FINAL_ISOLATED_PRESERVATION.json) and [FINAL_ORDINARY_PRESERVATION.json](FINAL_ORDINARY_PRESERVATION.json).

Only verified isolated processes were stopped after testing; Docker test storage is retained. Native PostgreSQL remains PID7024. Ordinary ports 5264/3000/8104 were absent and remain absent; this task did not start, stop or replace them. [FINAL_SERVICE_STATUS.json](FINAL_SERVICE_STATUS.json).

## Packages and readiness decision

Compatible corrected API publish: `.tmp/c4-step7a-api-final`; 88 files in [PACKAGE_SHA256.json](PACKAGE_SHA256.json). API DLL SHA256 `1216288412b17854112f324e3b3ee05e438afad6ea8c11244aea90b392261201`. This exact package ran the normal isolated host and the corrected progression path. Startup migration architecture checks pass. [SOURCE_SHA256.json](SOURCE_SHA256.json) identifies the current source; prior manifests/packages are retained.

**NOT READY for full production deployment** because the frontend production build is blocked by C1. No remaining confirmed C4 functional failure is known in the exercised journeys. The legacy PostgreSQL test-order limitation, non-flagged graphical limitation and limited accessibility coverage remain explicit. The dirty repository is not a reproducible release commit. No package/launcher is authorized for ordinary activation by this report. Follow [DEPLOYMENT_PLAN.md](DEPLOYMENT_PLAN.md) only after separate approval and successful current gates.

**Stopped after Step 7A. No ordinary migration, grant, configuration change, deployment, fixture write, staging, commit or push.**
