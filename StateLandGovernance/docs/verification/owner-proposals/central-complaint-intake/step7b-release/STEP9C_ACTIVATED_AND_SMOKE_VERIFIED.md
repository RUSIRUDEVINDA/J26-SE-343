# C4 Step 9C — local activation and read-only smoke verification

2026-10-24, Asia/Colombo. **LOCAL C4 RUNNING.** This continuation applied only the already reviewed attachment-table `SELECT`/`INSERT` grant. Both approved migrations were already present and were not replayed. No ordinary business record, complaint, assessment, evidence, decision, ML inference, model, regulatory rule, Conflict Detection implementation, Git index, commit or push changed.

## Grant path resolution

The source artifact was retained in place:

```text
StateLandGovernance/docs/verification/owner-proposals/central-complaint-intake/step1-early-governance-20261024/REVIEWED_ADDITIONAL_PERMISSIONS.sql
```

Its SHA256 is `59c21d8cd227490d8c00fe5a279b5dcc5af71cd4d00209ff6086a2a9103e9674`, matching the independently retained `FINAL_REVIEW_SHA256.json`. The complete decoded content was verified as exactly:

```sql
GRANT SELECT, INSERT ON TABLE governance_intelligence.officer_investigation_attachments TO slg_c4_regulatory_runtime;
```

The 271-character Windows path cannot be opened by this PostgreSQL 18.6 `psql` client, including with its extended-length form. Step 9C therefore supplied the verified original byte buffer directly to `psql` standard input. It did not generate, reconstruct, edit, copy, or substitute SQL, and `ON_ERROR_STOP` was enabled.

## Database and preservation

Immediately before execution, the database guard required `localhost:5432/state_land_governance_c4`, OID `34144`, 42 tables, 23 migrations, both approved migration identities, an empty attachment table, and no runtime rights on that table. It passed.

After successful execution, read-only verification found:

- OID `34144`, 42 tables, 23 migrations.
- `20261024000805_AddInvestigationEvidenceAndFinalization` and `20261024022733_AllowRegulatoryNoFollowupRequired` present.
- `officer_investigation_attachments` remains empty.
- `slg_c4_regulatory_runtime` has exactly `INSERT` and `SELECT` on that table.
- No `UPDATE`, `DELETE`, `TRUNCATE`, DDL, role-membership, or other attachment privilege was granted.
- The existing backup still hashes to `1b5f7ba0872f0cf59fe9f118c06cb3079f9c6a965120b568e31647e3f3032dd0`.

The Step 9 pre-write baseline already verified ordinary historical rows, schemas, sequences, migration history, durable grants, Kiwulakale proposal and demo workflow against the approved baseline. The only ordinary SQL executed since that baseline was the two explicitly approved forward migrations and this one reviewed `GRANT`; the grant itself cannot write application rows, sequences, or migration history.

## Running services

| Service | Loopback endpoint | Result |
| --- | --- | --- |
| ML | `127.0.0.1:8104/health` | `ready`, model version `v2` |
| API | `127.0.0.1:5264/health` | `Healthy`, `StateLandGovernance.Api`, live data mode |
| Frontend | `127.0.0.1:3000/` | HTTP 200; State Land Lease Information and Management System |

The ML launcher verified the frozen bundle before it bound its port. Current artifact hashes remain complaint classifier v2 `63b37eb7175e74a8dcde82bf042fcf9dcbb4f0432fbe3b4677aac037007a20ae` and anomaly v1 `59ed4a73d56d9f06798c4ebc4684a633afe72841c41ed0b3ba4e7bb658782b46`.

## Read-only authorization smoke checks

Using documented local development identities, with no write endpoint called:

| Check | HTTP result |
| --- | --- |
| Commissioner governance admission | 200 |
| Commissioner central-complaint capabilities | 200 |
| Commissioner saved officer-report history | 200 |
| Applicant governance admission | 403 |
| Applicant central-complaint capability request | 403 |
| Applicant own complaint tracking | 200 |
| Anonymous governance admission | 401 |

## Restart procedure

Inspect ports first. Reuse a healthy listener; do not start duplicates.

```powershell
Set-Location 'D:\AI-Driven-Digital-Lease-Governance-for-State-Land-Management\AI-Driven-Digital-Lease-Governance-for-State-Land-Management'
Get-NetTCPConnection -State Listen -LocalPort 3000,5264,8104 -ErrorAction SilentlyContinue |
  Select-Object LocalPort,OwningProcess

# Only if 8104 is free
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\StateLandGovernance\tools\development\Start-C4Ml.ps1

# Only if 5264 is free
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\StateLandGovernance\tools\development\Start-C4Step7BApprovedApi.ps1 `
  -CandidateEnvironmentPath '.\.tmp\c4-rename-backup-20261023_0840\renamed-target-environment.local.json' `
  -ApprovedActivation

# Only if 3000 is free
Set-Location .\StateLandGovernance.Web
npm.cmd run start -- --hostname 127.0.0.1 --port 3000
```

Manual testing of a separately authorized synthetic workflow remains pending. Do not use Kiwulakale or existing demo records for test writes.
