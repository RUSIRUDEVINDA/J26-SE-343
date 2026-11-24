# C4 routing/access manual verification

Current verified API19008/5264, portal16692/3000, ML36612/8104. PIDs are observations; inspect before reuse. No SQL/migrations/grants needed. Sign in again once after this deployment to establish the HttpOnly C4 route cookie; existing API sessions expired on restart.

1. http://localhost:3000/sign-in → existing commissioner credentials → **Governance Overview `/component-04`**. It must not automatically open complaints. Sidebar **Complaint Inbox** → **`/component-04/central-complaints`** → existing four queues/classification/DS assignment/handling/history. Do not submit business actions merely to verify presentation.
2. Sign out → existing applicant. Login must open `/workflow-governance/applicant/cases`. Root module selection and sidebar must omit C4 navigation. Direct `/component-04`, `/component-04/assessments`, `/component-04/central-complaints`, `/component-04/post-workflow` and saved-report/evidence URLs redirect to applicant dashboard without showing the governance shell. Applicant `/workflow-governance/applicant/complaints` remains available with own saved complaint/classification/public history only. Do not submit/retry a complaint as a fixture.
3. Sign out → existing DS. Existing fallback landing stays unchanged. DS C4 Complaint Inbox remains `/component-04/complaint-inbox` with complaint-specific assignment scope. History → its own saved early screening → manual evidence/history: existing eligibility and save/upload controls available only for responsible DS. Another DS cannot access it. Do not create artificial ordinary evidence/decisions. Commissioner can read existing evidence/history without DS write controls.
4. Commissioner and DS saved regulatory/conflict/early/anomaly reports retain creators/results/provenance. Refresh retrieves records; do not Run/Retry. Account switching clears old route cache via full login navigation. Test keyboard/narrow layout/loading/errors and no stale protected content. These graphical observations remain manual; HTTP tests already prove server/API denial and route readiness.

```powershell
Set-Location 'D:\AI-Driven-Digital-Lease-Governance-for-State-Land-Management\AI-Driven-Digital-Lease-Governance-for-State-Land-Management'
Get-NetTCPConnection -State Listen -LocalPort 5264,3000,8104 -ErrorAction SilentlyContinue | Select-Object LocalPort,OwningProcess
Invoke-RestMethod http://localhost:5264/health
Invoke-RestMethod http://127.0.0.1:8104/health
# Only if5264is free:
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\StateLandGovernance\tools\development\Start-C4NavigationAccessApprovedApi.ps1 -CandidateEnvironmentPath '.\.tmp\c4-rename-backup-20261023_0840\renamed-target-environment.local.json' -ValidateOnly
# Only after validation and with port still free:
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\StateLandGovernance\tools\development\Start-C4NavigationAccessApprovedApi.ps1 -CandidateEnvironmentPath '.\.tmp\c4-rename-backup-20261023_0840\renamed-target-environment.local.json' -ApprovedActivation
```

Do not restart healthy services. Do not use older pre-security/pre-rename launchers. Current API/package use unchanged private settings, password and grants; no DB deployment operation belongs to startup. No staging/commit/push was performed; review the completed diff only.
