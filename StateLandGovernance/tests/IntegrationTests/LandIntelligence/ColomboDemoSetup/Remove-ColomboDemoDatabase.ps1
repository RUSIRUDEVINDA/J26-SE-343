# Explicit cleanup for the durable Colombo demo database.
# This is the ONLY script that drops land_intel_colombo_demo.
# Setup never calls this.

param(
    [string]$PgHost = "localhost",
    [int]$Port = 5432,
    [string]$Username = "postgres",
    [string]$Password = "",
    [string]$DatabaseName = "land_intel_colombo_demo",
    [string]$PsqlPath = "C:\Program Files\PostgreSQL\17\bin\psql.exe",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

if ($DatabaseName -ne "land_intel_colombo_demo") {
    throw "Cleanup only targets 'land_intel_colombo_demo' (got '$DatabaseName')."
}

if ($DatabaseName -in @("state_land_governance", "postgres", "template0", "template1")) {
    throw "Refusing protected database name '$DatabaseName'."
}

if (-not $Force) {
    throw "Pass -Force to drop '$DatabaseName'. This is irreversible for that local demo database."
}

if ([string]::IsNullOrWhiteSpace($Password)) {
    $Password = $env:PGPASSWORD
}
if ([string]::IsNullOrWhiteSpace($Password)) {
    throw 'Set -Password or $env:PGPASSWORD (password is not committed).'
}

if (-not (Test-Path $PsqlPath)) {
    throw "psql not found at '$PsqlPath'."
}

$env:PGPASSWORD = $Password

Write-Host "Terminating sessions and dropping '$DatabaseName'..."
$terminateSql = "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '$DatabaseName' AND pid <> pg_backend_pid();"
& $PsqlPath -h $PgHost -p $Port -U $Username -d postgres -v ON_ERROR_STOP=1 -c $terminateSql | Out-Null
$dropSql = 'DROP DATABASE IF EXISTS "' + $DatabaseName + '";'
& $PsqlPath -h $PgHost -p $Port -U $Username -d postgres -v ON_ERROR_STOP=1 -c $dropSql
if ($LASTEXITCODE -ne 0) {
    throw 'DROP DATABASE failed.'
}

Write-Host "Dropped '$DatabaseName'."
