# Creates land_intel_colombo_demo if missing, then runs idempotent GIS import + demo fixtures.
# Does NOT drop or reset an existing database. Does NOT touch state_land_governance.
#
# Password is never written to disk by this script. Supply via:
#   -Password <value>   OR   $env:PGPASSWORD   OR   pre-set $env:LAND_INTELLIGENCE_DEMO_CONNECTION

param(
    [string]$PgHost = "localhost",
    [int]$Port = 5432,
    [string]$Username = "postgres",
    [string]$Password = "",
    [string]$DatabaseName = "land_intel_colombo_demo",
    [string]$PsqlPath = "C:\Program Files\PostgreSQL\17\bin\psql.exe",
    [string]$OsmGeoJsonRelativePath = "data\gis\experiments\colombo\osm_motor_roads_colombo_buffer.geojson"
)

$ErrorActionPreference = "Stop"

if ($DatabaseName -ne "land_intel_colombo_demo") {
    throw "This setup only creates/uses 'land_intel_colombo_demo' (got '$DatabaseName')."
}

if ($DatabaseName -in @("state_land_governance", "postgres", "template0", "template1")) {
    throw "Refusing protected database name '$DatabaseName'."
}

$solutionRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..")).Path
if (-not (Test-Path (Join-Path $solutionRoot "StateLandGovernance.sln"))) {
    throw "Could not resolve solution root from '$PSScriptRoot'."
}

Set-Location $solutionRoot

if (-not $env:LAND_INTELLIGENCE_DEMO_CONNECTION) {
    if ([string]::IsNullOrWhiteSpace($Password)) {
        $Password = $env:PGPASSWORD
    }
    if ([string]::IsNullOrWhiteSpace($Password)) {
        throw 'Set -Password, $env:PGPASSWORD, or $env:LAND_INTELLIGENCE_DEMO_CONNECTION (password is not committed).'
    }

    $env:LAND_INTELLIGENCE_DEMO_CONNECTION =
        "Host=$PgHost;Port=$Port;Database=$DatabaseName;Username=$Username;Password=$Password"
}

if (-not (Test-Path $PsqlPath)) {
    throw "psql not found at '$PsqlPath'. Install PostgreSQL/PostGIS or update -PsqlPath."
}

$conn = $env:LAND_INTELLIGENCE_DEMO_CONNECTION
if ($conn -match '(?i)Password=([^;]+)') {
    $env:PGPASSWORD = $Matches[1]
}

Write-Host "Ensuring database '$DatabaseName' exists (create-if-missing; never DROP)..."
$existsSql = "SELECT 1 FROM pg_database WHERE datname = '$DatabaseName';"
$exists = & $PsqlPath -h $PgHost -p $Port -U $Username -d postgres -tAc $existsSql
if ($LASTEXITCODE -ne 0) {
    throw 'Failed to query PostgreSQL for database existence.'
}

if ([string]::IsNullOrWhiteSpace($exists)) {
    Write-Host "Creating database '$DatabaseName'..."
    $createSql = 'CREATE DATABASE "' + $DatabaseName + '";'
    & $PsqlPath -h $PgHost -p $Port -U $Username -d postgres -v ON_ERROR_STOP=1 -c $createSql
    if ($LASTEXITCODE -ne 0) {
        throw 'CREATE DATABASE failed.'
    }
}
else {
    Write-Host "Database '$DatabaseName' already exists - leaving it in place."
}

$previousErrorAction = $ErrorActionPreference
$ErrorActionPreference = "Continue"
& $PsqlPath -h $PgHost -p $Port -U $Username -d $DatabaseName -v ON_ERROR_STOP=1 -c 'CREATE EXTENSION IF NOT EXISTS postgis;' | Out-Null
$extExit = $LASTEXITCODE
$ErrorActionPreference = $previousErrorAction
if ($extExit -ne 0) {
    throw 'CREATE EXTENSION postgis failed.'
}

$geoJson = Join-Path $solutionRoot $OsmGeoJsonRelativePath
if (-not (Test-Path $geoJson)) {
    Write-Host "Buffered OSM GeoJSON missing - exporting (Colombo + 25 km)..."
    py src\Modules\LandIntelligence\ML\tools\export_osm_motor_roads_geojson.py --district-buffer-km 25
    if (-not (Test-Path $geoJson)) {
        throw "Expected GeoJSON at '$geoJson' after export."
    }
}

# Process-local only: point Land Intelligence DI at the demo DB. Does not rewrite .env.
$env:LAND_INTELLIGENCE_CONNECTION = $env:LAND_INTELLIGENCE_DEMO_CONNECTION

$project = Join-Path $PSScriptRoot "ColomboDemoSetup.csproj"
Write-Host "Running idempotent demo setup against '$DatabaseName'..."
dotnet run --project $project -- $geoJson
exit $LASTEXITCODE
