# Runs isolated Colombo experimental GIS verification against COLUMBO_EXP_ISOLATED_CONNECTION.
# Must be executed from the StateLandGovernance solution root.

param(
    [string]$OsmGeoJsonRelativePath = "data\gis\experiments\colombo\osm_motor_roads_colombo_buffer.geojson"
)

$ErrorActionPreference = "Stop"

$solutionRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..")).Path
if (-not (Test-Path (Join-Path $solutionRoot "StateLandGovernance.sln"))) {
    throw "Could not resolve solution root from '$PSScriptRoot'."
}

Set-Location $solutionRoot

if (-not $env:COLUMBO_EXP_ISOLATED_CONNECTION) {
    throw "Set COLUMBO_EXP_ISOLATED_CONNECTION first (use New-ColomboExpIsolatedDatabase.ps1)."
}

# Process-local only: point DI at the isolated DB. Does not rewrite .env.
$env:LAND_INTELLIGENCE_CONNECTION = $env:COLUMBO_EXP_ISOLATED_CONNECTION

$geoJson = Join-Path $solutionRoot $OsmGeoJsonRelativePath
if (-not (Test-Path $geoJson)) {
    Write-Host "Exporting buffered OSM GeoJSON (Colombo + 25 km)..."
    py src\Modules\LandIntelligence\ML\tools\export_osm_motor_roads_geojson.py --district-buffer-km 25
    if (-not (Test-Path $geoJson)) {
        throw "Expected GeoJSON at '$geoJson' after export."
    }
}

$project = Join-Path $PSScriptRoot "ColomboExperimentalGisVerify.csproj"
Write-Host "Running verification against isolated DB..."
dotnet run --project $project -- $geoJson
exit $LASTEXITCODE
