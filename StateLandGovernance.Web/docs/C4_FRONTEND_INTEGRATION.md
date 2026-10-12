# Component 4 frontend integration

## PP1 final live evidence (2026-10-12)

The live workspace defaults to the real API and presents the five `PP1-FINAL-*` scenarios as a separate **PP1 Final Seed Scenarios** group. Each dossier is a composite of the latest independently persisted screening, complaint, workflow-anomaly, timing, referral and audit records; it is not an atomic assessment. Bounded history requests use `Promise.allSettled`, so an unavailable section does not erase other evidence. Demo mode remains explicit and is never an automatic fallback.

Upstream Component 1 and Component 3 inputs are synthetic (`SyntheticPp1Seed`, `pp1-seed-contract-v2`, `PP1 Demonstration`). Component 4 computation, frozen-model inference, PostgreSQL persistence, and displayed local Besu anchoring are real. Referral acknowledgement is synthetic; real Component 3 workflow termination did not occur. An anomaly score is statistical unusualness—not probability, confidence, fraud, guilt, corruption, or a legal finding.

Audit history uses the structured nullable `compliance_evaluations.CaseId` correlation. The original combined-case audit remains valid but historically unlinked because its evaluation predates that correlation. Page loads show persisted **Last Verification Status** and **Last Verified At** and never invoke state-updating verification. The outbox processor is a local single-instance PP1 mechanism, not a distributed exactly-once dispatcher.

**Branch:** `Component_04_RD`
**Scope:** `StateLandGovernance.Web/src/modules/component-04/`,
`StateLandGovernance.Web/src/app/component-04/`, and the published Component 4
ASP.NET Core boundary.

## Trustworthy live/demo boundary

The browser calls ASP.NET Core only. ASP.NET Core owns persistence and calls the
Python ML service and shared blockchain service. The browser never calls
FastAPI, PostgreSQL, the Go blockchain service, or Besu directly.

- **Live workspace** renders only successful API responses. A failed or
  malformed response becomes an error/unavailable state and never loads demo
  fixtures.
- **DEMO DATA** contains visibly labelled fictional seed snapshots. Demo
  results are not saved and are not described as backend evidence.
- Complaint classifications are labelled **Machine Prediction** and are
  advisory closed-set predictions.
- Workflow anomaly scores are advisory statistical scores, not probabilities
  or confidence values. A flag is not evidence of corruption, fraud,
  misconduct, or illegality.
- Workflow-anomaly persistence does not exist. Only the current session result
  is displayed.
- Early-screening responses do not prove that Component 3 accepted a referral.
- Blockchain failures are displayed as unavailable; no mock receipt is used as
  live evidence.

## Published Component 4 routes

All routes use `/api/governance-intelligence`.

| Screen | ASP.NET Core route | Persistence/integration |
| --- | --- | --- |
| Dashboard | `GET /dashboard` | PostgreSQL read model when configured; honest empty result otherwise |
| Compliance | `POST /evaluate-compliance` | Component 4 evaluation/audit persistence |
| Conflict detection | `POST /detect-conflicts` | Component 4 evaluation/audit persistence |
| Governance risk | `POST /evaluate-risk` | Component 4 evaluation/audit persistence |
| Explainability | `POST /explain` | Component 4 evaluation/audit persistence |
| Institutional consensus | `POST /evaluate-consensus` | Component 4 evaluation/audit persistence |
| Conditional verification | `POST /verify-conditions` | Component 4 evaluation/audit persistence |
| Complaint classification | `POST /classify-complaint` | Real FastAPI classifier; successful assessment persists in PostgreSQL |
| Workflow anomaly | `POST /predict-workflow-anomaly` | Real FastAPI Isolation Forest; no anomaly persistence |
| Early screening | `POST /screen-early-governance` | Screening snapshot/referral intent persistence; no confirmed C3 handoff |
| Audit receipt | `GET /audits/{auditRecordId}/receipt` | PostgreSQL receipt/outbox state |
| Audit verification | `POST /audits/{auditRecordId}/verify` | Shared blockchain verification boundary when configured |

ASP.NET Core currently exposes these routes without an authorization policy.
Authentication, authorization, and case-level access control remain PP1
deployment blockers.

## Frontend routes

| Route | State |
| --- | --- |
| `/component-04` | Live dashboard with loading, error, empty and success states |
| `/component-04/cases` | Live/demo assessment table, filters and accessible detail drawer |
| `/component-04/cases/compliance` | Real compliance submission in live mode |
| `/component-04/cases/conflicts` | Real conflict submission in live mode |
| `/component-04/complaints` | Complaint v2 form, persisted assessment ID, original text, four probabilities and closed-set note |
| `/component-04/anomalies` | Ordered completed-trace validation, score, frozen threshold, flag and nine features |
| `/component-04/screening` | Real C4 screening result and explicit unconfirmed-referral state |
| `/component-04/risk` | Real advisory risk evaluation |
| `/component-04/explain` | Real explanation evaluation |
| `/component-04/consensus` | Real consensus evaluation |
| `/component-04/conditions` | Real conditional verification |
| `/component-04/audit` | Real receipt lookup or unavailable/error state |
| `/component-04/verification` | Real ledger verification or unavailable/error state |
| `/component-04/timing` | Unavailable pending authoritative timing inputs |
| `/component-04/commissioner` | Unavailable; no fabricated queue or referral acknowledgement |
| `/component-04/summary` | Unavailable pending an authoritative joined summary contract |

## Known unsupported capabilities

- Workflow-anomaly history and persistence.
- Complaint-assessment history as a dedicated history endpoint; the dashboard
  exposes only its persisted case projection.
- Confirmed Component 3 referral delivery/acknowledgement.
- Commissioner queue, decision recording, and authoritative workflow actions.
- Authoritative lease-approval timing inputs.
- A joined final-governance-summary contract.
- Production authentication and authorization.

## Accessibility and responsive behavior

The module reuses the shared application shell, government header, navigation,
buttons, badges, forms, cards, and maroon/gold tokens. Controls have labels,
errors use alert semantics, results use live regions, tables scroll on narrow
screens, focus is visible, and the assessment drawer receives focus and closes
with Escape.

## Validation commands

Run from `StateLandGovernance.Web`:

```powershell
npx tsc --noEmit
npm run lint
node --test tests/component-04.test.mjs
npm run build
```

The standalone `tests/component-04.browser.mjs` script requires a Playwright
module supplied through `C4_PLAYWRIGHT_MODULE`; Playwright is intentionally not
added to the production frontend dependency set.
