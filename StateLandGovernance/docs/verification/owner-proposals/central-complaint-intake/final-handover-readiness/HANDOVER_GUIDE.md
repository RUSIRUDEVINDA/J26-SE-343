# Current UI acceptance checkpoint — 2026-10-24

[Final history presentation polish](../final-history-ui-polish/COMPLETED_AND_VERIFIED.md) is frontend-only and active on the reused portal. Current versus previous records are distinct, technical metadata is optional, and existing forms/downloads/pagination remain. [Manual acceptance](../final-history-ui-polish/MANUAL_ACCEPTANCE.md) is pending before any selective staging/commit. API/package/launcher/private settings/database/models are unchanged; do not restart healthy services for this task. Prior deployment/handover evidence remains below.

---

# Current launcher and evidence-history update — 2026-10-24

Use `Start-C4EarlyEvidenceHistoryApprovedApi.ps1` and `.tmp/c4-early-evidence-history-api` for the necessary compatible restart, preserving the existing renamed-target candidate and both grant files. API11064/5264, portal16692/3000 and ML36612/8104 were healthy; inspect before reuse. Earlier launchers below are historical. No database deployment belongs to startup.

[Early Governance historical evidence is implemented and activated](../early-evidence-history/COMPLETED_AND_VERIFIED.md). The newer assessment remains independent; earlier original attachments/verification decisions are discoverable separately under authenticated per-assessment access. [Manual walkthrough](../early-evidence-history/MANUAL_WALKTHROUGH.md). Browser observations remain pending. No staging/commit/push; existing source reproducibility caveats below remain.

---

# Latest corrective status — evidence feedback, repeated sign-in and complaint history

2026-10-23. Frontend corrective changes are active; current ordinary storage has no Proposal1.pdf attachment, and the existing reference-only Legal Dispute verification is preserved. Start Investigation is saved on one complaint; two separate triage reviews belong to another. Upload/action acknowledgement and uncertainty are now distinguished, histories remain separate, and complaint return URLs survive signout and another login-header click. 182 frontend/35 backend/7 architecture tests pass, with 45 isolated PDF and 10 isolated handling checks and no inference. No ordinary writes, restart, migration, permission/model change, staging, commit or push. Commissioner execution is explicitly excluded from this request. Browser retests remain manual. See [current evidence and retest guide](../manual-test-corrections/COMPLETED_AND_VERIFIED.md). Older status below is historical.

---

# Latest request — applicant redirect fixed; Commissioner execution unresolved

2026-10-23. Both complaint-page Sign in entry points now preserve the exact allowlisted return destination. 173 frontend, 35 focused backend and 7 architecture tests pass. No backend/package/permission/database/model change or restart. Commissioner officer-assessment execution is not enabled: its independent execution authority and immutable input-source policy require resolution; current read authority and DS investigation ownership were not broadened. See [current status and concrete boundary](../auth-redirect-and-execution-review/STATUS_AND_EXECUTION_BOUNDARY.md). Browser click-through remains manual. No staging, commit or push. Earlier status below is historical.

---

# Latest C4 presentation status — final UI cleanup active

2026-10-23. Authorized C4 entry redirects to Proposals; Overview/numbered sidebar icons/tools disclosure are hidden. Named workflow punctuation, action alignment, Saved badges and sign-in presentation are corrected. 168 frontend and 7 architecture tests pass; authenticated read-only checks cover 53 saved reports and 12 DS evidence workspaces. API/package/database/models/permissions are unchanged; no restart, inference, staging, commit or push. Graphical checks remain manual; three existing C1 TypeScript errors remain. See [current cleanup evidence](../final-ui-cleanup/COMPLETED_AND_VERIFIED.md). Existing approved API startup instructions remain valid. Earlier status below is historical.

---

# Current startup and history/form correction

Previous Regulatory Follow-ups and DS complaint form alignment are now active. Current API launcher is Start-C4HistoryFormsApprovedApi.ps1; private candidate is unchanged. Database remainsOID34144,41tables/21migrations; existing records/follow-ups and grants preserved. See [current evidence](../history-and-ds-form-fix/COMPLETED_AND_VERIFIED.md) and [current startup/manual guide](../history-and-ds-form-fix/MANUAL_WALKTHROUGH.md). The older package-specific launchers below are historical and must not replace this correction. No migrations/grants/ordinary test records or inference were introduced. Manual graphical checks remain pending.

---

# Current deployment: regulatory follow-up active

The separately approved ordinary deployment is complete. Database state_land_governance_c4 retainsOID34144, now41tables/21migrations. Current launcher is Start-C4RegulatoryFollowupApprovedApi.ps1 with the unchanged private candidate and reviewed follow-up package; do not use the older launcher below as the current startup path. See [actual deployment evidence](../regulatory-followup-deployment/ACTIVATED_AND_VERIFIED.md) and [current startup/manual guide](../regulatory-followup-deployment/MANUAL_WALKTHROUGH.md). Fresh archive SHA2565f7c8362491e6195ab663bd091c5b5a296d3e34aa57e9213d02e423439040681 includes current ordinary additions. No ordinary follow-up decision was created for testing. Prior sections below retain earlier handover history and do not supersede this update. No staging/commit/push.

---

# Update: regulatory follow-up implemented and isolated-tested

The schema was explicitly approved in the subsequent user request. Its implementation, migration, frontend and isolated verification are now complete; ordinary deployment remains separately gated. See [current deployment checkpoint](../regulatory-followup-implementation/IMPLEMENTED_AND_DEPLOYMENT_CHECKPOINT.md).319backend/158frontend/7architecture tests and34HTTP checks pass. Ordinary remains40tables/20migrations; the additional table/migration exists only in the new isolated restore. A concurrent ordinary regulatory addition requires a fresh backup/restore before deployment. The earlier guide below describes the preserved prior deployment and remains historical; its statements that schema implementation is unapproved are superseded by this update. Do not use the old launcher to load the new package without a reviewed successor and explicit deployment approval.

---

# C4 handover — current working tree, follow-up feature pending

Do not call this a reproducible release commit yet. Branch `Component_03_VI`, HEAD `2aa0fcfeb931c066f2b17c29e286fbcac046de94` predates the uncommitted navigation/security and current stabilization changes. The index is empty. Review `CHANGED_FILES.json` and `WORKTREE_CLASSIFICATION.json` before any selective staging. Many prerequisite C4 files are still untracked; this commit alone does not contain the complete running system.

## Deliver separately and securely

- Fresh archive and checksum from `BACKUP_AND_PREFLIGHT.json`; original full private fingerprint/schema baseline is alongside that archive in ignored storage. Preserve older archives. The dump contains sensitive research/complaint information: never add it to Git or attach it publicly.
- A reviewed complete source snapshot after selective Git review, dependency lockfiles, approved compatible package and package hash manifest. Current API package is `.tmp/c4-navigation-access-api`, launcher `Start-C4NavigationAccessApprovedApi.ps1`. Its hash manifest is in the sibling `final-login-applicant-access` evidence folder. Frontend changes in this task are served from source by the existing Next development server.
- Current private candidate `.tmp/c4-rename-backup-20261023_0840/renamed-target-environment.local.json` and the TWO independent permission documents, by secure operator transfer. Do not copy passwords or bearer tokens into documentation. `SANITIZED_ENVIRONMENT.example.json` is explanatory, not a complete launchable environment or an authority grant.
- Existing account credentials through the owner's secure channel. No account/password was reset. Commissioner username remains `commissioner`; DS/applicant use their existing accounts. Persistent permissions remain separate from the 30-minute login session.
- Frozen trained artifacts and complete trusted bundle files at their verified locations, plus model metadata, threshold contract and checksums. Never load an untrusted joblib/pickle file. No retraining is needed.

## Dependencies and startup

API targets .NET 8 (`src/Api/StateLandGovernance.Api.csproj`); restore existing solution/package versions, do not upgrade as part of handover. Frontend uses Next 16.3.5 / React 19.2.8; use `npm.cmd ci` in `StateLandGovernance.Web` with its `package-lock.json`. Python requirements are `StateLandGovernance/src/Modules/GovernanceIntelligence/ML/requirements.txt`, including scikit-learn 1.7.2. Several Python dependencies are lower-bound ranges, so that file alone is not an exact historical environment lock; retain the supported existing venv/environment inventory when transferring. No dependency was changed here.

From the repository root, inspect listeners 5264/3000/8104 and health first. Reuse healthy services. Only if API5264 is free:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\StateLandGovernance\tools\development\Start-C4NavigationAccessApprovedApi.ps1 -CandidateEnvironmentPath '.\.tmp\c4-rename-backup-20261023_0840\renamed-target-environment.local.json' -ValidateOnly
# Start only after validation, with the port still free:
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\StateLandGovernance\tools\development\Start-C4NavigationAccessApprovedApi.ps1 -CandidateEnvironmentPath '.\.tmp\c4-rename-backup-20261023_0840\renamed-target-environment.local.json' -ApprovedActivation
```

Only if ML8104 is free, run the existing `StateLandGovernance/tools/development/Start-C4Ml.ps1`. Only if portal3000 is free, from `StateLandGovernance.Web`: `npm.cmd run dev -- --hostname 127.0.0.1 --port 3000`. Sign in again after API restart. Do not run migrations as startup; do not use an obsolete pre-security, pre-handling or pre-rename package.

These private paths/absolute target guards are machine-specific. Another owner machine needs an explicitly reviewed successor configuration/launcher; replacing paths without validation is not a reproducible handover procedure.

## Isolated restore and grants

Ordinary target remains localhost5432/state_land_governance_c4, OID34144, 40 tables/20 migrations. Never restore over it. The fresh verification already restored the archive into NEW isolated Docker `c4_handover_restore_20261023` on 57657. Test storage is retained. Use a different unused name for any further restore:

```powershell
$pg = 'C:\Program Files\PostgreSQL\18\bin'
$archive = '<secure absolute archive path from BACKUP_AND_PREFLIGHT.json>'
Get-FileHash -Algorithm SHA256 -LiteralPath $archive
& "$pg\pg_restore.exe" --list $archive
# Operator confirms this is the isolated loopback server and the NEW name is unused.
& "$pg\createdb.exe" -W -h 127.0.0.1 -p 57657 -U central_test c4_owner_restore_NEW_UNIQUE_NAME
& "$pg\pg_restore.exe" -W -h 127.0.0.1 -p 57657 -U central_test -d c4_owner_restore_NEW_UNIQUE_NAME --no-owner --no-privileges --exit-on-error $archive
```

The archive retains original ACL definitions. Skipping ownership/ACL execution avoids importing ordinary identities into the isolated cluster. Compare all tables, normalized schema/history and sequence state before testing. `CURRENT_RUNTIME_GRANTS_REFERENCE.sql` inventories the current restricted runtime rights; it is a review reference, not an authorized ordinary deployment script. Provision runtime identity/password securely on a new owner cluster, prohibit superuser/createdb/createrole/schema-create, review schema USAGE/table/column rights and exact database CONNECT. Adapt only an explicitly approved new restore target. Do not replace column-only CAS UPDATE with broad UPDATE; do not grant ordinary operator credentials to the API.

All 20 applied migration identities are captured in the private baseline/catalogue. Never replay them on the existing database. Regulatory follow-up has NO generated/applied migration yet; see `REGULATORY_FOLLOWUP_DESIGN_CHECKPOINT.md`. Recovery preserves append-only tables/history; stop only the verified API and fail closed rather than starting an incompatible older binary. Restore recovery archives only to another NEW isolated database.

## Model locations

- Complaint: `ML/models/v2/governance_classifier_v2.joblib`, model v2; SHA256 `63b37eb7175e74a8dcde82bf042fcf9dcbb4f0432fbe3b4677aac037007a20ae`.
- Anomaly: resolve `ML/anomaly_detection/bundles/workflow_anomaly_model_v1/local_inference_config.json` trusted model path to `ML/anomaly_detection/runs/isolation_forest_baseline_v1/isolation_forest.joblib`; SHA256 `59ed4a73d56d9f06798c4ebc4684a633afe72841c41ed0b3ba4e7bb658782b46`.
- Retain `threshold_and_feature_contract.json` beside the anomaly artifact. All nine features, threshold and saved scores remain unchanged. ML paths above are relative to the GovernanceIntelligence module. A score is not a probability of misconduct; complaint probabilities do not establish truth or legal liability.

## Manual demonstration — no ordinary fixtures

1. Anonymous: open `/workflow-governance/applicant/complaints`. Check readable Sign in button, then sign in as existing applicant. It should return to the complaint page. Without `returnTo`, applicant login still opens `/workflow-governance/applicant/cases`. Unsafe/non-allowlisted return destinations are ignored.
2. Applicant: inspect own saved classification/public timeline. Direct `/component-04` and nested report/evidence URLs must redirect to applicant dashboard. No governance shell/data is available. Do not create/retry a complaint for demonstration.
3. Commissioner: normal login opens `/component-04`; sidebar Complaint Inbox opens `/component-04/central-complaints`. Inspect the actual four queues and existing histories; do not assume every complaint remains Unassigned. No assignment, decision or review is required to demonstrate saved data.
4. History: open existing regulatory findings. Expand a finding to inspect stored requirement, reason, rule/version/source and recommended action. No exact missing document is inferred. Request Clarification / Record Human Review persistence is pending approved schema implementation, not available now. Deliberate reassessment already uses `/component-04/regulatory-assessments/new` with real updated structured facts and a new submission key/version; do not run it for presentation alone.
5. Anomaly: open a saved report. Verdict should be the prominent heading, scores smaller, all nine values/units and exact technical details preserved. Refresh performs reads only; no Run/Retry.
6. Responsible DS: own saved early officer assessment exposes attachment/verification controls and immutable history. Other DS denied; Commissioner read-only. Check corrected “20–2000” and “· revision” text; no ordinary evidence upload is needed.
7. Check keyboard focus, narrow layout, account switching and no stale protected content. These graphical checks remain manual. Current proof uses authenticated HTTP, tests and server rendering, not an automated browser.

Use `VERIFY_READ_ONLY.sql` in pgAdmin on the exact ordinary target to inspect saved findings, model outputs, evidence metadata and complaint history without private contents. Pending follow-up absence is shown explicitly rather than issuing a query against a nonexistent table.

## Selective review and remaining limitations

No staging/commit/push was performed. First review only `CHANGED_FILES.json` task delta against its before hashes; then separately review prerequisite C4 files and shared authentication/navigation already dirty before this task. Exclude unrelated C1/C2/C3 work, private profiles/credentials, `.env`, `.tmp`, dumps, model binaries, logs and generated files. Do not stage an entire directory or `git add .`. A final exact staging list requires reviewing the remaining prerequisite untracked files; no claim is made that this task's small diff alone reproduces the full active system.

Current tests: 312 backend, 156 frontend, 7 architecture passed. No full production frontend build is claimed: three existing C1 TypeScript errors remain. New follow-up persistence/end-to-end tests cannot run before approved schema implementation; no such result is fabricated. Full source reproducibility and graphical manual observations remain explicit handover limits.
