# Regulatory follow-up persistence — schema approval required

2026-10-23. Not implemented, migrated or activated. Existing findings and reassessment remain functional. This is the one outstanding feature in the final handover request.

## Why existing persistence cannot safely hold this history

`compliance_evaluations.FindingsJson` is the immutable original engine result. `regulatory_evaluation_provenance` has one row per evaluation and stores original input/catalogue provenance, not repeated officer actions. Rewriting either would break preservation. `officer_action_events` requires an investigation/request parent and permits only six fixed operations; officer requests permit only early/anomaly. Complaint and evidence events have different typed parents and authorization. Generic governance audit engine labels do not represent regulatory follow-up. Reusing those structures would require changing their existing contracts or mislabelling events.

The database skill [SKILL.md](../../../../../../.agents/skills/database-development/SKILL.md) explicitly says “Do NOT create migrations without an approved schema design.” The attached request permits proposing an unavoidable additive design, and separately forbids automatic ordinary migration deployment. It does not review this exact new schema. No migration or endpoint accepting unsaved notes was introduced.

## Proposed single append-only table

`governance_intelligence.regulatory_followup_events`:

| Field | Proposed constraint/purpose |
|---|---|
| Id | uuid primary key, server generated |
| EvaluationId | uuid, required restrictive FK to compliance_evaluations.Id |
| RuleCode / RuleVersion | required bounded 100-character strings, resolved from exactly one actual saved finding |
| ActorId | required server-authenticated creator; never accepted from browser |
| SubmissionKey | required client idempotency UUID; unique ActorId/SubmissionKey |
| Operation | ClarificationRequested or HumanReviewRecorded only |
| ReviewOutcome | null for clarification; otherwise NoAdditionalConcern, AdditionalInformationRequired, FurtherInstitutionalLegalReview |
| MissingInformation | required 20–2000 characters for clarification, null for human review |
| Remarks | required 20–2000 characters; request details or reasoned review |
| ReferencesJson | required JSON array of at most ten bounded 500-character descriptive references; never fetched |
| Status | Recorded only; does not claim delivery, resolution or approval |
| Revision | positive per evaluation/rule sequence, unique EvaluationId/RuleCode/Revision |
| PreviousEventId | nullable restrictive self-FK; same evaluation/rule chain enforced by composite FK and alternate key; unique non-null predecessor |
| CreatedAtUtc | server UTC normalized to PostgreSQL microseconds |
| InputSha256 / CanonicalJson / EventSha256 | bounded input/canonical typed-event integrity; canonical content includes all typed fields, original finding digest and event payload |

Index EvaluationId/CreatedAtUtc for ordered retrieval. CHECKs enforce operation/outcome/null semantics, text bounds, hash shape, reference array and positive revision. EF rejects updates/deletes. Down refuses destructive removal. No existing-table alteration, backfill, engine or finding update.

## Authorization and transaction contract for review

Use existing authenticated C4 role admission plus fresh trusted DS eligibility AND exact creator ownership from regulatory provenance for writes. A read assignment or Commissioner C4AssessmentReadAll is not write authority. Legacy reports lacking owned provenance remain read-only. Commissioner retains current explicit read policy; applicants and unrelated DS are denied. Creator/state/read permissions rechecked after reads and before persistence. No new role or permission source.

POST `/api/governance-intelligence/regulatory-assessments/{id}/follow-ups`: submissionKey, ruleCode, operation, reviewOutcome, missingInformation, remarks, references, expectedRevision. Server derives creator, rule version, UTC and original finding digest. Clarification allowed only for saved InsufficientInformation; human review only for saved RequiresHumanReview/RequiresReview or requiresHumanReview=true. Ambiguous duplicate rule codes fail closed. No client facts or statuses establish authority.

GET same route uses existing exact-report read policy and post-read revocation checks. Bounded history, no protected input text in broad summaries. Append transaction uses a narrowly scoped evaluation/rule advisory lock, expected revision and unique constraints. Same key/content retrieves the original event without duplication; changed content409; historical replay returns current revision separately. No HTTP, inference or reassessment runs within the transaction. Audit actor/time populated at persistence boundary, not from input.

Only proposed additional runtime rights:

```sql
GRANT SELECT, INSERT ON governance_intelligence.regulatory_followup_events TO slg_c4_regulatory_runtime;
```

No UPDATE/DELETE/schema/DDL/role/peer/account/outbox rights. No new flags or delivery mechanism. Reassessment reuses existing structured regulatory submission with a deliberate new key/input version, preserving prior IDs; no automatic run on follow-up save. Existing UI is `/component-04/regulatory-assessments/new`.

## Required next gate

Approve this schema and write-ownership policy for implementation and isolated testing. Then generate/review additive EF SQL, test both operations, original-result immutability, concurrency, canonical hashes, replay/conflict, creator/Commissioner/applicant denial, fresh revocation and actual restricted PostgreSQL rights in the new isolated restore. Prepare exact package/migration/grant activation evidence. Ordinary activation still requires its own explicit deployment approval. No ordinary fixture or inference is needed.

Recovery retains history, disables new writes/stops the updated API and revokes only this table's added rights before any compatible-package rollback; never destructive Down or restore over ordinary storage.
