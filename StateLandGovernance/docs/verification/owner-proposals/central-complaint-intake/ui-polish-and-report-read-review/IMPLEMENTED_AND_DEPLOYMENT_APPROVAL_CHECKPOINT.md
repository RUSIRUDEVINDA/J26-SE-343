# C4 UI polish and Commissioner report read access

2026-10-23, Asia/Colombo. **UI-only changes completed on the existing development frontend. Material backend authorization implemented/tested in isolation; NOT deployed. Database rename audited only.** Existing activated API PID27588 and its approved package/private settings/complaint permission document remain unchanged.

## Delivered UI

Proposals remains `/component-04/assessments`, initially a listing with separate **View Proposal Data** and **Start Screening** controls. Proposal data opens/closes in memory and includes original location, stated extent/use, tenure unknowns, source/supporting data and routing assumptions. Nothing expands initially; viewing invokes no screening/write. Regulatory Screening is now the separate real form route `/component-04/assessments/regulatory`; existing report URLs and deep links are preserved. Navigation includes both entries and uses the existing longest-matching-route highlighting. Commissioner navigation is not hidden behind DS labels; Central Complaints remains server-capability gated.

The anomaly button consistently says **Run Anomaly Analysis**, retaining execution/idempotency behavior. Both current and original saved anomaly displays use a responsive nine-feature grid with exact values and count/day/hour units. Model name/version, exact score/threshold and artifact/input SHA256 are in a separate technical section. Repetitive PP1/demo headings are reduced; a concise illustrative/unconfirmed-routing disclosure remains. Evidence JSON, model values, inputs, calculations and saved records were not edited.

## Exact material policy change — separate deployment approval required

The current activated API correctly restricts protected reports to their creator/explicit assignments. Cross-creator Commissioner access is a material change, so the user's attached request requires this stop before API replacement.

Implemented a **separate read-only permission `C4AssessmentReadAll`**, distinct from complaint Read/Assign/Review. The approved complaint document, its grant ID and all nine complaint operations remain untouched. No table, migration or SQL privilege change is proposed.

Proposed new configuration, NOT loaded:

```text
GovernanceIntelligence__CommissionerReportReads__Enabled=true
GovernanceIntelligence__CommissionerReportReads__GrantFilePath=D:\AI-Driven-Digital-Lease-Governance-for-State-Land-Management\AI-Driven-Digital-Lease-Governance-for-State-Land-Management\.tmp\commissioner-report-read-review\report-read-permissions.local.json
```

The proposed private file is not created/activated in ordinary runtime. [PROPOSED_REPORT_READ_GRANT.json](PROPOSED_REPORT_READ_GRANT.json) contains exact actor`0ab669fe-d0ab-5695-ad55-10f6b3b5aab3`, proposed grant`77092597-0570-4e16-a71e-ea8d264cb462`, Officer/COMMISSIONER eligibility, only C4AssessmentReadAll, empty ReviewOperations, Persistent=true, Revoked=false, valid-from2026-10-23T00:00Z and no expiry. [PROPOSED_REPORT_READ_SETTINGS.json](PROPOSED_REPORT_READ_SETTINGS.json) records the exact section. The feature defaults false and does not derive authority from an Officer/Commissioner role label or complaint grant.

Reuses the existing bounded server grant-file reader and fresh account/directory adapter. The read policy requires this exact actor, correct current institution/profile, explicit current permission, provisioned source and unrevoked/unexpired grant. Reads recheck after asynchronous data retrieval; malformed/unavailable source fails closed. Changing/removing/revoking this separate grant removes cross-creator access without changing complaint permissions. Owner-only DS/applicant paths remain.

Scope: saved regulatory, Kiwula conflict/early, saved post-workflow demonstrations and saved officer early/anomaly requests through their existing report GET/list endpoints. Pending/Unknown other-creator jobs and unsaved investigation drafts remain private. Original creator, exact result, source/catalogue/model metadata and audit events are returned unchanged; missing historical ownership/provenance remains unavailable, never reconstructed. No write command/creator-key scope was broadened. No capability to edit/delete/rerun another officer's report is introduced.

## Verification

- **143 frontend tests pass**, including distinct navigation, initial collapsed proposals, real regulatory route and exact feature values/units. Focused lint0errors. Node SSR test loader now supports Next Link and JSON imports; this changes tests only.
- **275 focused backend tests and7architecture tests pass**, covering existing complaints/ownership plus explicit read grant, disabled/inactive/other-actor/expired/revoked/role-only denial, independence from complaint permission and revocation during an asynchronous regulatory read.
- **35 isolated repository checks pass,30saved reports read** against the retained isolated restored PostgreSQL database on57657. Connection-level transaction_read_only=on enforced. Actual stores preserve creator/provenance, allow all requested report families, deny an unrelated actor and remove global listings after live report-file revocation. No write or ML handler was invoked. This is repository/policy proof, not a claim of a new ordinary HTTP deployment or browser test.
- API build and review-only Release publish pass. `.tmp/central-report-read-review-api` is NOT running. [REVIEW_PACKAGE_SHA256.json](REVIEW_PACKAGE_SHA256.json) and [CHANGED_SOURCE_SHA256.json](CHANGED_SOURCE_SHA256.json) identify exact artifacts/files.
- Only the three existing C1 TypeScript errors remain (RecommendationsView309 TS2345; ParcelCatalog131/143 TS18048). No full production frontend build success is claimed. Browser tools unavailable; the three affected routes return HTTP200, not authenticated visual/keyboard/responsive proof.

Full results: [TEST_SUMMARY.json](TEST_SUMMARY.json), [ISOLATED_REPORT_READ_EVIDENCE.json](ISOLATED_REPORT_READ_EVIDENCE.json), [FRONTEND_HTTP_READINESS.json](FRONTEND_HTTP_READINESS.json). Active API/complaint grant/package unchanged: [ORDINARY_PRESERVATION_EVIDENCE.json](ORDINARY_PRESERVATION_EVIDENCE.json).

The comparison with the earlier activation baseline found additional research records in nine tables. **Every original row hash is retained**; current counts and differences are preserved in [RESEARCH_RECORD_DIFFERENCES.json](RESEARCH_RECORD_DIFFERENCES.json). The submitting process is not established. This task issued zero ordinary record writes or model calls. A future deployment must take a new verified backup including those additions; do not force the old baseline or overwrite its archive. Original complaints/classifications and complaint permission document remain unchanged.

## Existing login

Username **`commissioner`**; existing password-based sign-in, PBKDF2 verification, JWT and30-minute live server session. Exact actor/profile pairing confirmed; account is active under the existing profile contract and **sign-in returned200**. Login: http://localhost:3000/sign-in. Commissioner dashboard: http://localhost:3000/component-04/central-complaints. [EXISTING_COMMISSIONER_LOGIN.json](EXISTING_COMMISSIONER_LOGIN.json).

The documented development-only credential currently matches the stored verifier. Retrieve it yourself locally by opening `StateLandGovernance/src/Api/Security/DemoAccountProfiles.cs` and locating its `Password` constant. No password/hash/token/private connection value is copied here or to chat. No reset is needed or performed. `Setup-DemoAccounts.ps1` preserves existing accounts and is NOT a password-reset mechanism; no reset procedure is invented or executed.

## Boundary and next approval

Approve the exact separate report-read grant/settings and reviewed API package before ordinary deployment. Before deployment: fresh backup/isolated restore for current research records, original-data/grant/source/configuration preflight, reviewed versioned launcher/candidate preserving all existing settings, then replacement of one verified API only. No migration or PostgreSQL grant is needed for this read-policy change. No new inference/ordinary complaint action is needed for verification. Do not reuse a manifest-pinned old launcher to launch a different package.

Until that approval, broader report access remains **pending**, even though its UI navigation is visible. The current active package retains the previously approved creator restrictions. A database rename is a **separate** decision requiring the exact new name and explicit approval; see [DATABASE_RENAME_AUDIT.md](DATABASE_RENAME_AUDIT.md). No rename, new empty ordinary database, credentials/connection-string/PostgreSQL change, API restart/replacement, worker/Groq/blockchain change, C1/C2/C3 modification, commit or push occurred.
