# Activated C4 — startup and manual checks

Current services: API5264/PID27588, portal3000/PID16692, ML8104/PID36612. PIDs are activation-time observations; inspect current listeners before use. Reuse healthy services. No complaint/model test submission is needed.

```powershell
Set-Location 'D:\AI-Driven-Digital-Lease-Governance-for-State-Land-Management\AI-Driven-Digital-Lease-Governance-for-State-Land-Management'
Get-NetTCPConnection -State Listen -LocalPort 5264,3000,8104 -ErrorAction SilentlyContinue |
    Select-Object LocalPort,OwningProcess
Invoke-RestMethod http://localhost:5264/health
Invoke-RestMethod http://127.0.0.1:8104/health
```

Only if5264is free, start the reviewed **startup-corrected** package. Retain the private candidate/permission file and package in ignored storage; no root.env rewrite is necessary. Do not use the obsolete Kiwula/timed/pre-correction launcher.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\StateLandGovernance\tools\development\Start-CentralComplaintsStartupCorrectedApprovedApi.ps1 -CandidateEnvironmentPath '.\.tmp\central-persistent-authorization-review-20261023\candidate-environment.NOT_ENABLED.local.json' -ValidateOnly
# Only when5264is free; ordinary activation is approved/applied. Do not replay SQL.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\StateLandGovernance\tools\development\Start-CentralComplaintsStartupCorrectedApprovedApi.ps1 -CandidateEnvironmentPath '.\.tmp\central-persistent-authorization-review-20261023\candidate-environment.NOT_ENABLED.local.json' -ApprovedActivation
```

Only if8104is free, in another session:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\StateLandGovernance\tools\development\Start-C4Ml.ps1
```

Only if3000is free, in another session:

```powershell
Set-Location .\StateLandGovernance.Web
npm.cmd run dev -- --hostname 127.0.0.1 --port 3000
```

After an API restart, sign in again. Persistent permissions do not make existing login sessions permanent.

## Browser walkthrough — visual checks still pending

1. Open http://localhost:3000/sign-in and use the existing Commissioner account/password. No credential changed. Open http://localhost:3000/component-04/central-complaints. Confirm four queues and server-provided navigation/capabilities.
2. All three preserved complaints currently remain Unassigned. Open each to inspect actual original text, returned category/probabilities and separate classifier/review/handling states. Expand history: no assignment or handling events were fabricated. Confirm TakeDirect and AssignDs entry controls appear, but do not submit them merely for testing.
3. Sign in as an existing DS. http://localhost:3000/component-04/complaint-inbox is currently empty until a genuine authorized central assignment. This is the approved complaint-specific policy, not missing database data. A C3 case-review assignment alone no longer gives complaint access. Direct unassigned complaint reads are denied404.
4. Sign in as the actual applicant owner. http://localhost:3000/workflow-governance/applicant/complaints must retain the saved original/classification and public-only handling history, without private actor/notes/evidence fields. Do not submit/retry a complaint or change review merely for presentation.
5. Existing DS-created research reports remain under their actual creator scope at http://localhost:3000/component-04/assessments/history, regulatory report links and http://localhost:3000/component-04/post-workflow. Central complaint permission does not automatically confer research-report ownership. Refresh/reopen saved reports only; no Run/Retry is required. Actual saved IDs/API paths are in ORDINARY_ACTIVATION_RUNTIME_EVIDENCE.json.
6. Check sign-out/account-switch privacy, loading/errors, keyboard focus, collapsed history and narrow layouts. No browser automation was available, so record these visual observations manually. HTTP readiness alone does not establish them.

For later real business handling only: TakeDirect→investigation→notes/findings/information requests→reasoned central resolution→explicit closure/reopening; or explicit DS assignment→scoped DS investigation/findings→central decision. Respect current handling/assignment revisions. Reassignment/takeover/terminal decisions revoke former DS scope; reopening never silently restores an old assignment. These lifecycle paths were exercised in isolated tests, not on ordinary complaints.

## Read-only PostgreSQL check

Use the existing restricted runtime password at the prompt; no credentials in commands. These commands read metadata/counts only and reveal no complaint text.

```powershell
$env:PGOPTIONS = '-c default_transaction_read_only=on'
try {
  & 'C:\Program Files\PostgreSQL\18\bin\psql.exe' -X -W -v ON_ERROR_STOP=1 -h localhost -p 5432 -U slg_c4_regulatory_runtime -d c4_handover_local -f '.\StateLandGovernance\docs\verification\owner-proposals\central-complaint-intake\ACTIVATED_VERIFY_READ_ONLY.sql'
} finally { Remove-Item Env:PGOPTIONS -ErrorAction SilentlyContinue }
```

Expected at activation:19migrations,0assignment events and0handling events. Genuine later authorized business actions may change counts; never delete them to reproduce this baseline. No migration/backup restore is part of normal startup. For revocation/recovery, use the persistent configuration checkpoint and activated report; never start an incompatible old API.
