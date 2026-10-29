# Step 13 local activation diagnosis and proposed commands

2026-10-24, Asia/Colombo. **Diagnosis complete; activation awaits approval.** Only process/artifact inspection, HTTP GETs and an explicitly read-only ordinary database query ran. No application source edits, ordinary writes, migration, grant, service restart, staging, commit or push.

## Diagnosis

The six reported symptoms come from the old running deployment. They do not contradict the current source implementation.

| Surface | Observed live state |
| --- | --- |
| Frontend | PID **32340**, Node `C:\Program Files\nodejs\node.exe`, `next start --hostname 127.0.0.1 --port 3000` |
| Frontend working directory | Repository `StateLandGovernance.Web\` |
| Served production build | **`zqNlpKtMutxsle4CnUTZi`**, Next.js **16.3.5**; BUILD_ID modified 2026-10-24 13:45:24 Asia/Colombo; HTTP sign-in HTML contains this exact ID |
| Frontend source provenance | Source-map contents for OfficerAssessmentResult, regulatoryFollowups, KiwulaAssessmentWorkspace and EarlyGovernanceInvestigation match the retained **Step 7 release source hashes** |
| API | PID **26788**, native `.tmp\c4-step7a-api-final\StateLandGovernance.Api.exe`; working directory is that same package |
| API version | File 1.0.0.0; informational version `1.0.0+2aa0fcfeb931c066f2b17c29e286fbcac046de94`; all **88** retained Step 7 package-manifest files match |
| API health | HTTP 200, Healthy, StateLandGovernance.Api, Live data mode |
| ML | PID **27388**, existing uvicorn `src.http_service:app`, working directory `StateLandGovernance/src/Modules/GovernanceIntelligence/ML`; port 8104 HTTP 200, ready, model_version v2 |
| PostgreSQL | Port 5432, database **state_land_governance_c4**, OID **34144**, server **18.6**, **42 tables**, **23 migrations** |
| Latest ordinary migration | `20261024022733_AllowRegulatoryNoFollowupRequired` |
| Step 12/13 ordinary migration state | Both absent; `ux_regulatory_finding_closure` absent; old operation/remarks CHECKs still apply |

Current Git HEAD is `2aa0fcfeb931c066f2b17c29e286fbcac046de94`, with extensive uncommitted work. That shared commit string does **not** identify the Step 13 working-tree content. There is no unique committed Step 13 revision. File hashes/source manifests are needed to distinguish these releases.

All **119** current C4 frontend source files match the verified `.tmp/c4-step13-web` snapshot. Its build ID is **`gRzm4Bi6-Z-hb8NTuBxL1`**, distinct from the live build. The old assets contain the previous investigation toolbar and lack the current finalized-result, full active-context and individual-closure implementation. The verified Step 13 assets contain the new implementation and no old investigation-toolbar marker. The running API's C4 Application assembly hash also differs from the current built assembly.

**Do not serve the verified browser-test build directly:** its frontend assets embed fixture API port **58118**. Rebuild from the verified source with ordinary local API endpoint **http://localhost:5264**. No UI workaround or source change is needed to fix the stale deployment.

**Do not reuse `Start-C4Step7BApprovedApi.ps1` for the new release:** it hardcodes the old `.tmp/c4-step7a-api-final` package/manifest and exactly 23 migrations. A Step 13 package must be launched using the same protected environment after validating the expected 25-migration state, not by bypassing or editing that old release's gates.

Evidence: `.tmp/c4-step13-readiness/database.json`, `health.json`, `served-sign-in.html`, `artifact-comparison.json`; helper scripts `c4-step13-readiness-readonly.py`, `c4-step13-process-cwd.py`, `c4-step13-artifact-compare.py`. Credentials were not printed. Ordinary SQL used the restricted runtime identity and `default_transaction_read_only=on`.

## Activation order after separate approval

1. Freeze a new source/artifact manifest and prepare a **new** native API package and a fresh Next.js build for port 5264. Do not overwrite the running `.next` output or old API package.
2. Confirm current ports/process identities and protected environment; retain old artifacts for compatible recovery. Stop only the identified frontend/API after approval; **leave ML PID 27388 and PostgreSQL PID 7024 running**.
3. Capture a new ordinary preservation baseline, full custom-format database backup, backup hash and successful restore into a new isolated database. A mere `pg_restore --list` is insufficient; perform the restore and compare restored rows/schema/history before ordinary DDL.
4. Recheck exact ordinary target OID 34144, 42 tables, exact 23 migration identities and reviewed SQL hashes. No business-write test may run during this window. Apply only the two reviewed scripts below, in order.
5. Verify 25 migration identities, both CHECK definitions and unique closure index; compare all non-history row digests, sequences and runtime privileges with baseline. Do not grant additional rights.
6. Start updated native API on 5264 with the existing protected candidate environment, then new frontend on 3000. Preserve healthy ML unchanged.
7. Verify build identity and health, then perform read-only UI/authorization checks. Ordinary synthetic write testing requires its own authorization; do not use existing Kiwulakale/demo records.

## Commands for the approved activation window — not executed

Run from the repository root in PowerShell. Check exit codes after every external command and stop on any failure. Keep credential values and candidate contents out of logs.

```powershell
$ErrorActionPreference = 'Stop'
$repo = (Get-Location).Path
$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$release = Join-Path $repo ".tmp/c4-step13-local-$stamp"
$apiPackage = Join-Path $release 'api'
$webPackage = Join-Path $release 'web'
$pg = 'C:/Program Files/PostgreSQL/18/bin'
$docs = Join-Path $repo 'StateLandGovernance/docs/verification/owner-proposals/central-complaint-intake'
$sql12 = Join-Path $docs 'step12-individual-closure-20261024/REVIEWED_FORWARD.sql'
$sql13 = Join-Path $docs 'step13-direct-disposition-20261024/REVIEWED_FORWARD.sql'

# Recheck process IDs; they may change after this diagnosis.
Get-NetTCPConnection -State Listen -LocalPort 3000,5264,8104,5432 |
  Select-Object LocalPort,OwningProcess
Get-CimInstance Win32_Process |
  Where-Object { $_.ProcessId -in 32340,26788,27388 } |
  Select-Object ProcessId,ExecutablePath,CommandLine

if ((Get-FileHash $sql12 -Algorithm SHA256).Hash.ToLowerInvariant() -ne
 '2393ae71134d1da30160c727d3d15687bba93a852ca10145a01071426ed6c9ef') { throw 'Step12 SQL changed' }
if ((Get-FileHash $sql13 -Algorithm SHA256).Hash.ToLowerInvariant() -ne
 '02d2d73937d017b5665029a58e23900d2bb57080d9eafa55c4b3ad6fa9ac7714') { throw 'Step13 SQL changed' }

# Before migrations: confirm the existing protected environment/package/database
# while the ordinary schema is STILL at its diagnosed 23 migrations.
powershell.exe -NoProfile -File ./StateLandGovernance/tools/development/Start-C4Step7BApprovedApi.ps1 `
 -CandidateEnvironmentPath './.tmp/c4-rename-backup-20261023_0840/renamed-target-environment.local.json' -ValidateOnly
if ($LASTEXITCODE -ne 0) { throw 'Protected configuration preflight failed' }

New-Item -ItemType Directory -Path $release,$webPackage -Force | Out-Null
dotnet publish ./StateLandGovernance/src/Api/StateLandGovernance.Api.csproj `
 -c Release --no-restore --self-contained false -p:UseAppHost=true -o $apiPackage
if ($LASTEXITCODE -ne 0) { throw 'API publish failed' }

Copy-Item ./StateLandGovernance.Web/src -Destination $webPackage -Recurse
Copy-Item ./StateLandGovernance.Web/public -Destination $webPackage -Recurse
foreach ($name in @('package.json','package-lock.json','tsconfig.json',
 'next-env.d.ts','next.config.ts','postcss.config.mjs')) {
 $source = Join-Path './StateLandGovernance.Web' $name
 if (Test-Path $source) { Copy-Item $source -Destination $webPackage }
}
New-Item -ItemType Junction -Path (Join-Path $webPackage 'node_modules') `
 -Target (Join-Path $repo 'StateLandGovernance.Web/node_modules') | Out-Null

# Use webpack for this separate package's dependency junction; no product source
# or original next.config.ts changes. This build must pass before any service stops.
$priorPublicApi = $env:NEXT_PUBLIC_API_BASE_URL
try {
 $env:NEXT_PUBLIC_API_BASE_URL = 'http://localhost:5264'
 Push-Location $webPackage
 try {
  npm.cmd run build -- --webpack
  if ($LASTEXITCODE -ne 0) { throw 'Local frontend build failed' }
 } finally { Pop-Location }
} finally { $env:NEXT_PUBLIC_API_BASE_URL = $priorPublicApi }
Get-Content (Join-Path $webPackage '.next/BUILD_ID')
```

The new local-endpoint build/package is deliberately **not yet produced or qualified**. Its build must succeed, its C4 source must match the verified snapshot, and emitted browser assets must use 5264 with no `58118` fixture endpoint. If the separate webpack build fails, stop and resolve packaging before touching services; do not switch to serving the fixture build. Capture SHA256 manifests for the new package and source, with HEAD plus dirty-worktree provenance.

### Quiesce, backup and database preflight

Only after verifying live PID/path/port identities again:

```powershell
# These specific PIDs are observations, not permission to kill a changed process.
$webProcess = Get-CimInstance Win32_Process -Filter 'ProcessId=32340'
if ($webProcess.Name -ne 'node.exe' -or
 $webProcess.CommandLine -notlike '*next*start*--port 3000*') { throw 'Frontend identity changed' }
$apiProcess = Get-CimInstance Win32_Process -Filter 'ProcessId=26788'
if ($apiProcess.ExecutablePath -ne (Join-Path $repo '.tmp/c4-step7a-api-final/StateLandGovernance.Api.exe')) {
 throw 'API identity changed'
}
Stop-Process -Id 32340
Stop-Process -Id 26788
# Verify 3000 and 5264 are now free; do not kill ML or PostgreSQL.
```

Use the local PostgreSQL operator credential through a secure prompt or existing protected credential loader, never the restricted application role for DDL. Set `PGHOST=localhost`, `PGPORT=5432`, `PGDATABASE=state_land_governance_c4`, `PGUSER=postgres`; temporarily provide `PGPASSWORD` without printing it. Preserve and restore prior environment values afterward. Operator-session identity must match the exact database/OID before backup or SQL.

```powershell
$env:PGHOST='localhost'; $env:PGPORT='5432'
$env:PGDATABASE='state_land_governance_c4'; $env:PGUSER='postgres'
# Supply PGPASSWORD securely, outside logged command arguments.
$env:PGOPTIONS='-c default_transaction_read_only=on -c statement_timeout=15000'
& "$pg/psql.exe" -X -w -t -A -v ON_ERROR_STOP=1 -c @'
SELECT json_build_object('database',current_database(),
 'oid',(SELECT oid FROM pg_database WHERE datname=current_database()),
 'migrations',(SELECT json_agg("MigrationId" ORDER BY "MigrationId")
 FROM governance_intelligence.__ef_migrations_history));
'@
if ($LASTEXITCODE -ne 0) { throw 'Database preflight failed' }

# Gate: OID 34144, exact captured 23 identities; neither Step12 nor Step13 present.
# Capture row counts/digests for all 42 tables, schemas, sequences, constraints,
# existing indexes and runtime grants to a new baseline artifact at this point.
$backup = Join-Path $release 'state_land_governance_c4-before-step13.dump'
$env:PGOPTIONS='-c default_transaction_read_only=on'
& "$pg/pg_dump.exe" -w -Fc -f $backup
if ($LASTEXITCODE -ne 0) { throw 'Backup failed' }
Get-FileHash $backup -Algorithm SHA256
& "$pg/pg_restore.exe" --list $backup | Out-File (Join-Path $release 'backup-contents.txt')
if ($LASTEXITCODE -ne 0) { throw 'Backup archive unreadable' }
```

Restore that new backup into a newly named isolated database on **127.0.0.1:57657**, using its separate `central_test` credential. Guard the isolated database name and refuse overwrite. Example target `c4_activation_step13_<timestamp>`:

```powershell
# Use separate isolated credentials/environment for these commands; do not inherit
# ordinary PGDATABASE/PGPASSWORD or default_transaction_read_only for CREATE DATABASE.
& "$pg/createdb.exe" -w -h 127.0.0.1 -p 57657 -U central_test $isolatedDatabase
if ($LASTEXITCODE -ne 0) { throw 'Isolated backup target failed' }
& "$pg/pg_restore.exe" -w -h 127.0.0.1 -p 57657 -U central_test `
 -d $isolatedDatabase --no-owner --no-privileges --exit-on-error $backup
if ($LASTEXITCODE -ne 0) { throw 'Backup restore verification failed' }
```

Compare restored rows/schema/history to the ordinary baseline; no ordinary SQL may proceed until this passes. Keep the new backup and the previous backup in place. Do not restore over ordinary C4.

### Exact forward SQL order

1. `20261024112715_AllowIndividualFindingClosure` — reviewed Step 12 SQL; creates the unique closure index and closure CHECK.
2. `20261024120539_AllowDirectOfficerDisposition` — reviewed Step 13 SQL; permits absent remarks/references.

Do not replay earlier migrations/grants. Do not run a broad `dotnet ef database update`. Each retained script already has its own transaction; do not wrap it with `psql --single-transaction`. Set finite lock/statement timeouts so lock contention fails rather than hanging indefinitely. Recheck predecessor state immediately before each script; if only Step 12 commits, stop and report the 24-migration intermediate state rather than replaying it.

Windows path lengths previously prevented this psql from opening long reviewed artifact paths. Supply the **hash-verified original bytes** through standard input. In the approved operator environment only:

```python
# Execute only AFTER approval, backup/restore proof and exact-state gates.
from pathlib import Path
import hashlib, os, subprocess
items = [
 (Path(sql12_path), '2393ae71134d1da30160c727d3d15687bba93a852ca10145a01071426ed6c9ef'),
 (Path(sql13_path), '02d2d73937d017b5665029a58e23900d2bb57080d9eafa55c4b3ad6fa9ac7714'),
]
# sql12_path/sql13_path are the exact reviewed paths identified above.
for artifact, expected in items:
    payload = artifact.read_bytes()
    assert hashlib.sha256(payload).hexdigest() == expected
    # Guard/re-read expected predecessor migration history before this call.
    subprocess.run([r'C:/Program Files/PostgreSQL/18/bin/psql.exe',
        '-X','-w','-h','localhost','-p','5432','-U','postgres',
        '-d','state_land_governance_c4','-v','ON_ERROR_STOP=1'],
        input=payload, check=True, env=os.environ | {
          'PGOPTIONS':'-c lock_timeout=5000 -c statement_timeout=60000',
          'PGCLIENTENCODING':'UTF8'})
```

Postflight: exact prior history plus those two IDs (**25 total**), OID 34144, **42 tables**, correct partial unique index, closure CHECK allowing empty values, unchanged non-history rows/sequences/runtime grants and unchanged prior audit/hash data. No new privilege is required. Missing or changed state is a stop condition.

### Native API and frontend startup

Load the **existing** protected candidate environment without writing it or printing values. Preserve/restore the launch shell's previous values. Require its C4 connection to be localhost:5432/state_land_governance_c4 as `slg_c4_regulatory_runtime`; retain peer/account read-only connection protections and disabled blockchain/sandbox workers. Pin API URLs to loopback port 5264 in the child environment. Validate the newly published package manifest and 25-migration postflight before starting.

```powershell
$candidatePath = Join-Path $repo '.tmp/c4-rename-backup-20261023_0840/renamed-target-environment.local.json'
$candidate = Get-Content -LiteralPath $candidatePath -Raw | ConvertFrom-Json
$savedEnvironment = @{}
try {
 foreach ($property in $candidate.PSObject.Properties) {
  $savedEnvironment[$property.Name] = [Environment]::GetEnvironmentVariable($property.Name,'Process')
  [Environment]::SetEnvironmentVariable($property.Name,[string]$property.Value,'Process')
 }
 if (!$savedEnvironment.ContainsKey('ASPNETCORE_URLS')) {
  $savedEnvironment['ASPNETCORE_URLS'] = $env:ASPNETCORE_URLS
 }
 $env:ASPNETCORE_URLS='http://127.0.0.1:5264;http://localhost:5264'
 # Assert port 5264 free before launch; do not run the old Step7 launcher.
 $newApi = Start-Process -WindowStyle Hidden `
  -FilePath (Join-Path $apiPackage 'StateLandGovernance.Api.exe') `
  -WorkingDirectory $apiPackage -PassThru `
  -RedirectStandardOutput (Join-Path $release 'api.stdout.log') `
  -RedirectStandardError (Join-Path $release 'api.stderr.log')
} finally {
 foreach ($key in $savedEnvironment.Keys) {
  [Environment]::SetEnvironmentVariable($key,$savedEnvironment[$key],'Process')
 }
}

# After API health and protected settings pass, assert 3000 free.
$oldInternalApi = $env:C4_INTERNAL_API_BASE_URL
try {
 $env:C4_INTERNAL_API_BASE_URL='http://127.0.0.1:5264'
 $newWeb = Start-Process -WindowStyle Hidden -FilePath 'C:/Program Files/nodejs/node.exe' `
  -ArgumentList @((Join-Path $repo 'StateLandGovernance.Web/node_modules/next/dist/bin/next'),
   'start','--hostname','127.0.0.1','--port','3000') `
  -WorkingDirectory $webPackage -PassThru `
  -RedirectStandardOutput (Join-Path $release 'web.stdout.log') `
  -RedirectStandardError (Join-Path $release 'web.stderr.log')
} finally { $env:C4_INTERNAL_API_BASE_URL=$oldInternalApi }
```

Paths passed to Start-Process must be quoted correctly if this checkout is moved to a path containing spaces. Capture new PIDs and verify their actual executable/cwd/build IDs against the prepared release; a healthy endpoint alone does not prove version. Keep ML running on 8104; check its health before/after, with the same PID and frozen bundle hashes. Do not run Start-C4Ml.ps1 while its listener is healthy.

## Acceptance checks before any ordinary write test

- GET 5264/health and 8104/health; confirm Live API, ready v2 ML, no duplicate listeners.
- GET 3000/sign-in and verify its served build ID equals the new ordinary-endpoint package BUILD_ID, not either old/fixture ID. Browser assets must call 5264, never 58118/5807/3159.
- Sign in through the existing local session flow. Check officer admission and denial for unauthorized roles without calling business write endpoints.
- Start Screening: seven empty aligned concerns; no New/Open Existing/History toolbar, Previous Assessments, old result or duplicate manual-verification form. Opening makes no business POST.
- Open an already saved owned assessment explicitly through the separate navbar history. The main current-result renderer must show exact identifiers, finalization, seven saved findings and unchanged outcome separately from independent evidence state. History selection must not silently replace an active workflow.
- On an existing accessible current Regulatory evaluation, selector includes only Insufficient Information/Human Review Required; action includes No Issues Found — Verify and Close; selecting it shows no required remarks/reference/upload fields. Do **not** click Save during read-only smoke verification.
- Check exact active Early/Regulatory IDs on return navigation/refresh; no duplicate evaluation or previous-run list. Conflict/Combined screens must not substitute historical clearance; C3 handoff remains disabled.
- New end-to-end save/upload/finalize/Run/closure tests against ordinary storage remain pending separately authorized synthetic manual testing. Do not claim their durability from read-only smoke checks.

## Stop/recovery conditions

If hashes, credentials/target, restore proof, migration predecessor state or preservation checks fail, stop before proceeding. If package startup fails after migration, keep the ordinary data intact and stop only the newly identified API/frontend processes. Do not run Down, restore over ordinary data, replay grants or delete histories. The old read/write API is not automatically a safe fallback after new terminal dispositions exist; any fallback must be compatibility-reviewed and preferably read-only. ML remains untouched.

**STOP FOR APPROVAL.** These are proposed commands/gates, not an executed activation. The package build and guarded backup/migration/postflight runner still need to be carried out in the approved activation window. No source change is proposed to compensate for the stale build.
