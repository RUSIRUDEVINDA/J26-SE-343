# Central complaint intake ? read-only audit and approval checkpoint

2026-10-23 (Asia/Colombo). **AUDIT COMPLETE; IMPLEMENTATION STOPPED FOR REQUIRED APPROVAL.** No application source, frontend, configuration, role, permission, database record/schema, migration, model, worker, blockchain or Git history was changed. Only this audit/proposal/evidence and ignored local audit/test artifacts were written. No complaint, inference, retry, review or assignment was submitted.

## Decision

The current architecture does NOT fully support the requested central intake. A stored Commissioner profile exists, but there is no central-complaint authority contract. Current DS access is case-based, not complaint-specific. New central authorization and changed assignment semantics are required even if assignment metadata were embedded in existing JSON. Therefore the user's explicit stop condition applies before implementation, migration generation, grants or access changes.

Recommended minimum: one C4 append-only assignment-event table; SELECT/INSERT only on it; two explicit centrally issued C4 permissions; a host-supplied trusted directory adapter; and replacement of case-derived complaint visibility with current explicit complaint assignment. No new JWT role, account, password, classifier/engine, C3 project dependency or integration write is required. Approval is needed for this authorization/assignment design, not merely SQL deployment.

## Exact source findings

| Area | Inspected source and finding |
| --- | --- |
| Identity | Api/Security/DevelopmentAccounts.cs validates live sessions and exact stored actor/token-role pairing; browser-injected institution/capability/authority claims are rejected. Display-role text is not authority. Api/Security/PortalAccountStore.cs loads profiles/settings into host configuration and preserves existing accounts on explicit setup. No setup command was run. |
| Commissioner | Api/Security/DemoAccountProfiles.cs contains a Development Commissioner profile with token role Officer/institution COMMISSIONER. Fresh metadata independently confirms one stored Officer/COMMISSIONER profile. This proves a profile exists, not that its password/login works, that it is an officially appointed department, or that it has central complaint authority. No login attempt or permission grant was made. |
| DS directory | WorkflowGovernance/Presentation/Security/IntakeAssignmentDirectory.cs exposes trusted IntakeReviewers with ActorId/OfficeCode and separately scoped IntakeCoordinators. Fresh settings contain2reviewers and1coordinator;2reviewers match existing Officer/DS profiles. This is a case-intake directory, not a complaint-routing authorization. C4 must not reference the C3 internal directory/project. |
| Intake assignment | WorkflowGovernance/Application/Commands/CaseAssignmentService.cs requires a coordinator's office scope, real case/document binding and valid reviewer/expiry. It changes responsibility for a CASE and its evidence workspace. Reusing ChangeAsync to route a complaint would widen/alter case permissions and cannot route an unlinked complaint. It must remain untouched. |
| Submission | GovernanceIntelligence/Application/Complaints/RegisteredComplaints.cs registers original text/owner/hash/submission key before inference; optional validated CaseId/parcel may be null. Classification uses the existing client, CAS lease and registered-complaint:<id> binding. Registration survives inference failure. |
| References | Api/Security/ComplaintReferenceAccess.cs allows blank references, resolves a supplied case UUID/application reference through the existing case repository, and validates a supplied parcel against available C1 projections, including matching case when supplied. Invalid supplied references are rejected; they are not silently discarded or guessed. No ownership requirement is inferred. No text-based Kiwula linkage exists. Preserve this adapter's validation behavior. |
| Officer access | RegisteredComplaintService.RequireAsync denies officer reads for CaseId=null and otherwise requires ReviewerCasesAsync(actor). ListAsync filters and rechecks the case list; DetailAsync rechecks after classification reads. ComplaintReferenceAccess.ReviewerCasesAsync uses live, unrevoked, in-date C3 CaseWorkspaceReviewer assignments. These are not per-complaint assignments and are not restricted to DS institution by the complaint controller itself. |
| APIs | RegisteredComplaintsController currently provides list/detail, applicant POST, classification retry and officer review. Its officer category includes Officer/ComplianceOfficer/LandOfficer. No central list, target-directory, complaint-assignment or reassignment endpoint exists. Routes gate Enabled/WritesEnabled and resolve server actor IDs. |
| Original persistence | RegisteredComplaintEntityConfiguration and RegisteredComplaintStore own registered_complaints, classification link and existing assessment tables. Original identity/reference fields are immutable; CAS updates only Revision/PayloadJson. There is no AssignedOfficer field or assignment history in the typed record. Adding metadata to JSON would require changing its converter/application contract and risks dropping it on legacy record serialization; it is not a safe no-design shortcut. |
| Review | Existing allowed statuses are UnderReview, InformationRequested, ReviewRecorded with a bounded reason and expected complaint revision. Review neither verifies nor resolves allegations. Preserve these statuses, history and payload except normal explicitly authorized review writes. |
| Alternate readers | GovernanceIntelligenceController denies registered-complaint:<id> results through legacy assessment/history readers. Preserve that protection; new central access must not expose unrelated legacy classifications. |
| Audit reuse | officer_action_events has an exactly-one-parent FK to investigation/request plus a fixed operation CHECK. It cannot accept a complaint assignment without changing its approved schema. GovernanceAudit EngineType has six engine labels, none representing complaint routing. Do not mislabel routing as another engine or activate audit outbox/blockchain. The new immutable assignment record itself supplies the narrowly scoped audit trail. |

Paths above are relative to StateLandGovernance/src; module names expand under Modules/. Exact source hashes are in AUDIT_SOURCE_SHA256.json.

## Fresh read-only database evidence

All catalogue/SELECT connections forced default_transaction_read_only=on. Target C4 localhost:5432/c4_handover_local, role slg_c4_regulatory_runtime. Accounts inspected read-only in portal_accounts_local. No credentials, private profiles, original complaint text or private actor identifiers were printed or included in this report.

-17applied C4 migrations, through20261022152556_AddOfficerInvestigationsAndAssessmentRequests. No complaint assignment/routing table exists.
-3registered complaints:1linked,2unlinked. All3are Classified/Open.
-Both unlinked complaints have stored classification associations. All association bindings match registered-complaint:<id>; stored assessment original text matches the complaint payload; stored models arev2 and probability sums are valid. This is stored-data inspection, not a replay or fresh inference/accuracy claim.
-One Officer/COMMISSIONER profile;2Officer/DS profiles. Existing trusted directory has2DS-profile matches. No identities are automatically elevated by these counts.
-Current runtime SELECT/INSERT on complaint aggregates/classification tables; aggregate UPDATE only Revision/PayloadJson. No assignment table rights exist. Current officer-action table rights do not confer complaint-parent support.
-Counts, catalogue columns, actual column privileges and privacy-minimized fingerprints: READ_ONLY_METADATA.json.

The earlier officer-flow archive covered1complaint. It is not a sufficient current activation backup for these3records. Before any approved ordinary activation, create a NEW read-only archive and verify it by restoring to a NEW isolated database and comparing schema/history/fingerprints including both unlinked records. Do not overwrite older backups or restore over ordinary storage.

## Exact proposed scope ? NOT IMPLEMENTED

### 1. Central authority and identity

Reuse the existing host session and token roles; no new account/JWT role or password change. Add explicit server-owned C4 permissions `C4ComplaintCentralRead` and `C4ComplaintCentralAssign`. Assign requires Read. Neither Officer, COMMISSIONER institution, a C3 capability nor a friendly label alone grants access.

Each operator-reviewed grant must identify one exact existing actor, grant UUID, expected trusted institution COMMISSIONER, permitted operations, ValidFromUtc, ExpiresAtUtc and source/approval reference. All fields are server-owned. Reject missing/duplicate/expired/mismatched grants. Select the existing Commissioner profile only after operator review; do not grant every Commissioner or any DS by default. Exact actor and timestamps must appear in the controlled activation checkpoint, not be invented in this audit.

Proposed configuration section GovernanceIntelligence:CentralComplaints with Enabled/WritesEnabled default false and an explicit Grants array. No configuration was changed. The final reviewed key/value delta, including grant identity and expiry, must be concrete before activation. Existing complaint flags/credentials, peer/account read-only connections and disabled sandboxes/workers remain unchanged.

### 2. Trusted DS directory

Add a C4 Application port implemented by a thin Api host adapter. Read existing trusted account metadata and IntakeReviewers data without changing it. Return only eligible existing Officer/DS actors with validated nonempty office codes, safe display labels and a provenance snapshot digest. Never return password/session/profile payloads. C4 Application/Infrastructure must not reference C3 internal projects. No new Shared business module is needed.

Validate target again on submission; never accept client-supplied role/institution/office as authority. The central officer makes the recorded manual routing choice; a directory entry does not prove land jurisdiction or mandatory statutory routing for an unlinked complaint. Read current trusted metadata for authorization, not only a browser label or stale selector option. Existing startup-loaded configuration is not evidence of immediate account-directory revocation: implement the adapter's fresh read checks through the existing read-only accounts connection. Directory/account references remain logical across databases, not cross-database FKs. If a profile disappears after validation, future DS reads/reviews fail closed; central UI can show an assigned-but-ineligible target needing reassignment. Do not claim a distributed identity/complaint transaction.

### 3. One append-only C4 table

Exact proposed columns/checks/FKs/indexes are in PROPOSED_SCHEMA.sql: `complaint_assignment_events`. The row stores complaint, monotonic assignment revision, selected DS actor/office, assigning central actor/time/reason, submission key/input hash, previous event, trusted directory digest, authorization grant/source and exact canonical event bytes/SHA256. No original text/probabilities are duplicated into routing metadata. Populate actor/UTC audit metadata automatically at the C4 persistence boundary with a trusted scoped actor context; normalize UTC timestamps to PostgreSQL microsecond precision before canonicalization. Isolated verification must bind canonical event fields to typed columns and recompute SHA256, not only hash an unrelated JSON string.

Latest revision is the effective assignment; no row means Unassigned. Preserve all prior events on reassignment. PK/FK to complaint RESTRICT; same-complaint previous-event FK; unique complaint/revision and assigning-actor/submission-key; one-successor constraint; target lookup index. Actors/time/hash/provenance are resolved server-side. Same-key/same-content replay returns the saved event AND separately identifies the current assignment; a historical replay must never restore access to an earlier DS. Changed content conflicts409. New key with the same current target/office is rejected rather than adding a redundant reassignment.

No current-assignment cache table, existing-table column change, historical backfill, classifier change or extra audit table is proposed. EF migration name AddCentralComplaintAssignments; real timestamped ID/forward SQL only after schema approval. Down must refuse destructive removal. SQL is a proposal, not an applied/generated migration.

Exact additional runtime permission: SELECT,INSERT on this one table only, in PROPOSED_PERMISSIONS.sql. No new UPDATE/DELETE/DDL/schema/role/sequence/account/C1/C3/referral/outbox grants. Existing complaint CAS review/retry UPDATE rights remain unchanged.

### 4. Access semantics requiring explicit approval

-Applicant: same owner-only submission/detail/history/retry behavior. Blank references remain allowed; provided invalid references remain rejected.
-Central Read: all persisted registered complaints, linked/unlinked, assigned/unassigned, regardless of classifier success. Pending/failed classification remains visible as its actual state, never as a fabricated prediction. Central access is not authority to verify allegations or use DS triage actions.
-DS: only an active eligible DS identity that is the CURRENT explicit target of this complaint. Do NOT OR this with the old CaseWorkspaceReviewer fallback. Complaint access must not grant access to C3 case documents/workflow or parcel ownership.
-Existing records: all3existing rows without a new routing event are Unassigned. The currently case-visible linked complaint would need a deliberate central assignment to retain DS visibility under the new policy. This is a deliberate authorization change for approval, not a silent backfill. No original complaint/classification/review row is rewritten to simulate assignment.
-Reassignment: previous DS loses complaint reads/reviews/retry access; new eligible DS gains complaint scope only. Prior reviewer identity/history is preserved. No automatic reassignment from case IDs, parcel text or classification category.
-Enumeration: authenticate/authorize central capability before looking up complaint IDs; scope DS/applicant detail/history reads before exposing existence. Propose uniform404 for authenticated out-of-scope or missing complaint IDs, while missing central permission returns403 before lookup and anonymous returns401. This explicitly changes existing foreign/missing read denial distinctions; include it in the approved policy/tests rather than silently altering an API response contract.

Recheck grants/directory/current assignment before and after asynchronous reads. For assign/reassign and complaint review writes, use the same narrowly scoped complaint-key transaction lock at the C4 repository boundary, with expected assignment revision/unique revision conflict and existing expected complaint revision. This documented short-lock exception prevents a revoked DS review from racing a reassignment. It spans only local routing/review persistence; never hold it across ML/HTTP. Validate current DS authorization inside the review transaction before CAS update. Normal review payload/statuses remain unchanged.

Classifier registration/CAS/ML/result-link logic stays intact. An already-started classification can finish and remain saved for the applicant/current authorized readers; post-read authorization must deny a DS whose assignment changed while inference was running. Assignment itself never invokes ML. No automatic retries or workers.

### 5. Proposed APIs / UI

New controller under `/api/governance-intelligence/central-complaints`:
-GET `?assignment=unassigned|assigned&cursor=...`: bounded, server-filtered queue with actual classification/review/assignment summaries.
-GET `/{id}`: original complaint and actual saved classification, current assignment; central Read required.
-GET `/officers`: safe server-validated DS selector; central Assign required.
-GET `/{id}/assignments`: immutable event/history; central Read required.
-POST `/{id}/assignments`: submissionKey,expectedAssignmentRevision,targetActorId,reason only. Central Assign plus current Read required; server resolves all identities/times/office/authority metadata.

Integrate current explicit assignment into existing registered complaint list/detail/review/retry guards and queries; preserve applicant submission/classifier contracts. Avoid fetching only the first100global records and filtering afterward: apply each actor/queue scope before bounded pagination. Keep legacy registered-assessment reader protections. No automatic linking or inference calls on queue/detail/refresh.

Planned frontend `/component-04/central-complaints`: Unassigned/Assigned tabs, compact genuine list/detail and progressive assignment history; safe DS selector and explicit Assign/Reassign reason/revision. No fake queue is implemented now. Gate navigation/display from the server's capabilities, never frontend role text. Existing applicant and assigned-DS views remain, using updated server authorization only after approval.

## Verification and activation plan

Existing RegisteredComplaintTests ran9/9passing using existing mocks/EF InMemory. No new code/tests were authored. These establish current owner/case-reviewer denial, unlinked registration behavior, CAS/replay/cancellation/review preservation; they do NOT establish central routing. No new integration/browser/genuine inference proof is claimed.

After approval, isolated tests must cover actual controllers/repositories and restricted PostgreSQL: unlinked registration and one genuine classifier response using isolated fictional text; central reads; unauthorized DS/central capability failures; assignment to a trusted DS; other DS/applicant isolation; all ID/history/alternate-reader denials; reassignment immediate read/write revocation; review-versus-reassignment races; same-key replay/changed-input conflicts; concurrent initial assignments; current versus historic replay; grant expiry/directory revocation; invalid targets and reference validation; exact new grants; rollback and immutable historical payload fingerprints. Fixtures must be isolated; no ordinary complaint for testing.

Use the project's isolated PostgreSQL test convention. If the required container environment is unavailable, obtain an explicitly reviewed isolated alternative rather than claiming a container proof. Before ordinary activation: fresh backup/restore comparison for CURRENT3complaints, reviewed generated one-migration SQL/grants, exact actor/timebound configuration, complete runtime baseline, published tested package and controlled single-API replacement. Recheck concurrent records rather than overwriting them. Do not enable an old binary with new access settings as a fallback. Recovery disables the new routes/grants, revokes only new table SELECT/INSERT before older package use, retains the append-only history, and never runs destructive Down or restores over ordinary C4.

## Manual walkthrough

Current behavior remains: applicant portal can retrieve their saved complaints; assigned case reviewer can see the linked complaint. The two unlinked records are saved/classified but have no officer routing path. There is no functioning central queue or assignment button yet.

After approved implementation and isolated proof only: sign in as the explicitly granted central identity -> Unassigned Complaints -> open an actual saved item -> inspect original text and returned category/probabilities -> choose a trusted DS and reason -> Assign -> Assigned tab/history -> assigned DS can view/review -> another DS and other applicant remain denied -> central Reassign with latest revision/reason -> old DS denied/new DS allowed -> reload shows immutable assignment chain and unchanged original classifications. Refresh must never invoke classification. Actual ordinary authorization and IDs must be recorded after activation; none are fabricated here.

## Approval needed

Approve the one-table append-only design, exact SELECT/INSERT grant scope, explicit central Read/Assign permissions, server directory adapter, complaint-specific DS authorization (including removal of case-review fallback), scoped non-enumerating denials, and the short repository lock for assignment/review races, for C4-only implementation and isolated testing. Ordinary activation must have its own concrete reviewed backup, actor/expiry/settings and SQL checkpoint. No model/rule/C1/C2/C3/worker/blockchain change is included.

The stop comes directly from the attached request: ?STOP and present the exact proposed design for explicit approval before applying those changes.? The database skill `.agents/skills/database-development/SKILL.md` additionally states: ?Do NOT create migrations without an approved schema design.? No permission was inferred from a stored Commissioner or DS profile.
