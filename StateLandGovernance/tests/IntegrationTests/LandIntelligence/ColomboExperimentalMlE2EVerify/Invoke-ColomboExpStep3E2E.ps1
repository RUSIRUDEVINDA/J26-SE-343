# Step-3 opt-in E2E: disposable PostGIS + experimental_ml_service.py + recommendation verify.
# Does NOT modify state_land_governance, ml_service.py, or live output/ artifacts.
# Must be run from any directory; resolves StateLandGovernance solution root.

param(
    [string]$OsmGeoJsonRelativePath = "data\gis\experiments\colombo\osm_motor_roads_colombo_buffer.geojson",
    [int]$ExperimentalMlPort = 8501,
    [switch]$SkipDatabaseCreate,
    [switch]$SkipCleanup
)

$ErrorActionPreference = "Stop"

$solutionRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..")).Path
if (-not (Test-Path (Join-Path $solutionRoot "StateLandGovernance.sln"))) {
    throw "Could not resolve solution root from '$PSScriptRoot'."
}

Set-Location $solutionRoot

$newDb = Join-Path $PSScriptRoot "..\ColomboExperimentalGisVerify\New-ColomboExpIsolatedDatabase.ps1"
$removeDb = Join-Path $PSScriptRoot "..\ColomboExperimentalGisVerify\Remove-ColomboExpIsolatedDatabase.ps1"
$verifyProj = Join-Path $PSScriptRoot "ColomboExperimentalMlE2EVerify.csproj"
$mlDir = Join-Path $solutionRoot "src\Modules\LandIntelligence\ML"
$expService = Join-Path $mlDir "experimental_ml_service.py"
$modelDir = Join-Path $mlDir "output_colombo_osm_backend_compatible"

if (-not (Test-Path $expService)) {
    throw "Missing experimental_ml_service.py at $expService"
}
if (-not (Test-Path (Join-Path $modelDir "random_forest_pipeline.joblib"))) {
    throw "Missing backend-compatible model artifacts under $modelDir"
}

if (-not $SkipDatabaseCreate) {
    Write-Host "Creating disposable isolated database..."
    & $newDb
}

if (-not $env:COLUMBO_EXP_ISOLATED_CONNECTION) {
    throw "Set COLUMBO_EXP_ISOLATED_CONNECTION first (use New-ColomboExpIsolatedDatabase.ps1)."
}

# Process-local only — do not rewrite .env
$env:LAND_INTELLIGENCE_CONNECTION = $env:COLUMBO_EXP_ISOLATED_CONNECTION
# Isolate from Neo4j retries for this disposable verify process
Remove-Item Env:NEO4J_CONNECTION -ErrorAction SilentlyContinue
Remove-Item Env:NEO4J_USERNAME -ErrorAction SilentlyContinue
Remove-Item Env:NEO4J_PASSWORD -ErrorAction SilentlyContinue

$geoJson = Join-Path $solutionRoot $OsmGeoJsonRelativePath
if (-not (Test-Path $geoJson)) {
    Write-Host "Exporting buffered OSM GeoJSON (Colombo + 25 km)..."
    py src\Modules\LandIntelligence\ML\tools\export_osm_motor_roads_geojson.py --district-buffer-km 25
    if (-not (Test-Path $geoJson)) {
        throw "Expected GeoJSON at '$geoJson' after export."
    }
}

Write-Host "Starting experimental ML service on port $ExperimentalMlPort..."
$env:EXPERIMENTAL_COLOMBO_ML_PORT = "$ExperimentalMlPort"
$env:EXPERIMENTAL_COLOMBO_ML_MODEL_DIR = $modelDir
$mlProc = Start-Process -FilePath "py" -ArgumentList @(
    "-m", "uvicorn", "experimental_ml_service:app",
    "--host", "127.0.0.1",
    "--port", "$ExperimentalMlPort"
) -WorkingDirectory $mlDir -PassThru -WindowStyle Hidden

try {
    $healthy = $false
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Seconds 1
        try {
            $health = Invoke-RestMethod -Uri "http://127.0.0.1:$ExperimentalMlPort/health" -TimeoutSec 2
            if ($health.status -eq "ok" -and $health.model_identity.candidate -eq "colombo_osm_backend_compatible") {
                $healthy = $true
                Write-Host "Experimental ML healthy. candidate=$($health.model_identity.candidate)"
                break
            }
        }
        catch {
            # retry
        }
    }
    if (-not $healthy) {
        throw "experimental_ml_service did not become healthy on port $ExperimentalMlPort"
    }

    Write-Host "Running Step-3 E2E verify..."
    dotnet run --project $verifyProj -- $geoJson
    $code = $LASTEXITCODE
}
finally {
    if ($null -ne $mlProc -and -not $mlProc.HasExited) {
        Write-Host "Stopping experimental ML process $($mlProc.Id)..."
        Stop-Process -Id $mlProc.Id -Force -ErrorAction SilentlyContinue
    }
    if (-not $SkipCleanup -and (Test-Path $removeDb)) {
        Write-Host "Dropping disposable isolated database..."
        & $removeDb
        Remove-Item Env:COLUMBO_EXP_ISOLATED_CONNECTION -ErrorAction SilentlyContinue
        Remove-Item Env:LAND_INTELLIGENCE_CONNECTION -ErrorAction SilentlyContinue
    }
}

exit $code
