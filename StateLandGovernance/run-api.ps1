# Run the Land Intelligence API (http://localhost:5264)
# Usage: from any directory — .\StateLandGovernance\run-api.ps1
# Or:    cd StateLandGovernance; .\run-api.ps1

$StopScript = Join-Path $PSScriptRoot "stop-api.ps1"
if (Test-Path $StopScript) {
    & $StopScript | Out-Null
}

$Project = Join-Path $PSScriptRoot "src\Api\StateLandGovernance.Api.csproj"
if (-not (Test-Path $Project)) {
    Write-Error "API project not found: $Project"
    exit 1
}
dotnet run --project $Project
