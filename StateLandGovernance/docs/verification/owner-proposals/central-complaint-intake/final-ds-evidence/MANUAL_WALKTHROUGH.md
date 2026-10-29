# Final C4 manual demonstration

Current verified API35400/5264; portal16692/3000; ML36612/8104. Inspect listeners and reuse healthy services. Reauthenticate after API restarts.

```powershell
Set-Location 'D:\AI-Driven-Digital-Lease-Governance-for-State-Land-Management\AI-Driven-Digital-Lease-Governance-for-State-Land-Management'
Get-NetTCPConnection -State Listen -LocalPort 5264,3000,8104 -ErrorAction SilentlyContinue | Select-Object LocalPort,OwningProcess
Invoke-RestMethod http://localhost:5264/health
Invoke-RestMethod http://127.0.0.1:8104/health
# ONLY if5264is free:
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\StateLandGovernance\tools\development\Start-C4ManualEvidenceApprovedApi.ps1 -CandidateEnvironmentPath '.\.tmp\c4-rename-backup-20261023_0840\renamed-target-environment.local.json' -ValidateOnly
# After successful validation, only when API port is free:
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\StateLandGovernance\tools\development\Start-C4ManualEvidenceApprovedApi.ps1 -CandidateEnvironmentPath '.\.tmp\c4-rename-backup-20261023_0840\renamed-target-environment.local.json' -ApprovedActivation
```

1. Sign in at http://localhost:3000/sign-in as existing commissioner. Unchanged password; expected landing `/component-04/central-complaints`. Other roles retain existing fallback. Verify four queues, desktop two-column list/detail, classification/probabilities, eligible DS selector, handling controls and histories. Do not submit real actions merely for testing.
2. Existing DS: open `/component-04/assessments/history`, choose its own saved Early Governance officer report. Observe original findings, prominent Missing/Unverified/Verified counts and explanations. Those counts describe ORIGINAL findings, not later manual decisions.
3. On that report, Manual DS evidence verification loads only for current responsible DS or read-only authorized Commissioner. For genuine authorized business verification only: select one of seven concerns; attach supported actual evidence up to5MiB; save attachment; select attached evidence and/or enter genuine document references; choose Verified, Rejected or More Information Required; enter meaningful mandatory remarks; save. Same DS may attach and verify personally. Inspect revision/previous/new state/actor/time/history, then refresh. Existing automated findings and scores must stay unchanged. Correct with a NEW decision/current revision; never overwrite history. After uncertain response, refresh and deliberately replay the retained same request, not a new key.
4. Do NOT generate ordinary artificial evidence/decisions for demonstration. The complete fictional lifecycle is proven in isolated PostgreSQL; new ordinary tables were empty at activation. Commissioner view has no save/upload controls; other DS/applicant access denied. Only saved early requests owned by the DS are supported; no jurisdiction or C3 assignment is inferred.
5. Anomaly: open any saved workflow report at `/component-04/post-workflow` or History. Inspect large rounded score/threshold, actual flagged status, all9features/units, exact numeric values/model/artifact/input hashes in technical details. Score is not probability of misconduct. No Run/Retry needed.
6. Institutional Workflow Preview retains concise illustrative wording and expandable source/limitations; it submits no institutional action. Commissioner complaint UI preserves actual TakeDirect/DS assignment/investigation/decision operations, allowed only for genuine authorized work. No ordinary lifecycle action was used as a fixture.
7. Sign out/account-switch; applicant sees only its complaint/public history. DS stays creator/complaint-assignment-scoped. Commissioner report permission does not grant DS verification. Test keyboard Tab/Shift+Tab, labels/focus, errors/loading/uncertain replay, narrow mobile stacking and collapsed histories. These visual/keyboard checks remain manual; automated HTTP/SSR is not proof.

Read-only PostgreSQL metadata check, no credentials in commands:

```powershell
$env:PGOPTIONS='-c default_transaction_read_only=on'
try {
 & 'C:\Program Files\PostgreSQL\18\bin\psql.exe' -X -W -v ON_ERROR_STOP=1 -h localhost -p 5432 -U slg_c4_regulatory_runtime -d state_land_governance_c4 -f '.\StateLandGovernance\docs\verification\owner-proposals\central-complaint-intake\final-ds-evidence\VERIFY_READ_ONLY.sql'
} finally { Remove-Item Env:PGOPTIONS -ErrorAction SilentlyContinue }
```

Expected: sameOID34144,40tables/20migrations, no fabricated new-table rows. Genuine later verification changes row counts; never delete records to reproduce activation. SQL lists actual stored event/attachment metadata but omits remarks/content for privacy. All previous saved reports remain immutable. No migration/grant replay is part of normal startup. Do not use older launchers/packages.
