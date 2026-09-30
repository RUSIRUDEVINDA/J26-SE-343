# Drops the disposable Colombo experimental verification database.

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
    throw "DatabaseName must contain 'colombo_exp_iso'."
}

if ($DatabaseName -eq "state_land_governance") {
    throw "Refusing to drop state_land_governance."
}

$env:PGPASSWORD = $Password
& $PsqlPath -h $PgHost -p $Port -U $Username -d postgres -v ON_ERROR_STOP=1 -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '$DatabaseName' AND pid <> pg_backend_pid();" | Out-Null
& $PsqlPath -h $PgHost -p $Port -U $Username -d postgres -v ON_ERROR_STOP=1 -c "DROP DATABASE IF EXISTS `"$DatabaseName`";"
Write-Host "Dropped database '$DatabaseName'."
