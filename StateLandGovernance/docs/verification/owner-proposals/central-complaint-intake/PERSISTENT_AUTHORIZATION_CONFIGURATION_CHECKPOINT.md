# Persistent Commissioner authorization — revised activation approval checkpoint

2026-10-23, Asia/Colombo. **Implemented and isolated-tested; ordinary activation STOPPED for the separate approval expressly required in the attached final-completion request, section5.** No ordinary permission, schema, private runtime file, account, password or API deployment has changed. This replaces only the proposed timed authorization/deployment configuration; previous checkpoints and backup evidence remain preserved.

## Smallest authorization adjustment

Existing sign-in, password verification, JWT/session validation, roles and identities remain unchanged. Persistent C4 permission is separate from the existing30-minute login session: users still sign in normally and reauthenticate after API restarts/session expiry.

`CentralComplaintGrant.ExpiresAtUtc` is now optional. Omission alone grants nothing: `Persistent=true` is required, alongside a fresh server-owned permission source. An explicitly persistent grant can optionally retain a real expiry; existing timed grants keep their current expiry behavior. No distant expiry date is used. `Revoked=true`, removed grants, expired grants, duplicate/malformed definitions and wrong/currently ineligible identities deny authority. A standalone module without the fresh permission-source adapter cannot use a persistent grant.

The Api host implements the C4 permission-source port through a bounded, private JSON file read on **every authorization check**, including existing pre/post-read and transaction rechecks. No watcher/cache/restart delay or browser permission claims. File errors fail503 without falling back to old grants; an explicitly empty/revoked grant set denies403. Mixing a file with old inline grants fails closed. The file uses schema`c4-central-permissions/1`, rejects unknown/duplicate JSON properties, duplicate actors/IDs and oversized input. C4 Application has no file/configuration/host dependency.

The fresh read-only account/directory adapter still requires the exact matching current Officer/COMMISSIONER profile. Explicit `IsActive=false` or `Disabled=true` also denies C4 eligibility; malformed markers fail closed. Existing stored profiles without these optional markers retain their legacy active interpretation. No account marker was added or changed in ordinary storage; the isolated tests supply inactive fixtures. A previously issued valid JWT confers no central access when the current grant or account eligibility is revoked. Requests already past their last authorization check cannot be retroactively cancelled; each subsequent check observes the new file. No distributed account/file/complaint transaction is claimed.

Existing assignment/handling events already persist actor, operation, grant UUID and source. The source now includes SHA256 of the exact read permission-document bytes; it participates in existing canonical event hashes. No new audit table, migration, model/engine or blockchain processing is needed. Existing history is never rewritten.

## Exact revised configuration — NOT ENABLED

[PERSISTENT_PROPOSED_GRANTS.json](PERSISTENT_PROPOSED_GRANTS.json) contains exactly ONE proposed grant:

| Field | Reviewed value |
|---|---|
| Existing ActorId | `0ab669fe-d0ab-5695-ad55-10f6b3b5aab3` |
| New GrantId | `46b1f5ec-0c85-4e17-97f1-6478f4183481` |
| Institution/profile | exact Officer / COMMISSIONER |
| Permissions | C4ComplaintCentralRead, C4ComplaintCentralAssign, C4ComplaintCentralReview |
| ReviewOperations | TakeDirect, AssignDs, StartInvestigation, RecordNote, RequestInformation, RecordFindings, Resolve, Close, Reopen |
| ValidFromUtc | `2026-10-23T00:00:00Z` — provisioning lower bound; not a recurring window |
| Persistent / Revoked | `true` / `false` |
| ExpiresAtUtc | absent; no expiry workaround |
| SourceReference | `central-complaint-intake/PERSISTENT_AUTHORIZATION_CONFIGURATION_CHECKPOINT.md; separate explicit activation approval required` |

The existing Commissioner actor is explicitly selected; no other Commissioner, Officer or DS is automatically authorized. This is an unissued review proposal, not proof of appointment or successful ordinary login.

[PERSISTENT_PROPOSED_SETTINGS.json](PERSISTENT_PROPOSED_SETTINGS.json) replaces the old inline timed-grant section with exactly:

```text
GovernanceIntelligence__CentralComplaints__Enabled=true
GovernanceIntelligence__CentralComplaints__WritesEnabled=true
GovernanceIntelligence__CentralComplaints__GrantFilePath=D:\AI-Driven-Digital-Lease-Governance-for-State-Land-Management\AI-Driven-Digital-Lease-Governance-for-State-Land-Management\.tmp\central-persistent-authorization-review-20261023\central-permissions.NOT_ENABLED.local.json
```

No old CentralComplaints Grants keys are merged into this candidate. All existing non-central keys/credentials/read-only peer/account settings/disabled sandboxes and worker settings are preserved byte-for-value from the original private environment.

The never-loaded candidate is `.tmp/central-persistent-authorization-review-20261023/candidate-environment.NOT_ENABLED.local.json`. The two new files are in a new directory restricted to the current account and SYSTEM. Existing private files are untouched. Their NOT_ENABLED names reflect current review status; loading these exact reviewed paths after approval does not require silently renaming or changing configuration. [PERSISTENT_PRIVATE_CONFIGURATION_BASELINE.json](PERSISTENT_PRIVATE_CONFIGURATION_BASELINE.json) records the exact delta without credentials.

## Exact launcher/package diff

Old timed launcher and package are retained as historical evidence. Use only the NEW [Start-CentralComplaintsPersistentApprovedApi.ps1](../../../../tools/development/Start-CentralComplaintsPersistentApprovedApi.ps1) after separate approval:

- Package changes from `.tmp/central-preparation-published-api` to `.tmp/central-persistent-review-published-api`;88reviewed files in [PERSISTENT_PACKAGE_SHA256.json](PERSISTENT_PACKAGE_SHA256.json).
- Manifest/config baseline changes to PERSISTENT_PACKAGE_SHA256 / PERSISTENT_PRIVATE_CONFIGURATION_BASELINE.
- Removes mandatory24–25Oct window checks; validates exact private permission-file path/schema/one reviewed actor, exact permissions/operations/source, explicit Persistent and absence of an expiry workaround.
- Allows the intentional `Revoked` boolean to become true without silently authorizing different actors or operations. This permits restarting the compatible package with central access revoked while other features remain available. It does not validate an older binary as safe recovery.
- Retains package hashes, exact candidate-key delta, original private-file hash, runtime database identity, C1/C3/accounts read-only protection, disabled workers/sandboxes, duplicate5264 refusal and explicit `-ApprovedActivation` guard. `-ValidateOnly` loads no process environment and starts nothing.

Actual ValidateOnly passed. Full Release publish passed. [PERSISTENT_CHANGED_FILES.json](PERSISTENT_CHANGED_FILES.json) and [PERSISTENT_SOURCE_SHA256.json](PERSISTENT_SOURCE_SHA256.json) give exact implementation/test/tool paths and hashes. Review these together with the unchanged two SQL files; do not treat pre-existing unrelated working-tree changes as this task's edits.

## Tests and genuine preservation evidence

- **258 focused backend tests pass**,0failed/skipped; includes17new persistent-policy cases, existing complaint/handling/officer/workflow gates and dedicated Docker PostgreSQL regressions.
- **139 frontend tests and6architecture tests pass.** Frontend source/styling unchanged; architecture includes host ownership of the fresh permission-source adapter.
- **13 isolated real-login/capability/revocation HTTP checks pass.** Uses the existing DevelopmentAccounts password verifier, JWT and live-session validation with newly created isolated profiles only. Tests authorized login and capabilities; ungranted Commissioner, DS, applicant and anonymous denial; same-JWT grant revocation/removal/expiry; fresh inactive account metadata; malformed document fail-closed; all nine actual policy operations and permission-document provenance.
- **5 isolated persisted-operation checks pass.** A newly seeded fictional fixture only: TakeDirect under a persistent grant, stored grant/source/document digest and canonical hash, live revocation preventing the next write, no extra history and unchanged classification count. Fixture`d80f40a9-524c-4d96-b68d-a2df14f15b49`; event`4c7eb364-9915-4966-91e3-243fb434d26e`. These are isolated IDs, not ordinary reports.
- **9 final isolated saved-retrieval checks pass** under persistent-source authorization. Original saved handling history and actual modelv2 classification are retrieved without new inference or database writes. No new classification/anomaly inference occurred anywhere in this authorization task.
- Existing complete handling-path/concurrency/rollback/restore proofs remain preserved; no expensive full backup/restore was repeated. The proof apphost hit Windows path-length limits and was run from a short ignored output directory; a test-local variable collision was corrected before the final successful operation proof. These did not require an application/schema/grant scope expansion.

Evidence: [PERSISTENT_LOGIN_AND_REVOCATION_EVIDENCE.json](PERSISTENT_LOGIN_AND_REVOCATION_EVIDENCE.json), [PERSISTENT_OPERATION_AUDIT_EVIDENCE.json](PERSISTENT_OPERATION_AUDIT_EVIDENCE.json), [PERSISTENT_SAVED_RETRIEVAL_EVIDENCE.json](PERSISTENT_SAVED_RETRIEVAL_EVIDENCE.json), [PERSISTENT_TEST_AND_PRESERVATION_SUMMARY.json](PERSISTENT_TEST_AND_PRESERVATION_SUMMARY.json).

Fresh SELECT-only [PERSISTENT_ORDINARY_READ_ONLY_EVIDENCE.json](PERSISTENT_ORDINARY_READ_ONLY_EVIDENCE.json) confirms17ordinary migrations unchanged, both new tables absent, all three original complaint/classification fingerprints unchanged and exact Commissioner profile pairing. The existing archive SHA256 remains`ab6780784f2cf36b405b33c81d9e1a81da5e215f7cbcdc0418b61f988da433d4`. Original private configuration and both reviewed migration/grant SQL files are unchanged. Frozen complaint/anomaly artifact hashes were rechecked. No ordinary login, permission issuance, API deployment or complaint action occurred.

No browser tool is available; ordinary authenticated/browser checks remain after approval. Three existing C1 TypeScript errors and NuGet feed warnings remain; no green whole-repository production-build claim. Persistent C4 permission does not remove existing in-memory-session/multi-instance authentication limitations. Those are outside this PP1 authorization change.

## Revocation and recovery

After separately approved activation, a trusted server operator can atomically replace the private grant document with the same grant marked`Revoked:true`, or remove it. Do not alter passwords/accounts. Preserve the prior document securely with an operator change reference; use a new grant UUID/source when provisioning materially different authority. No frontend can perform this change. Example future server-side revocation, **not executed on the ordinary candidate**:

```powershell
$grantFile = 'D:\AI-Driven-Digital-Lease-Governance-for-State-Land-Management\AI-Driven-Digital-Lease-Governance-for-State-Land-Management\.tmp\central-persistent-authorization-review-20261023\central-permissions.NOT_ENABLED.local.json'
$document = Get-Content -LiteralPath $grantFile -Raw | ConvertFrom-Json
$document.Grants[0].Revoked = $true
$next = Join-Path (Split-Path -Parent $grantFile) ('revocation-' + [guid]::NewGuid().ToString('N') + '.json')
[IO.File]::WriteAllText($next, ($document | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
$previous = Join-Path (Split-Path -Parent $grantFile) ('permissions-before-revocation-' + [guid]::NewGuid().ToString('N') + '.json')
[IO.File]::Replace($next, $grantFile, $previous)
```

Fresh checks deny the revoked actor without restart; existing immutable event hashes retain the permission-document digest used at the time. An already-running request cannot be retroactively cancelled after its last check.

For deployment failure, use the tested fail-closed procedure: stop the new API, disable central candidate settings/revoke permission, revoke ONLY new-table SELECT/INSERT before considering any older package, retain both tables/migrations/history and keep API stopped until a compatible recovery package is reviewed. An incompatible old case/assignment-only API is NOT safe rollback. Do not restore over ordinary storage or run destructive Down. The prior stopped-API recovery has explicit downtime; no untested high-availability guarantee is made.

## Approval required, then exact activation sequence

Section5 of the user's attached request explicitly requires this stop for the material timed→persistent configuration change. **Approve this exact actor/grant/file/settings/new package and launcher before ordinary activation.** Approval must not include ordinary test complaints or real handling actions; those still need specific authorization.

After approval: revalidate [PERSISTENT_REVIEW_SHA256.json](PERSISTENT_REVIEW_SHA256.json), current source/package hashes, original archive checksum/readability, ordinary history/schema/grants/all36table fingerprints and protected settings/artifacts. If ordinary data changed, create/verify a new backup and isolated restore rather than overwrite it. Then stop any identified ordinary API; apply ONLY`20261022200907_AddCentralComplaintAssignments` and`20261022205550_AddComplaintHandlingEvents` through unchanged COMBINED_REVIEWED_FORWARD.sql and unchanged COMBINED_REVIEWED_ADDITIONAL_PERMISSIONS.sql. No new migration or privilege delta is introduced by persistent authorization. Verify preservation before starting exactly one reviewed compatible package with the new candidate. Preserve/reuse existing services; do not duplicate or start unrelated ones.

Preparation validation command (already passed, no activation):

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\StateLandGovernance\tools\development\Start-CentralComplaintsPersistentApprovedApi.ps1 -CandidateEnvironmentPath '.\.tmp\central-persistent-authorization-review-20261023\candidate-environment.NOT_ENABLED.local.json' -ValidateOnly
```

The same launcher accepts `-ApprovedActivation` **only after separate explicit approval and successful immediate preflight/SQL checks**. Do not use the old timed launcher. Full ordinary host startup/login/central inbox, existing engines/report retrieval, denial checks, public-history privacy, DB persistence and manual browser checks are the activation-time gates listed in [FINAL_ACTIVATION_PREPARATION_GO_NO_GO.md](FINAL_ACTIVATION_PREPARATION_GO_NO_GO.md), with this persistent configuration replacing its obsolete time window. No new model call is needed for retrieval verification; do not mutate ordinary complaints as fixtures.

**Current status: implementation ready for PP1; persistent configuration awaiting explicit activation approval; ordinary activation and final ordinary end-to-end completion NOT claimed.**
