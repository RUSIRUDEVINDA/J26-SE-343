# Finalized C3 governance manifest — remaining C4 integration contract

The C3 post-workflow adapter now invokes the existing `AssessWorkflowAnomalyCommandHandler` with exact persisted execution events, explicit UTC semantics and source revision. It does not modify the frozen model or synthesize its required terminal `DS Final Notification`. Anomaly output remains advisory; C3 records explicit human follow-up and finalization separately.

C4 currently exposes audit receipt, anchor and verification actions under `/api/governance-intelligence/audits/{auditRecordId}/...`. `VerifyAuditIntegrityQueryHandler` first loads a **C4 GovernanceAuditRecord**. Its verification cannot establish integrity of an arbitrary C3 manifest or a supplied SHA-256 alone. The consolidated screening response also does not expose a finalized C3-record audit registration.

The minimal owner contract must register or link an immutable source manifest with:

- Source system `C3`, case ID, finalized C3 governance-record ID and source record version.
- Exact execution/plan/snapshot/final-decision references and the finalized manifest SHA-256.
- An idempotency key for that same source ID/version/hash; different content must never replace an existing record.
- A returned C4 audit-record ID and echoed source bindings/hash.
- An authenticated caller/recipient boundary and acknowledgement/error semantics.

After registration, the existing C4 anchor/receipt/verify operations can report their actual status. Unconfigured transport, absent audit record, pending anchor, unavailable ledger or a mismatched hash must remain explicit states. No successful integrity proof, external audit identifier or legal finality is invented. C3's own manifest digest is a local integrity binding, not a substitute for that C4 receipt.

This is an owner-facing proposal, not an implemented endpoint or policy seed. No additional C4 backend change was made for this pending contract.
