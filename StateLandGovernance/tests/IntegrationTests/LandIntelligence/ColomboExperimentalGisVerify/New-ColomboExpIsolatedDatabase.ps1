# Requires PostgreSQL 17+ with PostGIS available (CREATE EXTENSION postgis).
# Creates a disposable database. Does NOT touch state_land_governance.

param(
    [string]$PgHost = "localhost",
    [int]$Port = 5432,
    [string]$Username = "postgres",
    [string]$Password = "postgres",
    [string]$DatabaseName = "land_intel_colombo_exp_iso",
    [string]$PsqlPath = "C:\Program Files\PostgreSQL\17\bin\psql.exe"
)

$ErrorActionPreference = "Stop"

if ($DatabaseName -notmatch "colombo_exp_iso") {
    throw "DatabaseName must contain 'colombo_exp_iso' (got '$DatabaseName')."
}

if ($DatabaseName -in @("state_land_governance", "postgres", "template0", "template1")) {
    throw "Refusing protected database name '$DatabaseName'."
}

if (-not (Test-Path $PsqlPath)) {
    throw "psql not found at '$PsqlPath'. Install PostgreSQL/PostGIS or update -PsqlPath."
}

$env:PGPASSWORD = $Password

Write-Host "Creating disposable database '$DatabaseName' on ${PgHost}:${Port}..."
& $PsqlPath -h $PgHost -p $Port -U $Username -d postgres -v ON_ERROR_STOP=1 -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '$DatabaseName' AND pid <> pg_backend_pid();" | Out-Null
& $PsqlPath -h $PgHost -p $Port -U $Username -d postgres -v ON_ERROR_STOP=1 -c "DROP DATABASE IF EXISTS `"$DatabaseName`";"
& $PsqlPath -h $PgHost -p $Port -U $Username -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE `"$DatabaseName`";"
& $PsqlPath -h $PgHost -p $Port -U $Username -d $DatabaseName -v ON_ERROR_STOP=1 -c "CREATE EXTENSION IF NOT EXISTS postgis;"

$connection = "Host=$PgHost;Port=$Port;Database=$DatabaseName;Username=$Username;Password=$Password"
$env:COLUMBO_EXP_ISOLATED_CONNECTION = $connection
Write-Host "Created. Set this for the verify run ONLY:"
Write-Host "`$env:COLUMBO_EXP_ISOLATED_CONNECTION='$connection'"
Write-Host "Do not point LAND_INTELLIGENCE_CONNECTION in .env at this database for normal app use."
