# Removes only known Land Intelligence synthetic/demo parcel rows from a target database.
# Default is dry-run. Deletion requires -Force.
#
# Allowlist:
#   - CadastralNumber ILIKE 'DEMO-%'
#   - Must NOT have a land_intelligence.real_parcel_sources row (imported cadastre is never deleted)
#
# Never drops a database.

param(
    [Parameter(Mandatory = $true)]
    [string]$ConnectionString,

    [switch]$Force
)

$ErrorActionPreference = "Stop"

Write-Host "Synthetic / demo land parcel cleanup"
Write-Host "Mode: $(if ($Force) { 'DELETE (-Force)' } else { 'DRY-RUN (default)' })"
Write-Host ""

$psqlCmd = Get-Command psql -ErrorAction SilentlyContinue
$psqlPath = $null
if ($psqlCmd) {
    $psqlPath = $psqlCmd.Source
} else {
    foreach ($c in @(
            "C:\Program Files\PostgreSQL\17\bin\psql.exe",
            "C:\Program Files\PostgreSQL\16\bin\psql.exe",
            "C:\Program Files\PostgreSQL\15\bin\psql.exe"
        )) {
        if (Test-Path $c) { $psqlPath = $c; break }
    }
}
if (-not $psqlPath) {
    throw "psql was not found. Install PostgreSQL client tools or add psql to PATH."
}

function Get-CsValue([string]$cs, [string]$key) {
    foreach ($part in $cs.Split(';')) {
        $kv = $part.Split('=', 2)
        if ($kv.Count -eq 2 -and $kv[0].Trim().Equals($key, [StringComparison]::OrdinalIgnoreCase)) {
            return $kv[1].Trim()
        }
    }
    return $null
}

$hostName = Get-CsValue $ConnectionString "Host"
$port = Get-CsValue $ConnectionString "Port"
if (-not $port) { $port = "5432" }
$db = Get-CsValue $ConnectionString "Database"
$user = Get-CsValue $ConnectionString "Username"
if (-not $user) { $user = Get-CsValue $ConnectionString "User ID" }
$password = Get-CsValue $ConnectionString "Password"

if ([string]::IsNullOrWhiteSpace($hostName) -or [string]::IsNullOrWhiteSpace($db) -or [string]::IsNullOrWhiteSpace($user)) {
    throw "ConnectionString must include Host, Database, and Username."
}

$env:PGPASSWORD = $password
$env:PGCLIENTENCODING = "UTF8"

$tempDir = Join-Path $env:TEMP ("li-synthetic-cleanup-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempDir | Out-Null

$listSqlPath = Join-Path $tempDir "list.sql"
$countSqlPath = Join-Path $tempDir "count.sql"
$deleteSqlPath = Join-Path $tempDir "delete.sql"

function Write-SqlFile([string]$path, [string]$sql) {
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($path, $sql, $utf8NoBom)
}

# Protect imported cadastre when real_parcel_sources exists; if the table is absent
# (older demo DBs), DEMO-% alone is still a safe allowlist.
$guardExists = @'
SELECT (to_regclass('land_intelligence.real_parcel_sources') IS NOT NULL)::text;
'@
$guardSqlPath = Join-Path $tempDir "guard.sql"
Write-SqlFile $guardSqlPath $guardExists

$listWithGuard = @'
SELECT p."Id"::text AS id, p."CadastralNumber" AS cadastral
FROM land_intelligence.land_parcels p
WHERE p."CadastralNumber" ILIKE 'DEMO-%'
  AND NOT EXISTS (
    SELECT 1
    FROM land_intelligence.real_parcel_sources s
    WHERE s."LandParcelId" = p."Id"
  )
ORDER BY p."CadastralNumber";
'@

$listNoGuard = @'
SELECT p."Id"::text AS id, p."CadastralNumber" AS cadastral
FROM land_intelligence.land_parcels p
WHERE p."CadastralNumber" ILIKE 'DEMO-%'
ORDER BY p."CadastralNumber";
'@

$countWithGuard = @'
SELECT count(*)::text
FROM land_intelligence.land_parcels p
WHERE p."CadastralNumber" ILIKE 'DEMO-%'
  AND NOT EXISTS (
    SELECT 1
    FROM land_intelligence.real_parcel_sources s
    WHERE s."LandParcelId" = p."Id"
  );
'@

$countNoGuard = @'
SELECT count(*)::text
FROM land_intelligence.land_parcels p
WHERE p."CadastralNumber" ILIKE 'DEMO-%';
'@

$deleteWithGuard = @'
BEGIN;
DELETE FROM land_intelligence.land_parcels p
WHERE p."CadastralNumber" ILIKE 'DEMO-%'
  AND NOT EXISTS (
    SELECT 1
    FROM land_intelligence.real_parcel_sources s
    WHERE s."LandParcelId" = p."Id"
  );
COMMIT;
'@

$deleteNoGuard = @'
BEGIN;
DELETE FROM land_intelligence.land_parcels p
WHERE p."CadastralNumber" ILIKE 'DEMO-%';
COMMIT;
'@

try {
    Write-Host "Target database: $db @ ${hostName}:$port"
    Write-Host ""

    $hasGuardOut = & $psqlPath -h $hostName -p $port -U $user -d $db -v ON_ERROR_STOP=1 -t -A -f $guardSqlPath
    if ($LASTEXITCODE -ne 0) { throw "psql guard query failed (exit $LASTEXITCODE)." }
    $guardRaw = (($hasGuardOut | Out-String).Trim().ToLowerInvariant())
    $hasRealSources = ($guardRaw -eq "t" -or $guardRaw -eq "true")

    if ($hasRealSources) {
        Write-SqlFile $listSqlPath $listWithGuard
        Write-SqlFile $countSqlPath $countWithGuard
        Write-SqlFile $deleteSqlPath $deleteWithGuard
        Write-Host "Allowlisted candidates (DEMO-% cadastral, excluding rows with real_parcel_sources):"
    } else {
        Write-SqlFile $listSqlPath $listNoGuard
        Write-SqlFile $countSqlPath $countNoGuard
        Write-SqlFile $deleteSqlPath $deleteNoGuard
        Write-Host "Allowlisted candidates (DEMO-% cadastral; real_parcel_sources table absent):"
    }

    & $psqlPath -h $hostName -p $port -U $user -d $db -v ON_ERROR_STOP=1 -f $listSqlPath
    if ($LASTEXITCODE -ne 0) { throw "psql list query failed (exit $LASTEXITCODE)." }

    $countOut = & $psqlPath -h $hostName -p $port -U $user -d $db -v ON_ERROR_STOP=1 -t -A -f $countSqlPath
    if ($LASTEXITCODE -ne 0) { throw "psql count query failed (exit $LASTEXITCODE)." }
    $count = [int](($countOut | Out-String).Trim())
    Write-Host ""
    Write-Host "Dry-run count: $count"

    if (-not $Force) {
        Write-Host ""
        Write-Host "No rows deleted. Re-run with -Force to delete these $count allowlisted synthetic/demo parcel(s)."
        exit 0
    }

    if ($count -eq 0) {
        Write-Host "Nothing to delete."
        exit 0
    }

    Write-Host ""
    Write-Host "Deleting $count allowlisted synthetic/demo parcel(s)..."
    & $psqlPath -h $hostName -p $port -U $user -d $db -v ON_ERROR_STOP=1 -f $deleteSqlPath
    if ($LASTEXITCODE -ne 0) { throw "psql delete failed (exit $LASTEXITCODE)." }
    Write-Host "Done. Child rows cascade via FK where configured."
}
finally {
    Remove-Item -Recurse -Force $tempDir -ErrorAction SilentlyContinue
}
