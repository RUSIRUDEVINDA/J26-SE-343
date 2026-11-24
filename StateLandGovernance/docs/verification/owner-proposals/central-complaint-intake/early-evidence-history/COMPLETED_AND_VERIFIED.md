# Early Governance evidence history — implemented and activated

2026-10-24 Asia/Colombo. The newer assessment now retrieves earlier authorized evidence and verification history in a separate **Historical — Read-only** section. No records, evidence or decisions were copied into the newer assessment.

## Finding and identity

Evidence had not disappeared from PostgreSQL. The current UI and API queried attachments/decisions only by the selected request ID; deliberate reruns create independent saved requests. They had no proposal-scoped historical read path.

The stable internal identifier is `officer_investigations.ProposalId`, linked by the saved request's `InvestigationId`. The query also requires the investigation/request creator and request ScopeKey to agree. The ordinary example uses `kiulakale-demonstration-adaptation-v1`. No display-name, fuzzy lookup or client proposal parameter establishes correlation or authority.

Original request `284e96a3-6744-45b4-8777-7f062336f7d5`, assessment `21e63369-6a89-4ea1-b882-49a3b659b111`, retains its PNG and LegalDispute Verified revision1. Attachment `c74e25f6-a006-4988-9998-485a887b0aae`, **State Land Development Proposal Form.png**, is 1,487,486 bytes; SHA256 `3752e6c4b98edffc8742fa6e64c9046f59c4fceb0a18d08361cc3f40f14facce`. Stored bytes and authenticated DS/Commissioner downloads match that hash.

Newer request `3ae73d0e-0271-485a-b307-52d603019679`, assessment `99c43be7-7e00-4664-9963-0f99ae1b8d0b`, retains zero current attachments and decisions: manual Unverified/revision0. Earlier request `64105595-9f53-499c-8276-e71daec4af6c` also appears separately with its original reference-only decision. No inference or ordinary screening was invoked.

## Implementation and security

New GET `/api/governance-intelligence/officer-assessments/{request}/manual-evidence/previous?limit=5&cursor=...` authorizes the anchor first, derives its persisted proposal identity, scopes eligible Saved/early predecessors before bounded keyset pagination, excludes the anchor and later requests, and reauthorizes each returned assessment plus the anchor after reads. Page size1–10; each group retains existing100attachment/1000event bounds. Empty predecessors are omitted; an empty-state message explains no authorized history exists.

Eligible creator DS retains existing current attachment/verification rights; shared proposal identity confers no other creator's access. Commissioner requires existing live explicit report-read permission and receives no historical write control/capability. Other DS/applicants/anonymous remain denied. Every download still goes through the original assessment's authenticated endpoint, integrity hash check and forced octet-stream attachment/no-sniff headers. No static file URL is introduced. Completed downloads are suppressed after the session token changes.

Current upload/decision controls and counts stay separate. Historical groups show original request/assessment, dates, concerns, attachment names/sizes/uploader, decisions/remarks/references/actor/time/revision and original report/download controls. Account/session/request keys reset the workspace; epoch checks discard obsolete responses. No evidence bytes, decisions or Verified states carry forward automatically.

No migration, new index, schema or grant was needed. Runtime uses its pre-existing SELECT on investigation/request/evidence tables. No engine, model, threshold, regulatory rule, complaint workflow, permission source, password or peer module changed.

## Verification

- 185 frontend tests pass; focused changed-file lint has zero errors/warnings.
- 35 focused backend and 7 architecture tests pass, zero skipped.
- 66 real HTTP/PostgreSQL checks pass against NEW isolated restored `c4_early_evidence_restore_20261024` on loopback57657. Actual existing Early Governance handler/store generated independent isolated reruns; evidence upload/decisions, pagination, current B independence, original downloads, another proposal/creator exclusion, reauthentication, revocation, concurrency, idempotency, canonical history and restricted privileges were checked. A uses a seeded saved-report fixture; new reruns use the real existing handler. No ML calls. All original restored-row hash multisets remain present after additive fictional fixtures.
- 113 ordinary authenticated read checks pass. All64existing saved report bodies match pre-cutover hashes. DS and Commissioner retrieve original PNG bytes, current B remains empty, historical groups preserve identities, unauthorized history/downloads fail, central queues and applicant tracking remain available. SSR report readiness200 and applicant direct-route redirect are verified.
- Full TypeScript remains blocked by only the three previously known C1 errors (RecommendationsView309; ParcelCatalog131/143). No full production frontend build is claimed. Existing NuGet vulnerability-feed warnings remain.
- Browser inventory is empty. Visual, keyboard, responsive and actual click-through checks remain manual; HTTP/SSR is not visual proof.

## Backup and deployment

Fresh read-only complete archive: `.tmp/c4-early-evidence-history-backup-20261024/state_land_governance_c4.dump`, 1,810,188 bytes, SHA256 `b3ad48da2f3f726af4c9d549972b94773f27d0922767f9cef148a842bc1d1b60`. New restricted directory permits only the current Windows account and SYSTEM. Listing/readability passed; restored to the NEW isolated database and all41original tables/schema/history/row fingerprints matched. Ownership/ACL execution was skipped in isolation; original ACL definitions remain in the archive. Earlier archives are untouched.

Verified old API16456 stopped once; compatible successor API11064 on5264 is healthy. Frontend16692/3000 and ML36612/8104 reused. Launcher `Start-C4EarlyEvidenceHistoryApprovedApi.ps1` validates all88package hashes, unchanged exact private candidate, separate permissions, renamed target/runtime identity, peer/account read-only protections and disabled worker/sandboxes. Package `.tmp/c4-early-evidence-history-api`; API DLL SHA256 `6c73a206df1fb8cdace4844446346948f8428ac516d69560647b1188c332ca8d`. No automatic startup migration or runtime SQL write was observed. Reauthenticate after restart.

Final native PostgreSQL identity remains localhost5432/**state_land_governance_c4**, OID**34144**, **41tables/21migrations**. Every original record/evidence byte/hash, schema, history, grants, sequence metadata, peer/account fingerprint, both private grant documents and candidate bytes match the new baseline. Frozen complaint/anomaly hashes remain unchanged. Isolated Docker stopped successfully with storage retained. No ordinary fixtures, ML inference, staging, commit or push.

## Recovery and remaining checks

Use the compatible reviewed package/launcher and retained configuration. On an authorization/preservation failure stop only the verified API and retain all data/history; do not fall back to an incompatible old binary, restore over ordinary storage, replay migrations or run destructive Down. This change has no SQL/grant rollback.

See [manual walkthrough](MANUAL_WALKTHROUGH.md), [exact changed paths/hashes](CHANGED_FILES.json), [backup proof](BACKUP_AND_RESTORE.json), [isolated proof](ISOLATED_RESTORED_HTTP_EVIDENCE.json), [ordinary API proof](ORDINARY_AUTHENTICATED_READ_EVIDENCE.json) and [final preservation](FINAL_PRESERVATION.json). No implementation/deployment blocker remains; graphical observations and known C1 build limitations remain explicit.
