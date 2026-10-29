# Step 1 — finalization design adjustment requires review

2026-10-24, Asia/Colombo. **Blocked before implementation.** No application source, EF migration, database schema/grant, private configuration or service was changed in this Step 1 attempt. Only the local preflight helper and these review/evidence artifacts were written. No staging, commit or push occurred.

## Read-only preflight

The current ordinary database matches the saved functional-corrections baseline: localhost:5432/state_land_governance_c4, OID 34144, 41 user tables and 21 migrations. All captured row fingerprints, schema, migration history, grants and sequence metadata match. Private settings/permissions, published API package and both frozen model hashes match. See DESIGN_REVIEW_PRESERVATION.json. No ordinary write was made. The first evidence-save attempt failed because the new output directory did not yet exist; all preceding comparisons had passed. After creating that documentation directory, the complete read-only check passed and saved evidence.

## Why the attachment approval does not cover finalization storage

The approved 11-column officer_investigation_attachments table remains appropriate for pre-screening evidence. It has no finalization field/event and cannot establish that the officer deliberately finalized all seven findings. Inferring finalization from the presence of files would silently finalize untouched drafts, particularly an all-NoIssueFound investigation needing no files.

OfficerInvestigationEntity is an immutable observation snapshot, and OfficerFlowStore.SaveAsync emits InvestigationSaved immediately when that snapshot is saved. It therefore cannot also mean that mandatory persisted evidence was checked later. OfficerAssessmentRequest Saved means an actual engine result exists. The screening verification tables require a saved request and represent later evidence review. None is an accurate substitute for pre-screening manual finalization.

The existing officer_action_events table is the smallest reusable store. Its ck_officer_operation currently permits only InvestigationSaved, RequestSubmitted, InferenceStarted, AssessmentSaved, AssessmentFailed and OutcomeUnknown. Its existing unique operation index applies only to RequestId, not InvestigationId. The previous EXACT_SCHEMA_REVIEW.md explicitly rejects disguising a new business action in arbitrary JSON under an existing operation. I have not introduced a new sub-kind under InvestigationSaved as a workaround.

## Minimal proposed adjustment — not implemented

Review FINALIZATION_SCHEMA_ADJUSTMENT_REVIEW_ONLY.sql for the exact CHECK replacement and partial unique index:

- Add InvestigationFinalized as an allowed operation only with an investigation parent and no request parent.
- Add one unique partial index on InvestigationId for that operation, preventing two finalization events for an immutable investigation at the database boundary.
- Reuse existing typed actor, investigation, UTC, canonical-details and event-hash fields. No new columns or finalization table.
- Keep the approved attachment table unchanged. No assessment_journey_events, regulatory CHECK change, cross-engine orchestration or new runtime event-store grants.

The future server-owned canonical finalization payload would bind its version, submission key/input hash, immutable investigation input hash, all seven explicit choices, and exact persisted attachment IDs/concerns/content hashes. Server actor/time would be stamped at persistence. A short investigation-scoped transaction would validate fresh DS eligibility, ownership, complete findings and required exact-scope files before insertion. Same-key/content would replay; changed input or a conflicting second finalization would return conflict. The unique index provides the final duplicate/contradiction safeguard. Original snapshots and engine categories remain unchanged; later evidence review remains distinct. These are proposed implementation rules, not tested functionality.

## Approval boundary and verification limits

The Step 1 request says: “If the reviewed design requires an adjustment, explain the exact reason and request approval before expanding the schema.” It also requires an immediate stop for unapproved schema changes. The database-development skill requires approved schema design and database-enforced uniqueness. This proposed existing-table CHECK/index delta was not included in the attachment-table approval, so no migration or partial feature was implemented.

No Step 1 functional tests, isolated restore, migration tests, upload/finalization tests or browser checks have run. Existing historical test counts are not Step 1 results. Existing unrelated edits from the earlier broad request were left untouched. No model was loaded or inference run. No running service was restarted, replaced or reconfigured.

**Decision needed:** approve or reject this exact CHECK/index adjustment alongside the already approved attachment table for implementation and NEW-isolated-database testing only. Ordinary deployment would still require separate approval. Steps 2–7 remain out of scope.
