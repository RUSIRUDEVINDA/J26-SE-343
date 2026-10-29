# Step 6 — final UI and complaint form improvements

2026-10-24, Asia/Colombo. **Implemented and isolated-tested. No ordinary deployment. Stopped after Step 6.** Step 7 was not started.

## Delivered and reused

- Current Step 1 investigation attachments are shown in compact, initially collapsed concern groups with saved-file counts and authorized Download buttons. Both investigation ID and concern must match before a file is rendered. Upload/finalization/required-evidence logic and authenticated download services remain unchanged.
- Previously loaded assessment attachments now produce a small neutral status: Saved attachments are listed below. The large upload-feedback panel is reserved for real upload, pending, failure or uncertain states. Loading history never claims a new successful upload.
- Previous assessment cards now say Previous assessment, include a short assessment identifier and link to their exact original saved report. Their attachments and verification entries are scoped to that request, never merged with current evidence. Original full IDs, timestamps, remarks, hashes and verification history remain available. The existing investigation selector now includes a short investigation ID alongside the date, and its old list is cleared during session reset. Independent reruns and immutable snapshots are preserved.
- DS and Commissioner handling cards already share ComplaintHandlingPanel → ComplaintHandlingHistory, including the same factual renderer and CSS. This was reused and verified rather than duplicated. Available operation/revision/time/status transition, public summary, reason, returned details, document references and technical actor/event fields remain consistently rendered. Role-specific actions and server-filtered episode/public histories are unchanged; the two roles do not acquire identical visibility where policy restricts it.
- Free-text address support was already implemented. RegisteredComplaints now renders its existing address field through a small exported ComplaintLandAddress component so its real form contract can be SSR-tested. The handler, capability negotiation, field validation and submission payload are unchanged. Optional parcel/case inputs remain optional. No duplicate address store, lookup or endpoint was added.
- Step 5 Anomaly UI/CSS/navbar/execution files are byte-for-byte unchanged. The uppercase flagged advisory and collapsed technical score/threshold presentation continue to pass their tests. Steps 1–5 regressions remain intact.

## Address contract and isolated proof

Existing applicant `GET /api/governance-intelligence/registered-complaints/intake-capabilities` advertises version c4-complaint-intake/2 with unverifiedLandLocation=true. The existing authenticated POST accepts optional LandAddress (up to2000 characters), CaseReference and ParcelId. It stores the address and any supplied references as unverified applicant assertions in the existing complaint payload, with trusted CaseId/ParcelId left null for new standalone intake. It never infers a parcel, ownership or application linkage and does not call the C1/C3 reference adapter. Existing trusted links on older saved rows are not rewritten. Legacy omitted-address hash/JSON compatibility remains unchanged.

The frontend displays the already implemented address field when the running backend advertises that contract; unknown parcel/case references do not require a lookup. Old packages without that capability retain their existing compatibility behavior; this task does not deploy/enable the new source on ordinary storage or claim old binaries gained address support. A future compatible package is required for ordinary use of this source contract.

**21 isolated actual controller/repository HTTP checks passed** on `c4_step3_restore_20261024`,127.0.0.1:57657, using the restricted runtime role. They cover:

- authenticated free-text address with no case/parcel;
- unknown optional parcel/application references saved unverified without blocking;
- exact original address/text/owner persistence, same-key replay and changed-address409;
- length/control-character rejection before persistence/classifier;
- owner list/detail, foreign applicant404, unassigned DS404, anonymous401 and Officer submission403;
- new-host reload retaining address/history/ownership;
- zero C1/C3 lookup calls.

Two fictional complaints were created only inside isolation. An intentionally unavailable test classifier adapter was called twice; the real classifier was never contacted. Both attempts honestly persist classification failure with no invented probabilities or predictions. Idempotent replay, validation rejection and GET/reload made no additional adapter calls. This proves intake preservation during classifier unavailability, not a new model-v2 inference result. Existing model-v2 saved outputs and all classification tables remained unchanged.

Isolated example complaint `0d126732-d367-4925-ae83-45afca02c63e` is a fictional fixture, not an ordinary URL or real allegation. No ordinary fixture was created.

## Evidence/history and regressions

**59 existing-fixture HTTP/storage checks passed** for exact Early/Regulatory chains, finalization, seven concerns, saved evidence/history, owner/foreign/applicant/Commissioner boundaries and readiness. Existing pre-screening and post-screening downloads returned bytes whose SHA256 equals the stored metadata. Other DS downloads were denied; existing Commissioner read-only downloads remained available. No evidence, verification, finalization or engine result was created or updated by this read/download proof.

Frontend tests additionally cover compact empty/populated lists, exclusion of wrong investigation/concern/request files, historical report links, neutral loaded feedback, preserved genuine-upload/uncertain feedback, free-text rendering, optional parcel payload and shared role-history factual fields without invented actions. Existing Steps 1–5 suites, including the repaired Anomaly assertions, pass unchanged.

| Gate | Result |
|---|---|
| Focused backend including officer, Regulatory, evidence, complaints, ownership/authorization and Anomaly regressions | **402 passed**,0failed/skipped |
| Dedicated architecture project | **7 passed** |
| Full frontend | **241 passed**,0failed/skipped |
| Isolated address persistence/HTTP | **21 passed** |
| Isolated saved evidence/download/sequence HTTP | **59 passed** |
| Focused lint | 0errors,2existing RegisteredComplaints hook warnings |
| TypeScript | Only3known C1 errors; no whole production build claim |

The three existing C1 errors remain RecommendationsView.tsx309 TS2345 and ParcelCatalog.tsx131/143 TS18048. Existing NuGet vulnerability-feed warnings remain. The broader Step 4/5 diagnostic configuration/XML-path failures were not rerun or represented as resolved; see the Step 5 diagnosis. Initial new test-harness errors (array Count versus Length and SSR maxLength attribute casing) were corrected and rerun successfully, without changing application behavior or weakening existing tests.

## Preservation evidence

Before/after isolated comparison preserves every pre-existing row. Only two new fictional registered-complaint rows were added; every other table fingerprint, schema/history and grant matched exactly. The database retains42tables/23migrations from the previously isolated Step 1/2 prerequisites; Step 6 adds no migration or grant. Test storage is retained and its container was stopped successfully.

Final SELECT-only ordinary check: `localhost:5432/state_land_governance_c4`, original **OID34144,41tables,21migrations**. All ordinary rows, durable schema/history/sequences/grants, original private configuration/permission files and published API package match the baseline. The baseline's vanished temporary-table grants remain explicitly excluded only from durable-object comparison; no durable privilege difference exists. The secure complete archive checksum/readability was rechecked: `1b5f7ba0872f0cf59fe9f118c06cb3079f9c6a965120b568e31647e3f3032dd0`.

Frozen complaint-v2 artifact SHA256 remains `63b37eb7175e74a8dcde82bf042fcf9dcbb4f0432fbe3b4677aac037007a20ae`; anomaly-v1 remains `59ed4a73d56d9f06798c4ebc4684a633afe72841c41ed0b3ba4e7bb658782b46`. No model loading/inference, feature, threshold, regulatory/Conflict engine, C1/C2/C3, blockchain, identity/password or production backend change occurred. No new schema, grants or ordinary write/deployment.

Final listeners: native PostgreSQL5432/PID7024 only. Ordinary API5264, portal3000 and ML8104 were already offline and remain untouched. Only the isolated PostgreSQL container was started/stopped for proof. No ordinary service restart. Git index remains unchanged; no staging, commit or push.

## Exact implementation and test delta

CHANGED_FILES.json records before/after SHA256 against the start-of-Step-6 working tree:

- `StateLandGovernance.Web/src/modules/component-04/components/AssessmentHistory.module.css`
- `StateLandGovernance.Web/src/modules/component-04/components/CurrentInvestigationAttachments.tsx`
- `StateLandGovernance.Web/src/modules/component-04/components/EarlyGovernanceStepOne.tsx`
- `StateLandGovernance.Web/src/modules/component-04/components/ManualEvidenceVerification.module.css`
- `StateLandGovernance.Web/src/modules/component-04/components/ManualEvidenceVerification.tsx`
- `StateLandGovernance.Web/src/modules/component-04/components/PreviousEarlyEvidence.tsx`
- `StateLandGovernance.Web/src/modules/component-04/components/RegisteredComplaints.tsx`
- `StateLandGovernance.Web/tests/component-04-step6-ui.test.ts`
- `StateLandGovernance/tests/IntegrationTests/InvestigationEvidenceProof/CombinedFindingsReadProof.cs`
- `StateLandGovernance/tests/IntegrationTests/InvestigationEvidenceProof/ComplaintLocationProof.cs`
- `StateLandGovernance/tests/IntegrationTests/InvestigationEvidenceProof/Program.cs`

Task documentation/evidence: STEP6_IMPLEMENTED_AND_VERIFIED.md, CHANGED_FILES.json, TEST_RESULTS.json, ISOLATED_COMPLAINT_ADDRESS_HTTP.json, ISOLATED_EVIDENCE_AND_SEQUENCE_HTTP.json, ISOLATED_PRESERVATION.json, FINAL_ORDINARY_PRESERVATION.json, SOURCE_AND_INDEX_PRESERVATION.json, SERVICE_STATUS.json and REVIEW_SHA256.json in this directory. Private baselines, helpers and logs stay in ignored storage.

## Remaining manual/deployment boundary

No graphical browser, keyboard or responsive observation is claimed. SSR and authenticated isolated API tests passed; ordinary services were not started for presentation testing. After a separately approved compatible deployment, check compact per-concern attachment expansion/downloads; historical cards and exact report links; DS/Commissioner allowed history fields/actions; applicant address with blank parcel and saved reload; account-switch privacy; and unchanged flagged/non-flagged Anomaly presentation. No ordinary test fixture is necessary for read-only demonstrations.

There is no new schema/permission requirement or application blocker found in Step 6. Existing TypeScript, broader-suite and manual-browser limitations remain disclosed. **Stop after Step 6. No Step 7 or ordinary deployment without explicit approval.**
