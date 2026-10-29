# HOW TO RUN THE PROJECT MANUALLY NEXT TIME — WINDOWS POWERSHELL

Updated 2026-10-25, Asia/Colombo, for approved Step 19 activation. These commands use the existing frozen ML environment and verified Step 19 native API/frontend packages. They do not build, migrate, grant, seed or train anything.

## Startup order and terminals

1. PostgreSQL Windows service.
2. Python ML in terminal 1; keep that terminal running.
3. Native API using the safe launcher below. It starts in the background with logs; its launch terminal may then be reused.
4. Next.js using the Step 19 background launcher; its terminal may then be reused.

Use a third PowerShell terminal for health checks or stop commands. After restarting Windows, application PIDs change: never paste an old PID into Stop-Process. Keep the retained `.tmp/c4-ml-venv`, `.tmp/c4-step19-prepared`, protected configuration/baseline files, the Step 19 start/stop helpers and the existing Step 18 ML inspection helper. Retain old packages for reviewed recovery. Do not delete or move them as temporary clutter while using this release.

At the start of EACH terminal, paste:

```powershell
$repo = 'D:\AI-Driven-Digital-Lease-Governance-for-State-Land-Management\AI-Driven-Digital-Lease-Governance-for-State-Land-Management'
Set-Location -LiteralPath $repo
```

## A. PostgreSQL — actual Windows service

This ordinary database is managed by Windows service **postgresql-x64-18**, display name **postgresql-x64-18 - PostgreSQL Server 18**, automatic startup. It is not a Docker database. Its verified service command uses `C:\Program Files\PostgreSQL\18\bin\pg_ctl.exe` and data directory `C:\Program Files\PostgreSQL\18\data`. Do not start the separate isolated Docker test database to run the ordinary application.

Check first:

```powershell
Get-Service -Name 'postgresql-x64-18'
Get-NetTCPConnection -State Listen -LocalPort 5432 -ErrorAction SilentlyContinue |
    Select-Object LocalAddress,LocalPort,OwningProcess
& 'C:/Program Files/PostgreSQL/18/bin/pg_isready.exe' -h localhost -p 5432
```

Expected: Running and `localhost:5432 - accepting connections`. During recovery the service wrapper PID was 4312 and PostgreSQL listener PID was 7024; use the service name after reboot, not those historical PIDs.

Only if the existing service is **Stopped**, start it from an Administrator PowerShell:

```powershell
$service = Get-Service -Name 'postgresql-x64-18'
if ($service.Status -eq 'Stopped') {
    Start-Service -Name 'postgresql-x64-18'
} elseif ($service.Status -ne 'Running') {
    throw 'PostgreSQL is transitioning or unavailable. Inspect Windows Services; do not start a second instance.'
}
& 'C:/Program Files/PostgreSQL/18/bin/pg_isready.exe' -h localhost -p 5432
```

The service already existed and was Running during verification, so no Start-Service/stop/restart was performed. Leave healthy PostgreSQL running. Do not use Restart-Service, database creation, migrations or grants as a startup workaround.

## B. Python ML — terminal 1

Correct working directory and existing environment:

```powershell
Set-Location -LiteralPath (Join-Path $repo 'StateLandGovernance/src/Modules/GovernanceIntelligence/ML')
$python = Join-Path $repo '.tmp/c4-ml-venv/Scripts/python.exe'
if (!(Test-Path -LiteralPath $python)) { throw 'The existing verified ML environment is missing.' }
Get-NetTCPConnection -State Listen -LocalPort 8104 -ErrorAction SilentlyContinue |
    Select-Object LocalPort,OwningProcess
```

If a listener exists, check its identity and health; reuse a healthy existing service. Otherwise run the verified existing launcher:

```powershell
if ($env:GOVERNANCE_MODEL_VERSION -and $env:GOVERNANCE_MODEL_VERSION -ne 'v2') { throw 'Unexpected ML model version override; inspect it without starting another runtime.' }
foreach ($key in @('GOVERNANCE_MODEL_PATH','GOVERNANCE_MODEL_METADATA_PATH','GOVERNANCE_MODEL_VALIDATION_PATH','GOVERNANCE_ANOMALY_BUNDLE_CONFIG')) {
    if ([Environment]::GetEnvironmentVariable($key,'Process')) { throw 'Unexpected ML artifact override; use the existing reviewed startup environment.' }
}
$listener = @(Get-NetTCPConnection -State Listen -LocalPort 8104 -ErrorAction SilentlyContinue)
if ($listener.Count) {
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$repo/.tmp/c4-step18-stop-service.ps1" -Service ML -CheckOnly
    if ($LASTEXITCODE -ne 0) { throw 'Port 8104 is not the verified ML service.' }
    $mlHealth = Invoke-RestMethod 'http://localhost:8104/health' -TimeoutSec 5
    if ($mlHealth.status -ne 'ready' -or $mlHealth.model_version -ne 'v2') { throw 'Existing ML is not ready v2; inspect its logs.' }
    Write-Host 'ML is already ready. Reuse it; do not start another instance.'
} else {
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$repo/StateLandGovernance/tools/development/Start-C4Ml.ps1"
}
```

The final command remains running in the terminal. The existing launcher enters the ML directory and executes exactly:

```powershell
# Shown to document the verified module and arguments; do not run these again
# while the launcher above is already serving port 8104.
& $python -m anomaly_detection.cli verify-bundle
if ($LASTEXITCODE -ne 0) { throw 'Frozen bundle verification failed; do not start ML.' }
& $python -m uvicorn src.http_service:app --host 127.0.0.1 --port 8104 --ws none
```

No Activate.ps1 step is required: the launcher explicitly selects the existing virtual-environment interpreter. The verified service defaults to classifier v2 and the existing frozen anomaly bundle. Do not set new artifact-path overrides or run training. Windows' virtual-environment shim may show a child base Python311 executable in process inspection; that is the verified behavior of this environment, not a second ML instance.

In another terminal:

```powershell
Invoke-RestMethod 'http://localhost:8104/health' -TimeoutSec 5
```

Expected: `status=ready`, `model_version=v2`.

## C. Step 19 native API — protected configuration loader

Use the retained `.tmp/c4-step19-prepared/api` package. No build, migration, grant or new configuration is needed. The launcher validates all 88 API manifest entries, the protected environment/baseline, restricted runtime role, read-only peer/account connections, disabled workers, exact database OID 34144 / 25 migrations and closure index before starting.

```powershell
Set-Location -LiteralPath (Join-Path $repo '.tmp/c4-step19-prepared/api')
# The launcher sets exactly ASPNETCORE_URLS=http://localhost:5264 in its child.
# It loads the existing protected configuration without printing credentials.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$repo/.tmp/c4-step19-start-api.ps1" -ValidateOnly
if ($LASTEXITCODE -ne 0) { throw 'Step19 API preflight failed. Stop; do not bypass it.' }
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$repo/.tmp/c4-step19-start-api.ps1"
if ($LASTEXITCODE -ne 0) { throw 'Step19 API startup failed.' }
$apiHealth = $null
for ($attempt=0; $attempt -lt 30; $attempt++) {
    try { $apiHealth = Invoke-RestMethod 'http://localhost:5264/health' -TimeoutSec 5; if ($apiHealth.status -eq 'Healthy' -and $apiHealth.dataMode -eq 'Live') { break } } catch {}
    Start-Sleep -Milliseconds 500
}
$apiHealth
if ($apiHealth.status -ne 'Healthy' -or $apiHealth.dataMode -ne 'Live') { throw 'API is not ready; inspect its logs.' }
```

This exact launcher was used for the approved Step 19 activation. It starts `.tmp/c4-step19-prepared/api/StateLandGovernance.Api.exe` in that package's working directory, hidden in the background. It restores the launching shell's prior environment values. It reuses a matching healthy API and refuses a conflicting listener. Do not add a second 127.0.0.1 binding. Its protected environment path remains `.tmp/c4-rename-backup-20261023_0840/renamed-target-environment.local.json`; never print or paste that file.

Logs: `.tmp/c4-step19-prepared/live-api.stdout.log` and `live-api.stderr.log`. PID evidence: `api-process.json` in that directory. The launcher returning does not guarantee startup health; require the Healthy/Live check above and a stable listener before starting the frontend.

## D. Step 19 production frontend — background launcher

Use `.tmp/c4-step19-prepared/web`, expected BUILD_ID **DRFPggN7amhgFu7ERvWXg**. Do not run npm install/build/dev. This helper validates the prepared manifest, BUILD_ID and API health; it uses the existing Node/Next executable and starts only if port 3000 is free.

```powershell
Set-Location -LiteralPath $repo
# The launcher sets C4_INTERNAL_API_BASE_URL=http://localhost:5264 in its child.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$repo/.tmp/c4-step19-start-frontend.ps1" -ValidateOnly
if ($LASTEXITCODE -ne 0) { throw 'Step19 frontend preflight failed.' }
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$repo/.tmp/c4-step19-start-frontend.ps1"
if ($LASTEXITCODE -ne 0) { throw 'Step19 frontend startup failed.' }
$page = $null
for ($attempt=0; $attempt -lt 30; $attempt++) {
    try { $page = Invoke-WebRequest 'http://localhost:3000/sign-in' -UseBasicParsing -TimeoutSec 5; if ($page.Content.Contains('DRFPggN7amhgFu7ERvWXg')) { break } } catch {}
    Start-Sleep -Milliseconds 500
}
if (!$page.Content.Contains('DRFPggN7amhgFu7ERvWXg')) { throw 'Served frontend is not Step19.' }
$page.StatusCode
```

The tested helper starts `C:/Program Files/nodejs/node.exe` with the existing `StateLandGovernance.Web/node_modules/next/dist/bin/next` binary and arguments `start --hostname 127.0.0.1 --port 3000`, working directory `.tmp/c4-step19-prepared/web`. The child environment contains the internal API URL shown above; browser assets already embed `http://localhost:5264`. The helper quotes the Next path, restores the parent environment, reuses the verified matching build and rejects a different listener. Both application launch terminals may be closed once health passes; ML's foreground terminal must remain open.

Logs: `.tmp/c4-step19-prepared/live-web.stdout.log` and `live-web.stderr.log`; PID evidence: `frontend-process.json`. Open `http://localhost:3000` manually in the browser. No fixture-port build should be used.

## E. Health and process checks

```powershell
Get-NetTCPConnection -State Listen -LocalPort 3000,5264,8104,5432 -ErrorAction SilentlyContinue |
    Select-Object LocalAddress,LocalPort,OwningProcess
& 'C:/Program Files/PostgreSQL/18/bin/pg_isready.exe' -h localhost -p 5432
Invoke-RestMethod 'http://localhost:8104/health' -TimeoutSec 5
Invoke-RestMethod 'http://localhost:5264/health' -TimeoutSec 5
$page = Invoke-WebRequest 'http://localhost:3000/sign-in' -UseBasicParsing -TimeoutSec 10
$page.StatusCode
$page.Content.Contains('DRFPggN7amhgFu7ERvWXg')
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$repo/.tmp/c4-step19-stop-service.ps1" -Service API -CheckOnly
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$repo/.tmp/c4-step19-stop-service.ps1" -Service Frontend -CheckOnly
```

Expected: PostgreSQL accepting connections, ML ready/v2, API Healthy/Live, frontend 200/True. CheckOnly prints the current PID/path/command without stopping anything. One API PID may own IPv4 and IPv6 localhost listeners. These are not duplicate API services.

## F. Stop or restart one application safely

Stop only when no save/upload/follow-up is in progress. Inspect dynamically rather than reusing a PID from this document:

```powershell
# Inspection only; these do not stop anything.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$repo/.tmp/c4-step19-stop-service.ps1" -Service Frontend -CheckOnly
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$repo/.tmp/c4-step19-stop-service.ps1" -Service API -CheckOnly
# The previously verified ML inspection/startup commands are retained.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$repo/.tmp/c4-step18-stop-service.ps1" -Service ML -CheckOnly
```

When deliberately stopping ONE service, choose only its command:

```powershell
# Individual commands, not a batch stop procedure:
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$repo/.tmp/c4-step19-stop-service.ps1" -Service Frontend
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$repo/.tmp/c4-step19-stop-service.ps1" -Service API
```

The same Step 19 stop helper was exercised during activation with explicit `-Release Step18` to stop only the old verified application processes. Its default is Step19: it checks the native API executable or Node/Next command plus served frontend BUILD_ID before stopping the single listener. Both Step19 CheckOnly paths were tested after activation; a gratuitous stop/restart of the healthy new release was not performed. A free port reports already stopped; identity mismatch stops the procedure. It never controls PostgreSQL.

ML's previously verified individual stop command remains available only when a deliberate ML stop is required; it was not executed during Step 19 activation:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$repo/.tmp/c4-step18-stop-service.ps1" -Service ML
```

To recover a stopped application, rerun only its C or D startup block. To replace a verified unhealthy process, inspect its identity first and stop that service only. Do not restart a healthy dependency to recover another service. Do not run all stop commands together.

## G. Retained files and troubleshooting

Keep `.tmp/c4-ml-venv`, `.tmp/c4-step19-prepared`, the protected configuration/baseline files, `.tmp/c4-step19-start-api.ps1`, `.tmp/c4-step19-start-frontend.ps1`, `.tmp/c4-step19-stop-service.ps1` and the existing Step 18 ML inspection helper. Retain the previous Step 17 API / Step 18 frontend packages for reviewed recovery. These are operational release artifacts despite their `.tmp` location.

- Port occupied: inspect the listener and CheckOnly output. Reuse the matching healthy release. Do not kill all node/python/dotnet processes.
- API configuration/database preflight fails: stop and keep the error. Do not change credentials, grants, migration history or database target.
- API startup fails: inspect its stdout/stderr and port. Exactly one localhost binding is required. Do not add a second binding or force a different port.
- Frontend API failure: check API health and the internal URL supplied by the frontend launcher. Do not use a fixture build or rebuild this approved package.
- BUILD_ID/hash mismatch: stop; recover the retained verified artifacts. No source reconstruction or package rebuild is part of manual startup.
- ML failure: use the unchanged B commands and frozen bundle verification. Do not retrain, switch environments or suppress artifact validation.
- No new migration, grant, seed, applicant communication, C3 integration or business-record test belongs to startup. API/frontend release selection must remain paired so that Addressed labels and authoritative completeness agree.

## Verified Step 19 activation and limits

Step 19 API PID **10144**, Healthy/Live on port 5264; frontend PID **39920**, serving **DRFPggN7amhgFu7ERvWXg** on port 3000. ML remains PID **2412**, ready/v2; PostgreSQL remains PID **7024**, OID 34144, 25 migrations. These are dated observations, not future stop commands.

Read-only live acceptance and all-table preservation results are recorded in `STEP19_ACTIVATED_AND_READONLY_VERIFIED.md` and `.tmp/c4-step19-prepared/activation-*.json`. The Step 19 gate uses saved officer follow-up completeness. Addressed does not mean an outstanding clarification or institutional/legal concern has been cleared. Original findings and all saved audit data remain preserved.

No business-record writes, migrations, grants, package rebuilds, ML/PostgreSQL restarts or Git mutations were part of activation. New ordinary write testing requires separate authorization.
