# Durable intake assignment design

Authorized scope: normal case-assignment lifecycle requested 2026-10-19.

Add `workflow_governance.case_assignments`: one row per existing case (PK/FK, cascade restricted), integer optimistic revision, JSONB audited state containing declared jurisdiction, exact matched policy id/version, office, active exact-document reviewer grant and append-only assignment/revocation history. Scope does not change applicant ownership. No automatic verification or approval. Reassignment replaces the active grant atomically and retains the previous grant. Authorization re-reads it on every request; tokens never carry mutable grants.

Add `workflow_governance.intake_jurisdiction_policies`: UUID PK, identifier/version unique, exact jurisdiction and office, publisher/source, effective dates. Definitions append-only. No default jurisdiction mappings or approval seeds. Zero/ambiguous matches remain Unassigned. An authorized intake coordinator may explicitly choose an office with a reason, preserving unresolved routing provenance.

Server-owned office coordinator and eligible-officer directory entries are trusted identity/office configuration, independent of case IDs. They never give access to case evidence. Coordinators see bounded intake metadata and may assign only their office or unresolved intake when explicitly authorized. Reviewer capabilities are a fixed server-owned ProposalReview profile, not browser input. Institutional reviewers retain their existing restricted paths.

New rows are lazily created for pre-existing cases without changing documents/analyses. Intake creates its assignment record immediately after case registration, before binary storage or OCR. Failed OCR cannot lose the intake. Short EF saves/optimistic concurrency; no transaction held across OCR. Existing static grants remain legacy compatibility only for cases without a managed assignment row; managed revocation cannot fall back to static grants.

Migration only creates new tables/indexes/FK. No populated column changes or deletion. Down refuses destructive reversal; recovery requires restoring matching code/schema or reviewed forward remediation. Back up C3 schema before applying locally. No C4 schema/code change.
