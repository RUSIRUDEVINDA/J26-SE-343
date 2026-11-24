# Early Governance historical evidence — manual check

Reuse healthy API5264/PID11064, portal3000/PID16692 and ML8104/PID36612; PIDs are observations. Sign in again after the API restart. No SQL/migration/restore or Run/Retry is needed.

1. Sign in as the existing responsible DS. Open http://localhost:3000/component-04/officer-assessments/3ae73d0e-0271-485a-b307-52d603019679 . **Current Assessment** must have zero attachments/history and independent Unverified/revision0.
2. Below it, inspect **Previous Early Governance Evidence and Verifications**. Group request `284e96a3-6744-45b4-8777-7f062336f7d5` is **Historical — Read-only**, with State Land Development Proposal Form.png (1,487,486 bytes), Legal Dispute Verified/revision1, original remarks/references/actor/time. It has no upload or Save decision control. The separate earlier reference-only decision remains grouped under its own request.
3. Download the PNG with its authenticated button. Compare SHA256 `3752e6c4b98edffc8742fa6e64c9046f59c4fceb0a18d08361cc3f40f14facce`. Open original saved report; it preserves original metadata/history. Returning to the newer report must not carry Verified into its current workspace. Refresh/reopen/sign out and back in: same independent histories, no duplicate records. Do not upload or verify merely to test.
4. Use Load more when offered. Check loading/error feedback, keyboard focus, readable long filenames/IDs and narrow layout. No authorized earlier records yields the explicit empty state. These graphical observations remain pending.
5. Existing Commissioner: historical and current evidence are read-only; download/report GETs work under the independent live report-read permission. Another DS must be denied the anchor/download. Applicant direct C4 URLs redirect to the existing applicant dashboard; legitimate own complaint tracking outsideC4 remains available. Sign-out/account switching must not retain previous protected history.

From repository root:
```powershell
Get-NetTCPConnection -State Listen -LocalPort 5264,3000,8104 -ErrorAction SilentlyContinue | Select-Object LocalPort,OwningProcess
Invoke-RestMethod http://localhost:5264/health
Invoke-RestMethod http://127.0.0.1:8104/health
# Only if5264is free; never duplicate a healthy API:
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\StateLandGovernance\tools\development\Start-C4EarlyEvidenceHistoryApprovedApi.ps1 -CandidateEnvironmentPath '.\.tmp\c4-rename-backup-20261023_0840\renamed-target-environment.local.json' -ValidateOnly
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\StateLandGovernance\tools\development\Start-C4EarlyEvidenceHistoryApprovedApi.ps1 -CandidateEnvironmentPath '.\.tmp\c4-rename-backup-20261023_0840\renamed-target-environment.local.json' -ApprovedActivation
```

Use the existing restricted runtime password at the prompt for read-only metadata:
```powershell
$env:PGOPTIONS='-c default_transaction_read_only=on'
try {
  & 'C:\Program Files\PostgreSQL\18\bin\psql.exe' -X -W -v ON_ERROR_STOP=1 -h localhost -p 5432 -U slg_c4_regulatory_runtime -d state_land_governance_c4 -f '.\StateLandGovernance\docs\verification\owner-proposals\central-complaint-intake\early-evidence-history\VERIFY_READ_ONLY.sql'
} finally { Remove-Item Env:PGOPTIONS -ErrorAction SilentlyContinue }
```
No ordinary fixture actions or inference are needed. Existing services/models/records are preserved; no staging/commit/push performed.
