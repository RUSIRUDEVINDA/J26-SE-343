# Activated C4 regulatory follow-up — startup and manual verification

Verified API13300/5264, portal16692/3000 and ML36612/8104. Inspect current listeners before reuse; these PIDs are observations. Database remains state_land_governance_c4/OID34144, now41tables/21migrations. No migration or restore belongs to normal startup.

```powershell
Set-Location 'D:\AI-Driven-Digital-Lease-Governance-for-State-Land-Management\AI-Driven-Digital-Lease-Governance-for-State-Land-Management'
Get-NetTCPConnection -State Listen -LocalPort 5264,3000,8104 -ErrorAction SilentlyContinue | Select-Object LocalPort,OwningProcess
Invoke-RestMethod http://localhost:5264/health
Invoke-RestMethod http://127.0.0.1:8104/health
```

Only when5264is free, use the current successor, never an obsolete launcher:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\StateLandGovernance\tools\development\Start-C4RegulatoryFollowupApprovedApi.ps1 -CandidateEnvironmentPath '.\.tmp\c4-rename-backup-20261023_0840\renamed-target-environment.local.json' -ValidateOnly
# Only after validation, with port still free:
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\StateLandGovernance\tools\development\Start-C4RegulatoryFollowupApprovedApi.ps1 -CandidateEnvironmentPath '.\.tmp\c4-rename-backup-20261023_0840\renamed-target-environment.local.json' -ApprovedActivation
```

Only if8104is free, run the existing Start-C4Ml.ps1. Only if3000is free, from StateLandGovernance.Web run `npm.cmd run dev -- --hostname 127.0.0.1 --port 3000`. Reuse healthy services. Sign in again after the API restart; persistent permissions do not make sessions permanent. No account/password was changed.

## Browser observations — still manual

1. Existing responsibleDS: sign in at http://localhost:3000/sign-in; open http://localhost:3000/component-04/assessments/history and an existing owned regulatory report. Follow-up history is currently empty. Original stored findings, source/version and input provenance remain visible.
2. Regulatory follow-up permits choosing a saved InsufficientInformation finding for Request Clarification, or a saved human-review finding for Record Human Review. Inspect available fields, required20–2000character text and references, without submitting a fictional ordinary action. Clarification is internal only; no delivery claim. Human review is separate from original findings and does not grant approval.
3. For a future genuine authorized action only: enter actual missing information/request or reasoned review/outcome and references, then deliberately save. Inspect eventID/actor/UTC/rule/version/revision and unchanged original outcome. Refresh/reopen reads history only. An uncertain save retains its exact key for deliberate replay; a confirmed400/409 rejection requires reviewing current history before a new deliberate submission.
4. Commissioner: login still opens `/component-04`; Complaint Inbox opens `/component-04/central-complaints`. Saved regulatory follow-up history is readable without write controls. Existing central queues/classifications/handling histories and other saved reports remain. Do not submit complaint decisions for testing.
5. OtherDS cannot access another creator's regulatory follow-up/evidence. ResponsibleDS retains own early screening/evidence controls; Commissioner evidence is read-only. No artificial ordinary evidence is needed.
6. Applicant login/complaint return remains outside C4. Direct C4 URLs redirect to applicant dashboard; complaint ownership/public-only history remains. Check sign-out/account switching, keyboard/focus, loading/errors and narrow layout. These visual observations were not automated; authenticated HTTP is not click-through proof.
7. Saved anomaly reports keep prominent verdict, nine features, exact technical scores and frozen model provenance. Do not Run/Retry or rerun regulatory screening to demonstrate retrieval.

## Read-only PostgreSQL

Use the existing restricted runtime password at the prompt. No password/token belongs in a command or report.

```powershell
$env:PGOPTIONS='-c default_transaction_read_only=on'
try {
  & 'C:\Program Files\PostgreSQL\18\bin\psql.exe' -X -W -v ON_ERROR_STOP=1 -h localhost -p 5432 -U slg_c4_regulatory_runtime -d state_land_governance_c4 -f '.\StateLandGovernance\docs\verification\owner-proposals\central-complaint-intake\regulatory-followup-implementation\VERIFY_FOLLOWUPS_READ_ONLY.sql'
} finally { Remove-Item Env:PGOPTIONS -ErrorAction SilentlyContinue }
```

At deployment:21migrations and0follow-up events. Later genuine authorized actions may change event counts; never delete history to reproduce this snapshot. Other saved-engine verification is in the earlier final-handover-readiness/VERIFY_READ_ONLY.sql. Both queries use actual columns and omit private contents.

Verify the new secure backup:

```powershell
Get-FileHash -Algorithm SHA256 -LiteralPath '.tmp\c4-followup-deployment-backup-20261023\state_land_governance_c4.dump'
```

Expected `5f7c8362491e6195ab663bd091c5b5a296d3e34aa57e9213d02e423439040681`. Do not restore over ordinary storage. All private candidate/permission documents, credentials and archive remain outside Git. No staging/commit/push was performed.
