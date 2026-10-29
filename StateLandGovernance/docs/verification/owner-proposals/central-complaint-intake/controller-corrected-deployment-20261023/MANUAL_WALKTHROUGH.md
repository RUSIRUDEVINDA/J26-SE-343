# C4 final PP1 startup and browser walkthrough

API5264/PID18396, frontend3000/PID16692, ML8104/PID36612 were healthy at verification. Inspect current listeners; do not duplicate processes. Database is permanently `state_land_governance_c4`, OID34144. No SQL/migration/restore is part of startup.

```powershell
Set-Location 'D:\AI-Driven-Digital-Lease-Governance-for-State-Land-Management\AI-Driven-Digital-Lease-Governance-for-State-Land-Management'
Get-NetTCPConnection -State Listen -LocalPort 5264,3000,8104 -ErrorAction SilentlyContinue |
    Select-Object LocalPort,OwningProcess
Invoke-RestMethod http://localhost:5264/health
Invoke-RestMethod http://127.0.0.1:8104/health
```

Only when5264is free, use this final compatible launcher:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\StateLandGovernance\tools\development\Start-C4ControllerCorrectedApprovedApi.ps1 -CandidateEnvironmentPath '.\.tmp\c4-rename-backup-20261023_0840\renamed-target-environment.local.json' -ValidateOnly
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\StateLandGovernance\tools\development\Start-C4ControllerCorrectedApprovedApi.ps1 -CandidateEnvironmentPath '.\.tmp\c4-rename-backup-20261023_0840\renamed-target-environment.local.json' -ApprovedActivation
```

Only when8104is free, run the existing Start-C4Ml.ps1 in another session. Only when3000is free, from StateLandGovernance.Web run `npm.cmd run dev -- --hostname 127.0.0.1 --port 3000`. Do not restart healthy frontend/ML. Never use old pre-rename/timed/pre-correction launchers.

## Manual browser checks — still pending observation

1. Open http://localhost:3000/sign-in; use existing `commissioner` username and unchanged password. Retrieve the documented local development credential yourself from DemoAccountProfiles.cs Password constant if needed; no password reset was made. Sign in again after any API restart.
2. Open http://localhost:3000/component-04. Inspect the navigation: Proposals, Regulatory Screening, Conflict Detection, Early Governance, Institutional Workflow Preview, Post-Workflow Anomaly, Central Complaints and History.
3. Proposals `/component-04/assessments`: details start collapsed; View Proposal Data opens/closes the factual/source/assumption information. Viewing must not submit screening. Regulatory Screening `/component-04/assessments/regulatory` remains a separate form; do not Run merely for deployment testing.
4. History `/component-04/assessments/history`: open existing saved regulatory/conflict/early/anomaly reports. Commissioner can read saved cross-creator results; creator/source/model remain original. Exact33saved URLs are in SAVED_REPORT_URLS.json. Open the previously failing officer report http://localhost:3000/component-04/officer-assessments/29f53e56-9e9a-4e04-9ed3-350d1a7518d3 and refresh. GET/reopen does not run the engine.
5. Post-Workflow `/component-04/post-workflow`: open a saved report, inspect nine values/units and separate model/score/threshold details. Do not press Run or Retry. Workflow preview remains illustrative, not a real C3 execution or official approval.
6. Central Complaints `/component-04/central-complaints`: inspect all four queues, original saved text/classification, allowed entry controls and history. All existing complaints remain unassigned; do not TakeDirect/Assign/Investigate/Resolve/Close as test actions. Full handling paths were tested in isolation.
7. Sign out; existing DS retains its own saved-report scope and complaint-assignment policy. Unassigned complaint inbox stays empty; central permission is not granted to DS. Another applicant/Officer cannot see Commissioner cross-creator reports. Sign back in as actual applicant owner at `/workflow-governance/applicant/complaints`: original classification/public-only timeline remain; internal actor/notes/evidence fields must not appear.
8. Test sign-out/account switching, browser refresh, loading/error display, keyboard focus, narrow/mobile layout and collapsed histories. Record actual visual observations. HTTP200/automated tests do not establish these browser checks. No new ordinary complaint, decision or model inference is necessary.

## Read-only PostgreSQL verification

Use the existing restricted runtime password at the prompt. No credentials in shell commands.

```powershell
$env:PGOPTIONS = '-c default_transaction_read_only=on'
try {
  & 'C:\Program Files\PostgreSQL\18\bin\psql.exe' -X -W -v ON_ERROR_STOP=1 `
    -h localhost -p 5432 -U slg_c4_regulatory_runtime -d state_land_governance_c4 `
    -c 'SELECT current_database(),current_user,current_setting(''transaction_read_only''); SELECT oid FROM pg_database WHERE datname=current_database(); SELECT count(*) AS migrations FROM governance_intelligence.__ef_migrations_history; SELECT count(*) AS assignment_events FROM governance_intelligence.complaint_assignment_events; SELECT count(*) AS handling_events FROM governance_intelligence.complaint_handling_events;'
} finally { Remove-Item Env:PGOPTIONS -ErrorAction SilentlyContinue }
```

Expected at verification: OID34144,19migrations, zero assignment/handling events. Later genuine authorized business actions may change counts; never delete them to reproduce this snapshot. All38table fingerprint/ACL/preservation evidence is in FINAL_PRESERVATION_AND_PROCESS.json. Frozen results and creator associations were verified through33real saved reports, with no rerun.

C4 is frozen for PP1. Manual browser observations remain pending; no further C4 feature work is proposed.
