# C4 final UI cleanup — implemented and read-verified

2026-10-23, Asia/Colombo. Frontend presentation changes are active through the existing Next development server. No API publish, restart, SQL, migration, grant, permission-document change, ordinary fixture, inference, staging, commit or push was performed.

The C4 sidebar begins with Proposals, omits Overview and numbered icons, and retains Commissioner/DS complaint destinations and active-link behavior. Authorized `/component-04` requests now return an uncached HTTP 307 to `/component-04/assessments` from the existing proxy **after** live admission. The page retains a redirect fallback. Applicant/anonymous denials execute first. Overview implementation and underlying authorized capability/report routes remain present. The layout-level “More governance tools and evidence” disclosure is removed, including its unused space.

Proposal actions use equal-width grid cells, with a single column at narrow widths. Screening toolbars use consistent minimum heights, wrapping and aligned links/buttons. Saved badges stay on one line, status cells have minimum width, and history tables scroll horizontally rather than splitting words. Shared C4 action rows receive consistent sizing. Existing stacked complaint labels, full-width fields, textarea heights, payloads and confirmation logic are unchanged.

The two literal `?` separators were authored in `OfficerAnomalyPanel.tsx` and `OfficerSavedReports.tsx`. They are now UTF-8 em dashes. Persisted anomaly requests contain `Scenario`/`ScopeKey = kiwula-kale-named-route-v2` and `SourceVersion = kiwula-named-route/2`, not that display label; their table has no human workflow-name column. Saved API bodies still match prior hashes. The disclosure indicator also contained a literal `?`, now a minus sign. Four authored loading/saving/analyzing status labels now end with an ellipsis instead of a stray question mark. No global replacement or stored-data repair was performed; genuine questions and user-entered punctuation remain untouched.

The yellow banner came from the noncompact `DevelopmentSessionBar`, which printed **Signed in** unconditionally, then printed Sign in when no session existed. Shared PageLayout now uses the existing compact header account control, retaining real expiry/error notices and signout. No session, credential, login-return or authorization logic changed. The applicant complaint CTA previously used `IndependentGovernance.primary`, whose white text lost to a later `.stack a` rule. It now uses the local inbox button class with explicit readable white text, focus/hover states and a 44px minimum height. Header sign-in/signout controls receive matching readable sizing.

## Verification

- **168 frontend tests pass**, zero failed/skipped; six new cleanup regressions plus updated historical navigation expectations. Existing live-admission/header-spoofing, applicant denial, safe return, complaint-form, evidence and report tests remain passing.
- **7 architecture tests pass**, zero failed/skipped. Backend business code/package unchanged; database integration tests were not run against ordinary storage.
- Focused lint: **0 errors, 3 existing hook/ref warnings**. TypeScript reports only the three known C1 failures: RecommendationsView.tsx309 TS2345; ParcelCatalog.tsx131/143 TS18048. No whole-production-build success is claimed.
- **173 authenticated HTTP/SSR checks pass**, including both root destinations, nine applicant route denials, central permissions/four queues, legitimate applicant tracking and **53 saved reports** with unchanged response hashes and applicant denials.
- **82 additional GET-only evidence checks pass across 12 saved early workspaces**: responsible DS retains verification eligibility; Commissioner remains read-only; other DS/applicants/anonymous remain denied. No upload or decision was submitted.
- All current C4 row fingerprints, normalized schema, migrations, grants, settings and sequences match the task's initial read-only baseline. Database remains **localhost:5432/state_land_governance_c4, OID34144, 41 tables, 21 migrations**. Frozen complaint/anomaly hashes, private candidate/permissions and all existing API package hashes match. See FINAL_PRESERVATION.json.

The initial frontend run caught an obsolete Overview/disclosure test; it was updated to the requested behavior. The first HTTP harness expected only a 307 from the page-level Next redirect, while Next emitted streamed redirect data. The final proxy branch provides a plain HTTP307; the complete final HTTP run passed. These were corrected and rerun, not skipped.

Existing services were reused: **API16456/5264**, **frontend16692/3000**, **ML36612/8104**. The API package remains `.tmp/c4-history-form-api`, using `Start-C4HistoryFormsApprovedApi.ps1` and the unchanged private candidate. No new launcher/package is needed for these frontend changes. The source is served by Next dev; no production frontend bundle was deployed.

Browser inventory was empty (`apps=[]`, `browsers=[]`). Desktop/tablet/mobile rendering, actual tab order, focus visibility, hover appearance, sidebar selection and sign-in click-through remain manual observations. HTTP/SSR and CSS assertions do not establish graphical success. See [manual checks](MANUAL_WALKTHROUGH.md).

Exact task paths and before/after hashes: CHANGED_FILES.json. TASK_DIFF.patch compares against task beforeimages rather than attributing the extensive pre-existing dirty working tree to this task. Previous implementation, deployment and model evidence is retained unchanged.
