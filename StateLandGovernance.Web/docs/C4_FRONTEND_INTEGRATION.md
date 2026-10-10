# Component 4 Frontend Integration

**Repository:** `RUSIRUDEVINDA/J26-SE-343`  
**Branch inspected:** `Component_04_RD_Frontend`  
**Base commit at inspection:** `23413d8`  
**Scope:** frontend and documentation under `StateLandGovernance.Web/`. Backend, ML, database, workflow and blockchain sources were read-only.

## Integration model

The browser talks only to ASP.NET Core through the shared `src/lib/apiClient.ts`.
It does not call the classifier/anomaly services, the Go blockchain service or
Besu directly. Demo data is available only after the user selects **Explore
demo data**. Switching back to **Live workspace** removes demo content; failed
live requests never fall back to fixtures.

The six published evaluation POST routes are user initiated, can persist
evaluation/audit data, and are not used as history or dashboard reads. The
frontend does not retry them automatically. The four structured evaluation
pages accept user-provided JSON matching the backend DTOs; they do not generate
sample evidence.

## Published Component 4 evaluation contracts

All routes use `/api/governance-intelligence/`. ASP.NET Core uses camelCase JSON
names. The routes return HTTP 200 results; invalid commands return HTTP 400
(model binding may also return ProblemDetails). The response fields named
`status`, `outcome` and `severity` are JSON strings. Source: the
`GovernanceIntelligenceController` and DTOs under
`StateLandGovernance/src/Modules/GovernanceIntelligence/`.

| Frontend | HTTP route and request | Result presented |
| --- | --- | --- |
| Compliance | `POST /evaluate-compliance`: `actionName`, legacy `leaseDurationYears`, `proposedUse`, `leaseAmount`, `zoningArea`, or optional structured `input` (`proposalId` and proposal evidence) | Status, violations, conditions/deadlines, detailed rule findings, source references, flags and evaluation ID |
| Conflict detection | `POST /detect-conflicts`: `actionName`, `decisions[]` (decision/subject/institution/authority/type/use/effective dates/reference and optional mandate, land-use and parcel fields) | Status, conflict type/severity/detection state, involved decision and institution IDs, subject, explanation, evidence rule, recommendation and timestamp |
| Governance risk | `POST /evaluate-risk`: `actionName`, `input` with `subjectId`, `decisionHistory[]`, `complianceViolations[]`, `governanceConflicts[]`, `complaints[]`, `institutionalValidations[]` | Risk score/severity, human-review flag, disclaimer, triggered indicators, evidence/rules/recommendations, score contribution, actors and timestamp |
| Explainability | `POST /explain`: `actionName`, `input` with `subjectId` and nullable `complianceEvidence`, `conflictEvidence`, `riskEvidence` summaries | Overall severity/review state and each explanation’s source engine, outcome, reason code, plain-language explanation, evidence, recommendation and timestamp |
| Institutional consensus | `POST /evaluate-consensus`: `actionName`, `input` with `subjectId`, `positions[]` and `policy` | Outcome/explanation/recommendation, returned counts, quorum/mandatory-institution/threshold booleans and timestamp |
| Conditional verification | `POST /verify-conditions`: `actionName`, `input` with `subjectId`, non-empty `conditions[]`, optional `evidence[]` and `policy` | Outcome/explanation/recommendation, returned counts, per-condition statuses/reasons/notes and timestamp |

Relevant exact enum strings include consensus policy modes `Unanimous`,
`SimpleMajority`, `Supermajority`, `ThresholdCount`; positions `Approve`,
`Reject`, `ConditionalApprove`, `Abstain`, `Pending`; evidence states
`Satisfied`, `Failed`, `Pending`; condition outcomes `FullySatisfied`,
`ProvisionallySatisfied`, `Unsatisfied`, `PendingEvidence`. These operations
evaluate only supplied inputs; they do not load a case or establish a legal
finding.

The frontend validates successful response structures before rendering them.
Malformed responses are reported as unavailable, not rendered as successful
results. HTTP 400/404/409/5xx, timeout, network and missing
authentication/authorization failures have distinct user-facing handling.

## Frontend route inventory

All views are under `/component-04`; direct navigation and refresh are
supported. Unknown views use Next.js `notFound`.

| Route | Screen |
| --- | --- |
| `/` | Governance dashboard |
| `/cases` | Assessment list, search, status filters and assessment detail |
| `/cases/compliance`, `/cases/conflicts` | Compliance/conflict evaluation entry points |
| `/cases/assessment/{assessmentId}` | Exact assessment dossier |
| `/cases/assessment/{assessmentId}/overview`, `/compliance`, `/conflicts` | Dossier views |
| `/complaints` | Classifier availability and explicitly fictional complaint history |
| `/anomalies` | Post-workflow anomaly availability and fictional trace example |
| `/screening` | Early evidence screening and referral status |
| `/risk`, `/explain`, `/consensus`, `/conditions` | Live risk, explanation, consensus and conditional-evaluation forms |
| `/timing` | Timing evidence and unavailable calculation |
| `/commissioner`, `/commissioner/review` | Unavailable queue and decision recording state |
| `/summary` | Final joined-summary availability |
| `/audit` | Audit-history availability |
| `/verification` | Blockchain verification and receipt availability |

## Capability and blocker matrix

`Evaluation route published` means a source-backed ASP.NET POST route exists; it
does not mean a live server is running or that a user is authorized. Browser
requests require a configured API base URL and a reachable backend.

| Feature | Frontend state | Backend contract | Live integration | Demo mode | Tests / blocker |
| --- | --- | --- | --- | --- | --- |
| Dashboard and assessment history | COMPLETE; truthful empty/unavailable states | No dashboard/history GET route | Not connected | Fictional dashboard data is explicit | Service isolation and browser route checks; **BACKEND CONTRACT REQUIRED** |
| Compliance | COMPLETE | `POST evaluate-compliance` | Connected through shared API client | Fictional case summary; no fake API response | Request/response tests; production authorization still required |
| Conflict detection | COMPLETE | `POST detect-conflicts` | Connected through shared API client | Fictional case summary; no fake API response | Request/response tests; no decision/history GET |
| Governance risk | COMPLETE | `POST evaluate-risk` | Connected through shared API client | No invented risk output | DTO response test; production authorization required |
| Explainability | COMPLETE | `POST explain` | Connected through shared API client | No invented explanation | DTO response test; production authorization required |
| Institutional consensus | COMPLETE | `POST evaluate-consensus` | Connected through shared API client | No invented institutional positions | DTO response test; production authorization required |
| Conditional verification | COMPLETE | `POST verify-conditions` | Connected through shared API client | No invented condition results | DTO response test; production authorization required |
| Complaint classification and history | COMPLETE unavailable/live-not-supported state | Internal classifier command/client only; no browser POST or history GET | Not connected | Fictional fixed examples, no classifier execution | **BACKEND CONTRACT REQUIRED**; **DB FINALIZATION REQUIRED** for persisted assessments |
| Workflow anomaly | COMPLETE unavailable/live-not-supported state | Internal predictor client only; no browser route or history GET | Not connected | Fictional completed trace only; higher score means more unusual and `score > threshold` means flagged | No anomaly F1/precision/recall without labeled ground truth; **COMPONENT 3 REQUIRED** |
| Early governance screening | COMPLETE unavailable/live-not-supported state | Internal command/query handlers, no browser route | Not connected | Seven exact indicator names; fictional missing evidence | **BACKEND CONTRACT REQUIRED**; **DB FINALIZATION REQUIRED** |
| Commissioner referral | COMPLETE unavailable state | Internal command; referral policy/handoff unconfigured and acknowledgement required | Not connected | No fictional referral is recorded | **COMPONENT 3 REQUIRED**; **BACKEND CONTRACT REQUIRED** |
| Timing / two-month handover | COMPLETE unavailable state | Calculator contracts only; no published endpoint or authoritative C3 boundary data | Not connected | No simulated clock or deadline | **COMPONENT 3 REQUIRED** |
| Commissioner queue and decisions | COMPLETE unavailable state; decision actions disabled | No queue/read or authorized decision route | Not connected | No fake Commissioner decision | **COMPONENT 3 REQUIRED**; **BACKEND CONTRACT REQUIRED** |
| Final governance summary | COMPLETE unavailable state | No joined summary/read route | Not connected | Separate fictional snapshots are not combined | **BACKEND CONTRACT REQUIRED** |
| Governance audit history | COMPLETE unavailable state | Internal audit query/receipt/verify handlers; no browser route/list endpoint | Not connected | No audit rows invented | **BACKEND CONTRACT REQUIRED** |
| Blockchain anchor, receipt and verification | COMPLETE unavailable state; no success claim or fabricated receipt | Go routes exist, but there is no published ASP.NET browser wrapper; anchor dependencies are not registered in current ASP.NET DI | Browser-to-ASP.NET-to-Go-to-Besu path unavailable | No fake hash, transaction or receipt | **BACKEND CONTRACT REQUIRED**; **DB FINALIZATION REQUIRED** |

### ML and anomaly semantics

The complaint taxonomy shown in demo examples matches the four categories in
the Component 4 ML configuration. The frontend cannot submit text to the
classifier because the application command and internal inference client are
not published as a browser endpoint. Model metadata validation/deployment
issues are not bypassed. No model is trained or loaded by this frontend.

The anomaly page intentionally does not report accuracy, precision, recall or
F1. There is no accessible labeled workflow-anomaly evaluation dataset or
browser evaluation contract. A score is shown only for an explicitly fictional
demo trace and is not a risk score, legal result or allegation.

### Early screening, persistence, workflow and trust boundaries

Early screening’s available internal indicator strings are
`LegalDispute`, `UnauthorizedOccupation`, `UnauthorizedConstruction`,
`FamilyOrInheritanceClaim`, `MultipleClaimants`, `UnresolvedObjection` and
`PreviousIllegalLandActivity`. Query handlers exist internally, but no
controller exposes them. Referral requires an acknowledged handoff and a
Commissioner process reference; the default policy/handoff is unconfigured.

`WorkflowsController` currently has no actions. Timing is an internal
calculator contract only and is not wired as an operational endpoint. The UI
therefore does not manufacture workflow identities, referrals, timing
calculations or Commissioner decisions.

Go exposes `GET /health`, `POST /api/v1/anchors`,
`GET /api/v1/anchors/{auditRecordId}` and
`POST /api/v1/anchors/{auditRecordId}/verify`. ASP.NET transport code exists,
but no browser controller publishes it and current DI does not register the
anchor store/service or outbox processor. The browser must not call Go or Besu
directly. An integrity match concerns record integrity only, not legal
correctness.

## Security, authorization and database limits

- **PRODUCTION AUTHORIZATION REQUIRED:** inspected ASP.NET controllers have no
  `[Authorize]` policy or configured authentication/authorization middleware.
  Evaluation endpoints persist results and must not be considered
  production-ready until identity and case-level authorization are provided.
  The frontend does not invent Commissioner roles or identities.
- **DB FINALIZATION REQUIRED:** complaint assessment and early-screening
  persistence depend on configured stores/PostgreSQL. No schema, migration,
  credentials or persistence code was changed.
- **COMPONENT 3 REQUIRED:** authoritative completed workflow traces, referral
  acknowledgement, process transitions, timing boundaries, Commissioner
  workflow and final joined case status.
- No backend, ML source/model/data/metadata, blockchain source/configuration,
  database migration, root configuration, environment file or `.gitignore`
  was modified by this frontend work.

## Responsive behavior and accessibility

The C4 shell reuses the existing shared page layout and uses C4-local responsive
grids, horizontally scrollable data tables, wrapping identifiers/evidence,
semantic headings/tables/forms, labeled controls, status/error announcements
and keyboard-visible focus. The mobile navigation is compact and remains
Component-4-scoped. Demo mode is visibly labeled; live mode never falls back to
demo fixtures.

## Validation record

Run these commands from `StateLandGovernance.Web`:

```powershell
npx tsc --noEmit
npm run lint
npm run build
node --test tests/component-04.test.mjs
node --test tests/component-04.browser.mjs
```

The latest `npx tsc --noEmit`, `npm run lint`, `npm run build`, and
`node --test tests/component-04.test.mjs` runs passed; all 12 focused tests
passed. Lint reports one existing warning in the shared `Button.tsx`.

The standalone browser test script expects Playwright, which is not in the
frontend package manifest or installed dependencies; it was not installed to
avoid changing dependencies. Through the integrated browser against the
already-running local Next.js server, all 17 C4 routes returned HTTP 200 with
headings and no horizontal overflow at 390px or 768px. Invalid JSON produced
an alert without making an API call. Demo mode persisted across navigation, and
the live risk evaluation was paused while demo mode was active.

The production backend was not called; live endpoint availability,
authorization, and persistence remain integration blockers.
