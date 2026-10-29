# Current C4 history/form fix — manual checks

Verified API16456/5264, portal16692/3000,ML36612/8104. Inspect current listeners; do not duplicate or restart healthy services. Database is state_land_governance_c4/OID34144,41tables/21migrations. No SQL/grants/migration belong to startup.

```powershell
Set-Location 'D:\AI-Driven-Digital-Lease-Governance-for-State-Land-Management\AI-Driven-Digital-Lease-Governance-for-State-Land-Management'
Get-NetTCPConnection -State Listen -LocalPort 5264,3000,8104 -ErrorAction SilentlyContinue | Select-Object LocalPort,OwningProcess
Invoke-RestMethod http://localhost:5264/health
Invoke-RestMethod http://127.0.0.1:8104/health
# Only if5264is free:
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\StateLandGovernance\tools\development\Start-C4HistoryFormsApprovedApi.ps1 -CandidateEnvironmentPath '.\.tmp\c4-rename-backup-20261023_0840\renamed-target-environment.local.json' -ValidateOnly
# Only after successful validation, with port still free:
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\StateLandGovernance\tools\development\Start-C4HistoryFormsApprovedApi.ps1 -CandidateEnvironmentPath '.\.tmp\c4-rename-backup-20261023_0840\renamed-target-environment.local.json' -ApprovedActivation
```

Use only this current package-specific launcher. Reauthenticate after the API restart. Existing credentials/private settings are unchanged. ML/frontend startup remains their existing commands only if corresponding ports are free.

## Regulatory history — read existing records only

1. Sign in as the responsibleDS. Open `/component-04/assessments/history` and a saved Kiwula regulatory assessment other than `d4e358c1-148c-41e0-b80c-6c8899413056`. Prefer the latest real saved assessment. Do not rerun screening merely to demonstrate this fix.
2. Current **Regulatory follow-up** history/revision belongs only to that selected assessment; if no event exists, revision0/history0 is correct.
3. Below it, expand/inspect **Previous Regulatory Follow-ups**. The original assessment above should contain event `719c94ae-bf27-42a2-b98a-e17a374ce869`, marked **Historical — Read-only**, with original time, rule/version, remarks, missing information, references, officer and revision. Check the original-report link. No historical write form appears in that section.
4. Refresh previous follow-ups, reload the saved report and sign in again. The event remains discoverable and stays attached to the original assessment. Load more pages if offered. Switching proposal/assessment/account must clear old protected content; a different internal proposal must not show these events.
5. Commissioner retains explicit read-only report/history access. Another DS/applicant must not retrieve the protected anchor/history. Applicant complaint tracking remains outside C4. Do not record a new clarification/review for testing.

## DS complaint form — graphical checks still pending

1. Open `/component-04/complaint-inbox` as the currently assigned eligibleDS. Select an actual assigned complaint; do not assign or change lifecycle merely to make a test fixture.
2. Available action, Reason, Notes, Applicant-safe summary, optional Evidence references, Triage status and Triage Reason must each have the label above a full-width control, respecting card padding. Reason/summary/triage textareas should be at least100px high; notes112px. Helper text stays below its field; buttons align with the form.
3. Without submitting, inspect each action actually allowed by the server. Other states/terminal actions were rendered in automated tests; exercise business transitions only with isolated fictional data. Required fields, disabled/retained requests and terminal confirmations must behave as before.
4. On Commissioner `/component-04/central-complaints`, inspect the same shared form alignment and confirmation checkbox, without recording a decision. Internal reason/notes remain separate from applicant-safe summary; applicant history must show only the permitted public summary.
5. Check desktop, tablet and narrow/mobile widths, textarea resizing, no horizontal overflow, visible keyboard focus, logical tab order and native required-field validation. Browser inventory was empty, so these visual/keyboard checks are not claimed completed by automation. Do not submit an ordinary form as a fixture.

All ordinary records—including the existing clarification, complaint histories and model predictions—were verified unchanged. Automated tests passed:324backend/162frontend/7architecture/47isolated HTTP checks. Three unrelated C1 TypeScript errors remain. No staging/commit/push.
