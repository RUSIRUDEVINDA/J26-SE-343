# Run the Land Intelligence API (http://localhost:5264)
# Usage: from any directory — .\StateLandGovernance\run-api.ps1
# Or:    cd StateLandGovernance; .\run-api.ps1
#
# Expects LAND_INTELLIGENCE_CONNECTION (repo-root .env) to target imported real
# Colombo cadastre (e.g. land_intel_colombo_real_demo). Parcel search excludes DEMO-*.

$StopScript = Join-Path $PSScriptRoot "stop-api.ps1"
if (Test-Path $StopScript) {
    & $StopScript | Out-Null
}

$liConn = $env:LAND_INTELLIGENCE_CONNECTION
if ($liConn -match '(?i)Database=land_intel_colombo_demo\b') {
    Write-Warning "LAND_INTELLIGENCE_CONNECTION points at land_intel_colombo_demo (DEMO-* fixtures). Prefer land_intel_colombo_real_demo for the normal UI. DEMO-* rows are excluded from parcel search."
}

$Project = Join-Path $PSScriptRoot "src\Api\StateLandGovernance.Api.csproj"
if (-not (Test-Path $Project)) {
    Write-Error "API project not found: $Project"
    exit 1
}
dotnet run --project $Project
