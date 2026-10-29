# C4 controller-corrected deployment — activated and verified

2026-10-23, Asia/Colombo. **SUCCESS. One corrected API is healthy against `localhost:5432/state_land_governance_c4`, OID34144. Commissioner saved cross-creator access and independent complaint permissions pass actual authenticated HTTP verification. C4 is frozen for PP1; standalone C3 implementation can begin as a separate task. Browser visual/keyboard/responsive checks remain manual.**

## Exact correction and boundaries

Only `OfficerFlowsController` application source changed in this continuation. Its existing Execute path takes an operation-specific savedReportRead flag, set ONLY by saved officer-assessment GET and history. Authenticated Officer role, exact actor, current eligible Commissioner profile and the existing explicit C4AssessmentReadAll policy are required for a non-DS caller. No Commissioner institution/role bypass exists. Existing DS gate remains unchanged. Catalogue, private investigation reads/list, save/early/anomaly POST commands keep the DS requirement. Existing repositories continue Saved/creator filtering and live post-read checks. No creator/result/provenance substitution.

No migrations, schema/grants, database name, accounts/passwords, frozen models/features/thresholds/rules or peer modules changed. The earlier in-place rename remains final. No ordinary complaints/decisions/assignments or inference calls were submitted.

## Test evidence

- Focused backend **298 passed**, architecture **7 passed**, frontend **143 passed**, no failed/skipped tests.
- Final controller/repository boundary run **25 passed**, including two additional real EF-InMemory repository checks for foreign Pending/Unknown requests.23overlap the298-run; **300 distinct backend cases** passed across runs, not a claimed single300-test invocation.
- **69 isolated checks**, including authenticated loopback HTTP through the actual controller and existing stores against retained restored PostgreSQL data with transaction_read_only=on. Covers active explicit Commissioner GET/history, all saved early/anomaly requests, private drafts, denied POSTs, DS owner/other DS, unrelated Officer, applicant/anonymous, missing/revoked/expired/malformed/unavailable grants, inactive profile, institution-only denial and restored access. Test-only loopback authentication is not ordinary JWT proof; actual ordinary login/HTTP below supplies that evidence. No write/model handler invoked.
- Release publish succeeded,88files hashed. Focused whitespace check passed. No whole production frontend build or TypeScript success is claimed; the three documented unrelated C1 TypeScript errors and existing NuGet/unrelated warnings remain. The Windows evidence collector initially needed CRLF normalization; only that helper was corrected, tests/package unchanged.

## Immediate preflight and actual deployment

Exact renamed target/OID,38tables,19migrations, normalized schema, owner/ACL/table/column grants/settings/sequences and every current row fingerprint match the fresh rename backup. Peer/account fingerprints remain exact. The archive is readable/listable and SHA256 reverified; no new backup/restore was needed because data did not change. Existing complaint/report permission files and private candidate match exact reviewed bytes. Frozen artifact hashes are unchanged. No automatic startup migration call remains; compiled startup boundary checks pass, and startup/verification logs contain no SQL write/migration activity.

Verified no API listener existed before startup. New launcher `StateLandGovernance/tools/development/Start-C4ControllerCorrectedApprovedApi.ps1` validates the NEW package and exact renamed target/private configuration/independent permission files, peer protection and disabled worker/sandboxes; ValidateOnly passed. It started exactly one API **PID18396/5264**. Actual process environment was read back and matches the approved candidate, including Database=state_land_governance_c4. Existing frontend **PID16692/3000**, ML **PID36612/8104** reused and healthy (MLready/modelv2). PIDs are observations, not permanent identifiers. Isolated Docker container stopped successfully, data retained.

New package `.tmp/central-controller-corrected-api`:
- API DLL SHA256 `65cdff6905f89b39b6184e55ca7a70b8a8b9bff059042adf59ede386d690cc05`.
- C4 Presentation DLL SHA256 `7afc459ea1de41d0d419cdb9dc503a95cdbf405deb94db3d83b6c33cde4d4c6a`.
- Exact source/package/launcher/config manifests are versioned here; previous stopped package/manifests remain untouched. SOURCE_SHA256.json records the pre-start source/test set; FINAL_SOURCE_SHA256.json additionally records the two later test-only cases. Runtime application source/package did not change after publish.

## Ordinary authenticated verification

**308 checks passed;33saved reports** retrieved with actual original-owner and existing Commissioner sessions, across regulatory, Kiwula conflict/early, original workflow and officer-assessment GET/history families. Outcome grouping: `{'regulatory': 14, 'conflict': 1, 'early': 8, 'anomaly': 10}`. Each Commissioner body hash equals its original-owner body, preserving exact result, provenance and creators; repeat GETs return identical bodies. Full33IDs/API paths/body hashes/frontend URLs: SAVED_REPORT_URLS.json. The previously denied officer request29f53e56-9e9a-4e04-9ed3-350d1a7518d3 now succeeds.

Actual existing Commissioner login succeeded without credentials changes; JWT/live sessions and current account eligibility apply. Anonymous/foreign-applicant/other-DS saved report access is denied; private raw investigations remain denied. Central capabilities expose the existing Read/Assign/Review and nine operations, four queues and genuine saved complaints/classifications. Applicant owner/public-only history and unassigned DS404/empty inbox protections pass; no case-reviewer fallback. Both handling paths remain their existing isolated end-to-end evidence; no ordinary lifecycle was fabricated for deployment.

Controlled server-file report revocation immediately denies the same live Commissioner's GETs in EVERY saved route family and officer history, while independent complaint capabilities remain. Complaint revocation denies complaint capability without removing report access. Both exact approved original byte sequences restored; subsequent reads succeed. No restart needed. No password/token/connection value is disclosed in evidence.

All nine C4 frontend section routes returned200. This is HTTP readiness, not authenticated browser rendering. Browser automation was unavailable; manual checks below remain. No new research/complaint/ML records or ordinary event history were created by verification.

## Database and archive preservation

Current database `state_land_governance_c4`, original/final OID **34144**; ownerpostgres.38user-table fingerprints,19migrations, schemas/ACLs/settings/sequences and peer/account tables match before and after startup/verification. No rename or SQL/grant replay in this continuation. Exact proof FINAL_PRESERVATION_AND_PROCESS.json.

Fresh verified archive retained:
`D:\AI-Driven-Digital-Lease-Governance-for-State-Land-Management\AI-Driven-Digital-Lease-Governance-for-State-Land-Management\.tmp\c4-rename-backup-20261023_0840\c4_handover_local.dump`

Bytes334801; UTC last write2026-10-23T08:37:07.728148+00:00; SHA256 **`e702a55f70fe1e22059b65f9672cb98dea2a380b89e9aba10c33de54400d530f`**. Restore proof to NEW isolated databasec4_rename_restore_20261023_0840/57657 remains valid; original data still matches. Historical archive name truthfully records its pre-rename origin. No archive overwritten or ordinary restore performed.

## Current startup and recovery

Use only the new controller-corrected launcher/package. Historical renamed-target launcher points to the known DS-only controller defect; old pre-rename launchers target the wrong name. Reuse healthy listeners. Manual guide includes exact commands. Normal sign-in again is required after an API restart; persistent authorization does not make30-minute sessions permanent.

If a later mandatory failure occurs, stop only the verified corrected API, retain renamed database/history and exact evidence, and inspect current state. Do not start incompatible old binaries, rename back automatically, restore over ordinary storage, replay migrations/grants or weaken read protections. Live permission revocation uses the existing private server documents independently; no account/password change required. A new failure is not permission for an unreviewed repair.

See [manual walkthrough](MANUAL_WALKTHROUGH.md), AUTHENTICATED_RUNTIME_EVIDENCE.json, ISOLATED_AUTHENTICATED_HTTP_EVIDENCE.json, FINAL_TEST_RESULTS.json, FINAL_PRESERVATION_AND_PROCESS.json and all SHA256 manifests. No commit/push, C1/C2/C3 change, engine/model/rule change, worker/blockchain/Groq or new feature development occurred.
