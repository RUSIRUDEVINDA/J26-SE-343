# Starts the full API host against land_intel_colombo_demo with Colombo GIS coverage
# and experimental ML forced off. Does not rewrite .env. Password stays in-process only.

param(
    [string]$Urls = "http://127.0.0.1:5264"
)

$ErrorActionPreference = "Stop"

$solutionRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..")).Path
if (-not (Test-Path (Join-Path $solutionRoot "StateLandGovernance.sln"))) {
    throw "Could not resolve solution root from '$PSScriptRoot'."
}

if (-not $env:LAND_INTELLIGENCE_DEMO_CONNECTION) {
    throw "Set `$env:LAND_INTELLIGENCE_DEMO_CONNECTION first (same value used by Setup-ColomboDemoDatabase.ps1)."
}

$dbName = ""
if ($env:LAND_INTELLIGENCE_DEMO_CONNECTION -match '(?i)Database=([^;]+)') {
    $dbName = $Matches[1]
}
if ($dbName -ne "land_intel_colombo_demo") {
    throw "LAND_INTELLIGENCE_DEMO_CONNECTION must target land_intel_colombo_demo (got '$dbName')."
}

Set-Location $solutionRoot

$env:LAND_INTELLIGENCE_CONNECTION = $env:LAND_INTELLIGENCE_DEMO_CONNECTION
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = $Urls

# Process-local GIS coverage for the demo DB (does not change committed appsettings defaults).
$env:LandIntelligence__GisEnrichmentCoverage__SupportedDistrictNames__0 = "Hambantota"
$env:LandIntelligence__GisEnrichmentCoverage__SupportedDistrictNames__1 = "Colombo"
$env:LandIntelligence__GisEnrichmentCoverage__RoadSourceLayer = "expressways"
$env:LandIntelligence__GisEnrichmentCoverage__OsmMotorRoadFilterPolicyVersion = "2026-03-20-colombo-v1"
$env:LandIntelligence__GisEnrichmentCoverage__DistrictRoadSourceLayers__0__DistrictName = "Hambantota"
$env:LandIntelligence__GisEnrichmentCoverage__DistrictRoadSourceLayers__0__RoadSourceLayer = "expressways"
$env:LandIntelligence__GisEnrichmentCoverage__DistrictRoadSourceLayers__1__DistrictName = "Colombo"
$env:LandIntelligence__GisEnrichmentCoverage__DistrictRoadSourceLayers__1__RoadSourceLayer = "osm_motor_roads"
$env:LandIntelligence__ExperimentalColomboMl__Enabled = "false"

Write-Host "Starting API on $Urls against land_intel_colombo_demo (ExperimentalColomboMl=false)..."
dotnet run --project src\Api\StateLandGovernance.Api.csproj --urls $Urls
