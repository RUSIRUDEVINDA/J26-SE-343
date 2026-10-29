# C4 final assessment-history presentation polish

2026-10-24 Asia/Colombo. **Frontend-only cleanup complete; ready for manual acceptance. No staging, commit or push.** Existing services were reused without restart or API redeployment.

## Before and after

| Area | Before | After |
| --- | --- | --- |
| Early Governance current | Expanded attachment/history disclosures; long request/officer/event IDs and hashes inline; repeated status text | Current Assessment heading; unchanged upload/decision controls and feedback; direct attachment/history lists; status badges; optional Technical details for identifiers/provenance |
| Early Governance previous | Repeated Historical — Read-only labels and nested attachment/verification disclosures | Previous Assessments with the requested read-only explanation once; compact original-assessment cards with date, concern/latest status, attachment/history counts, visible records/downloads, original report link and one Technical details disclosure per card |
| Regulatory current | Generic follow-up heading; expanded saved-history disclosure and per-event provenance disclosures | Current Regulatory Follow-ups; unchanged form/replay controls; visible revisions with original finding context, remarks/references and one Technical details disclosure |
| Regulatory previous | IDs used as primary headings; repeated read-only labels and event IDs inline | Previous Regulatory Follow-ups; one read-only explanation; date-led original-assessment cards; all distinct revisions remain visible; authorized original-report metadata; single Technical details disclosure per card |

Previous sections say exactly once: “Previous assessments are shown for reference. Changes can only be made to the current assessment.” Historical cards have downloads/report navigation but no upload, save or decision forms. Current attachments, decisions and revision remain independent. Original automated screening results remain unchanged by manual decisions.

Shared C4 CSS provides compact cards, wrapped long filenames/remarks/IDs, responsive narrow layouts, visible focus and existing status badges. Useful records are visible directly; only technical metadata is collapsed. Exact officer IDs remain available under Technical details; the visible label identifies the responsible DS without inventing a person's name.

The historical regulatory event DTO contains no rule-title or original-screening-outcome field. The UI therefore reads each original report using the existing authenticated `getRegulatoryReport` GET, with cancellation on unmount. It matches both rule code and version, and uses the saved requirement as the readable title, consistent with the existing report UI. Missing/unavailable original metadata is explicitly marked unavailable; no title or outcome is inferred. These are read-only presentation requests, not a new backend contract, workflow or assessment. Pagination, historical authorization, abort/session guards, forms, validation, pending keys and idempotency are retained.

## Verification

- **189 frontend tests pass**, zero failed/skipped. New/updated focused tests cover single explanations and technical disclosures, original identities/download controls, historical non-editability, current-state independence, separate regulatory revisions and missing/mismatched original metadata. Existing upload feedback/single-flight/replay, permissions/navigation/applicant restrictions and complaint regressions pass.
- **7 architecture tests pass**, zero skipped. No backend source change or backend deployment was needed.
- Focused ESLint on all six changed TSX components: **0 errors/warnings**.
- TypeScript still reports only the3known unrelated C1 errors: RecommendationsView309 TS2345; ParcelCatalog131/143 TS18048. No full production frontend build is claimed.
- **113 authenticated ordinary read checks pass**, including all64saved report bodies matching pre-cutover baseline hashes, original PNG exact-byte downloads for responsible DS and Commissioner, unauthorized DS/applicant/anonymous denials, current-zero versus historical-original evidence, existing complaint permissions/queues/applicant tracking, reauthentication and SSR route readiness. No ordinary write fixture, inference, screening or business action.
- Read-only PostgreSQL confirms **state_land_governance_c4/OID34144,41tables/21migrations**; all original row fingerprints, schema, migration history, grants and sequence metadata are unchanged.
- All2162protected backend/configuration and authentication/service-source hashes checked remain unchanged. The currently published88-file API package, exact process environment, private candidate and both independent permission documents are unchanged. Frozen complaintv2 SHA256 `63b37eb7175e74a8dcde82bf042fcf9dcbb4f0432fbe3b4677aac037007a20ae`; anomalyv1 SHA256 `59ed4a73d56d9f06798c4ebc4684a633afe72841c41ed0b3ba4e7bb658782b46` unchanged.

One new test initially expected capitalized “Insufficient Information”; it was corrected to the established display label “Insufficient information” and the full final suite reran green. No application status, model or rule was changed for that assertion.

## Services and remaining observations

API11064/5264, portal16692/3000 and ML36612/8104 are reused and healthy at verification. Frontend changes are served from source by the existing Next development server. No API republish, restart, launcher, SQL, schema, migration, grants, model, account/password, sign-in/sign-out/return routing or complaint/triage change was made.

Browser inventory apps=[]/browsers=[]: graphical, keyboard and responsive observations are pending manual acceptance. Automated rendering and HTTP readiness are not browser visual proof. There is no implementation blocker; known C1 build limitations and graphical acceptance remain explicit.

Exact task-only changed files and hashes: [CHANGED_FILES.json](CHANGED_FILES.json). Review [TASK_DIFF.patch](TASK_DIFF.patch), [manual checks](MANUAL_ACCEPTANCE.md), [read-only preservation](FINAL_PRESERVATION.json) and [authenticated read evidence](ORDINARY_AUTHENTICATED_READ_EVIDENCE.json). Pre-existing unrelated working-tree changes are excluded from the task diff. Do not stage the whole repository or treat this UI diff as the entire source handover.
