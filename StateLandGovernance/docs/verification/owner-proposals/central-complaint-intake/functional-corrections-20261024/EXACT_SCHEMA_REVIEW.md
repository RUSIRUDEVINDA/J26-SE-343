# Exact proposed C4 schema — review first, implementation paused

2026-10-24 Asia/Colombo. **Requested design review only. Not approved, generated as EF, applied or isolated-tested.** The owner's review-first reply supersedes the earlier implementation continuation. No ordinary SQL write/migration/grant/configuration change, service restart, staging, commit or push occurred.

## Review files

- [Exact table definitions, all column types, FKs, CHECKs and indexes](PROPOSED_SCHEMA_REVIEW_ONLY.sql).
- [Exact existing regulatory operation CHECK replacement](PROPOSED_REGULATORY_CHECK_CHANGE_REVIEW_ONLY.sql).
- [Exact additional runtime privileges](PROPOSED_PERMISSIONS_REVIEW_ONLY.sql).
- [Initial investigation and plan](INVESTIGATION_AND_IMPLEMENTATION_PLAN.md), retained as the earlier proposal.

The SQL is deliberately a review artifact, not a migration or deployment script. No PostgreSQL syntax/execution proof is claimed. There is no fabricated migration timestamp/history row. After design approval, a new EF migration would be generated and its SQL compared to this exact approved scope, then tested only in a NEW isolated restore. Ordinary deployment needs separate approval.

## 1. Why existing C4 storage is insufficient as currently defined

| Existing storage | Actual constraint/contract | Why reusing it unchanged is insufficient |
| --- | --- | --- |
| officer_investigations | Immutable seven-choice snapshot; same-creator supersession; JSON stores observations, not files | Can preserve draft/final choices, but not confirmed file bytes, typed attachment metadata or a finalization event establishing current stage completion. Existing NotYetVerified remains a readable historical/internal draft value; new finalization would reject incomplete choices. |
| officer_assessment_requests | Kind only early/anomaly; state only Pending/Saved/Failed/Unknown; Saved requires an actual result; early/anomaly have different mandatory source FKs | A fake Draft/Saved request would violate the schema or falsely claim execution. It cannot select a cross-engine current journey or represent pre-assessment evidence readiness. Unknown cannot be recycled as draft. |
| screening_evidence_attachments | Mandatory RequestId plus same-creator FK | Stores attachments for a saved assessment; ManualEvidenceStore additionally requires State=Saved. Requiring supporting evidence before the first run is circular. Relaxing only the API check does not provide a legitimate pre-run request or investigation binding. |
| screening_evidence_verifications | Mandatory request/concern/creator chain; status Verified/Rejected/MoreInformationRequired; first previous status Unverified | Subsequent evidence decisions are distinct from explicit initial IssueFound/NoIssueFound attestation. Reusing a historical Verified event would violate independent rerun semantics. NoIssueFound without documents is not the existing Verified attachment/reference operation. |
| officer_action_events | Exactly one investigation/request parent; fixed operation CHECK; unique RequestId/Operation | Lacks proposal+revision/head semantics and restrictive links to regulatory/conflict records. Repeated stage/restart/outcome operations conflict with the existing uniqueness. Arbitrary JSON under an existing operation would mislabel business actions and bypass the reviewed contract. |
| regulatory_followup_events | FK to regulatory evaluation; operation CHECK only ClarificationRequested/HumanReviewRecorded | Cannot store NoFollowupRequired unchanged. It is not a cross-stage completion journal and cannot parent pre-assessment files. Existing revision/previous-event/key constraints already enforce append-only numbering and replay; there is no evidence of an overwritten revision counter to repair. |
| kiwula_assessment_provenance and saved engine reports | Per-engine result/provenance records | Remain authoritative for their exact result, but do not specify which records belong to the current journey or invalidate downstream readiness after an explicit rerun. Selecting max(timestamp) would not meet the request. |
| generic governance audit/outbox | Engine-oriented technical audit with existing operation/parent rules | Not a free business-workflow store. No new engine label, blockchain operation or outbox processing is proposed. |
| React currentEarly/currentRegulatory state | Browser memory | Cannot protect a direct API call, survive reload authoritatively or serialize competing reruns. |

It is technically possible to ALTER existing attachment/action tables extensively instead. The two-table proposal is a safer additive alternative: it preserves their existing non-null FKs, canonical histories, result semantics and downloads. It is not a claim that no conceivable single-table/generic-JSON design exists. Combining file bytes and journey events into one generic nullable payload table would weaken clear type-specific bounds and history queries; it is not the recommended design.

## 2. Proposed table A: officer_investigation_attachments

**Purpose:** confirmed supporting evidence for a real saved investigation before Early Governance execution. It is not a copied previous-assessment attachment or a fabricated assessment.

Exactly11columns:

| Column | PostgreSQL type | Null/default | Purpose |
| --- | --- | --- | --- |
| Id | uuid | NOT NULL; server-generated | Attachment identity |
| InvestigationId | uuid | NOT NULL | Exact existing investigation |
| CreatorActorId | uuid | NOT NULL; scoped server actor | Same responsible DS |
| SubmissionKey | uuid | NOT NULL | Idempotent explicit upload |
| Concern | varchar(100) | NOT NULL | One of the existing7enum names |
| FileName | varchar(150) | NOT NULL | Safe original filename |
| ContentType | varchar(50) | NOT NULL | PDF/PNG/JPEG/UTF8text only |
| Content | bytea | NOT NULL | Original1byte–5MiB file |
| ContentSha256 | varchar(64) | NOT NULL | SHA256 of the bytes |
| InputSha256 | varchar(64) | NOT NULL | Canonical upload identity/content hash |
| CreatedAtUtc | timestamptz(6) | NOT NULL; server audit boundary | Microsecond-normalized UTC |

PK Id; unique CreatorActorId/SubmissionKey; restrictive composite FK InvestigationId/CreatorActorId to the existing investigation alternate key; investigation/time/ID index. Exact CHECKs in SQL enforce nonempty identities,7concerns, safe bounded filename,4content types, size, lowercase64hex hashes and actual content/hash correspondence.

Fresh DS eligibility and investigation ownership must be checked before/after asynchronous reads and at persistence; no actor/role supplied by the browser grants authority. Existing file signature/UTF8 checks,100attachments/investigation cap, bounded metadata, secure authorized download, octet-stream/no-sniff and server-side SHA256 verification are reused. These controls do not authenticate the allegation or provide malware scanning.

New attachments remain investigation-scoped. A finalized current report may display their original metadata/download references through its exact bound investigation; it does not duplicate bytes/rows into legacy screening_evidence_attachments. A rerun requires an explicit new current investigation/finalization and must not automatically inherit older investigation files or decisions. Original request-based evidence/downloads remain unchanged.

## 3. Proposed table B: assessment_journey_events

**Purpose:** an append-only current-stage binding and completion/reservation history for one authenticated creator and exact server-resolved proposal. Highest integer Revision is the authoritative current head; time/display names do not choose current records. Old reports stay readable under their original scope even if unbound or predating this policy.

Exactly22columns:

| Column | Type | Null/default |
| --- | --- | --- |
| Id | uuid | NOT NULL; server-generated |
| CreatorActorId | uuid | NOT NULL; trusted scoped actor |
| ProposalId | varchar(120) | NOT NULL; exact server-catalogue identity |
| Revision | integer | NOT NULL; expected head+1 |
| PreviousRevision | integer | GENERATED ALWAYS AS (Revision-1) STORED |
| PreviousEventId | uuid | NULL only for revision1 |
| SubmissionKey | uuid | NOT NULL |
| Operation | varchar(40) | NOT NULL |
| Stage | varchar(16) | NOT NULL |
| State | varchar(16) | NOT NULL |
| InvestigationId | uuid | NULL until selected/reset |
| EarlyRequestId | uuid | NULL until reserved/reset |
| RegulatoryEvaluationId | uuid | NULL until saved/reset |
| ConflictProvenanceId | uuid | NULL until saved/reset |
| AnomalyRequestId | uuid | NULL until reserved/reset |
| ReservationEventId | uuid | NULL except one saved/unknown reservation outcome |
| SourceVersion | varchar(100) | NOT NULL; workflow-policy version |
| InputSha256 | varchar(64) | NOT NULL |
| DetailsJson | jsonb | NOT NULL; bounded object |
| CreatedAtUtc | timestamptz(6) | NOT NULL; server audit boundary |
| CanonicalJson | text | NOT NULL; bounded canonical object bytes |
| EventSha256 | varchar(64) | NOT NULL; SHA256 of exact canonical UTF8 bytes |

Compared with the initial prose plan, PreviousRevision and ReservationEventId make continuity/outcome correlation explicit. This is a refinement for review, not an implemented scope expansion. Locked is a derived API/UI readiness status; stored states are NotStarted,InProgress,Completed,Unknown.

### Keys, FKs and indexes

- PK Id; unique event/scope and event/scope/revision alternate keys for scoped self-FKs.
- Unique creator/proposal/revision and creator/submissionKey.
- Restrictive predecessor FK includes generated PreviousRevision: revisionN must reference revisionN−1 of the same creator/proposal. Unique non-null PreviousEventId prevents branches.
- Scoped reservation FK and unique non-null ReservationEventId allow exactly one persisted Saved/Unknown outcome for a reservation. The server validates the referenced reservation operation/stage and canonical input, not just its existence.
- Existing composite same-creator FKs for investigation, early request and anomaly request.
- Restrictive regulatory FK to existing owned regulatory provenance EvaluationId; conflict FK to kiwula_assessment_provenance.Id. These existing tables have no matching composite owner key, so DB FKs ensure existence; the server MUST independently validate creator, proposal/source and engine. No old-table index/column/key change is hidden here.
- Creator/proposal/revision-desc history index. No timestamp-based head selection or mutable current-head cache table.

The SQL enumerates all16operations and exact operation/stage/state combinations, ancestor-reference bounds, reset rules, reservation requirements, positive/predecessor revisions, bounded JSON objects and lowercase hashes. A canonical-hash CHECK proves correspondence to CanonicalJson bytes; it does not alone prove typed-column binding or authorship. A scoped audit interceptor must serialize ALL relevant typed fields, and reads/isolated tests must independently recompute that canonical representation. No user-defined DB function, trigger or PostgreSQL extension is added.

### Server rules beyond structural SQL

| Rule | Required authoritative check |
| --- | --- |
| Initial finding | Explicit IssueFound/NoIssueFound for all7concerns; untouched/NotYetVerified is draft/incomplete |
| IssueFound evidence | At least one confirmed same-investigation/concern/creator attachment, verified content/hash; selected local file is insufficient |
| Initial attestation | InvestigationFinalized records its exact immutable investigation, validated attachment IDs/hashes and manual provenance; no rewrite/promotion of automated evidence categories |
| Early completed | Exact current finalized investigation plus Saved early result; original automated categories remain separately labelled |
| Regulatory completed | Exact bound saved evaluation and latest required recorded dispositions for that immutable rule set; no mandatory review/hold waived by administrative NoFollowupRequired |
| Conflict completed | Supported exact saved conflict assessment and genuinely required officer actions. Existing illustrative comparison is not parcel conflict clearance. Current capability gap remains locked pending a separate policy decision |
| Anomaly execution | Exact current completed predecessors plus a server-owned reservation/request/projection; unchanged nine-feature extractor/model/threshold |
| Later evidence review | Subsequent review is labelled separately; Rejected/MoreInformationRequired on the current bound early request creates ManualReviewHeld and invalidates downstream readiness, preserving earlier result/history |
| Rerun | Explicit StageRestarted clears target/descendant current bindings; previous completion never silently unlocks a new unfinished run |
| Idempotency | Same creator/key/content returns original reservation/result; changed content409; same-key pending/unknown never invokes the engine again |
| Concurrent writes | Expected journey revision + unique constraints + one short creator/proposal transaction lock; stable lock order (submission key,journey,local resource); no external HTTP across locks |
| Interrupted inference | Original request/result retained; Unknown holds current readiness for investigation; no automatic retry or old-success fallback |
| Late old completion | Correlated to its original reservation; may not overwrite or complete a newer current generation |
| Direct bypass | All executing/finalizing normal and legacy entry points consult the same server gate. Old saved GETs and already-saved same-key replay remain reads, not new execution |

SQL deliberately cannot claim to check fresh host account eligibility, saved engine JSON semantics or legal requirements by itself. Those must be explicit application policies tested through actual controllers/repositories after approval. Commissioner keeps current saved-read authority and complaint permissions, without new Early/anomaly execution authority. Other DS/applicant/anonymous denials remain intact.

## 4. Exact regulatory CHECK delta

Current ck_regulatory_followup_operation accepts only ClarificationRequested or HumanReviewRecorded with their corresponding payload fields. The proposal keeps both clauses verbatim in meaning and adds only:

```sql
OR ("Operation"='NoFollowupRequired'
    AND "ReviewOutcome" IS NULL
    AND "MissingInformation" IS NULL)
```

The full DROP/ADD SQL is in PROPOSED_REGULATORY_CHECK_CHANGE_REVIEW_ONLY.sql. It would run atomically only in an approved future migration. All current rows remain valid; no backfill or existing event UPDATE. Applied migration20261023121903 must never be edited/replayed.

Global20–2000character Remarks stays mandatory, making it the new action's rationale; existing10references/500characters each, actor/time/canonical hashes, Status=Recorded, revision chain, FKs and unique keys stay unchanged. The server must reject the new action for a saved RequiresHumanReview/RequiresReview or explicit mandatory-review/hold condition. It is an administrative disposition, not Compliant, proof of truth or legal approval.

Existing unique evaluation/rule/revision remains. RuleVersion is the immutable saved finding's version for that evaluation/code, validated on each operation; no new version may be substituted. Replay returns the original event; a new deliberate key with current revision appends the next integer. An old request with stale revision conflicts instead of overwriting an event.

## 5. Exact permission delta

```sql
GRANT SELECT, INSERT ON TABLE governance_intelligence.officer_investigation_attachments TO slg_c4_regulatory_runtime;
GRANT SELECT, INSERT ON TABLE governance_intelligence.assessment_journey_events TO slg_c4_regulatory_runtime;
```

Exactly4new table-privilege entries. No runtime UPDATE/DELETE/TRUNCATE/DDL/schema/role/sequence/account/peer/outbox grants. Existing regulatory SELECT/INSERT suffice. Table owner/operator identity/password and all prior permissions remain unchanged. Unexpected inherited/default privileges must cause a STOP/review, not an unreviewed broad ACL repair.

Runtime append-only grants and EF write guards reject history UPDATE/DELETE. Server actor/time stamps are derived from trusted scoped context, never browser flags. This is not RLS or protection from an unrestricted superuser; backups/operator controls remain necessary.

## 6. Preservation, counts, testing and rollback

Current read-only baseline: state_land_governance_c4/OID34144,41tables/21migrations, models unchanged. No proposed schema object exists from this task. If later approved as one additive EF migration, the expected target would be43user tables/22migrations, contingent on a fresh drift check; those future counts are NOT current facts.

After isolated-implementation approval only: generate/review new EF SQL; use a NEW secure complete read-only backup and NEW isolated restore; compare all original rows/schema/history/ACLs; apply only the new migration and4grant entries there; test actual restricted-role persistence, every execution gate and bypass route, all7findings/evidence rules, ownership/Commissioner denial, typed canonical integrity, revisions/replay/concurrency, restart retrieval and stale/Unknown reruns. No ordinary fixtures or ML call is needed to test pure/HTTP contracts; real inference requires separately intentional controlled testing and never forced outputs. No test success is claimed for this not-yet-implemented design.

Rollback is non-destructive: keep tables/events and expanded CHECK, stop new execution/compatible API as necessary, revoke only new-table rights if approved recovery requires it, retain histories. Do not narrow the CHECK after NoFollowupRequired rows exist, delete them to permit rollback, run destructive Down, restore over ordinary storage or restart an older API that bypasses the new gates. Continuous recovery service requires a separately reviewed compatible fail-closed/read-only package; no safe old-binary equivalence is claimed.

## 7. Work that preceded the review-first reply

Some independent source edits began under the original request while this design question was pending: anomaly display, complaint-history cards, standalone-address JSON/API/unit-test work and capability-gated frontend preparation. The50selected in-memory/unit regressions completed successfully; no comprehensive final/frontend/isolated/deployment success is claimed. Backend changes were NOT published; the current API package is unchanged. The existing development frontend can hot-serve presentation edits; no process was restarted. The new address form is capability-gated so the old running API cannot silently discard its address.

Those edits are retained uncommitted, and further implementation is paused. See PRE_REVIEW_SOURCE_DELTA.json/TASK_DIFF_AT_REVIEW.patch for exact task-only paths, excluding prior unrelated working-tree changes. No new EF entity, migration, schema CHECK or sequential gate was implemented. The two withdrawn attachments were not implemented.

**Awaiting the owner's explicit design approval or rejection. No ordinary deployment is authorized by design approval. Conflict capability treatment remains an unresolved policy decision; no acknowledgement option was inferred from the review-first answer.**
