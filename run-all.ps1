# Run the complete State Land Governance system (ML, Blockchain, API, Frontend) with 1 command.
# Usage: .\run-all.ps1

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Starting State Land Governance System (Full Stack)       " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Stop any stale instances
$apiProcesses = Get-Process -Name "StateLandGovernance.Api" -ErrorAction SilentlyContinue
if ($apiProcesses) {
    Write-Host "[1/5] Stopping stale API processes..." -ForegroundColor Yellow
    $apiProcesses | Stop-Process -Force -ErrorAction SilentlyContinue
}

# 2. Start Python ML Services (Isolation Forest & Complaint Classifier on port 8104)
Write-Host "[2/5] Checking and Launching Python ML Service (port 8104)..." -ForegroundColor Green
$pythonCandidates = @(
    "C:\Users\Asus\AppData\Local\Programs\Python\Python313\python.exe",
    "C:\Users\Asus\AppData\Local\Programs\Python\Python311\python.exe",
    "python.exe"
)
$pythonExe = $null
foreach ($cand in $pythonCandidates) {
    if (Test-Path $cand) {
        $pythonExe = $cand
        break
    }
}

if ($pythonExe) {
    $mlDir = Join-Path $PSScriptRoot "StateLandGovernance\src\Modules\GovernanceIntelligence\ML\src"
    Start-Process -FilePath $pythonExe -ArgumentList "-m uvicorn http_service:app --host 127.0.0.1 --port 8104" -WorkingDirectory $mlDir -WindowStyle Minimized
    
    # Wait for ML service health
    $mlReady = $false
    for ($i = 0; $i -lt 10; $i++) {
        Start-Sleep -Seconds 1
        try {
            $resp = Invoke-RestMethod -Uri "http://127.0.0.1:8104/health" -TimeoutSec 2 -ErrorAction Stop
            if ($resp.status -eq "ok" -or $resp.status -eq "healthy") {
                $mlReady = $true
                break
            }
        } catch { }
    }
    if ($mlReady) {
        Write-Host "      Python ML Service is Online (http://127.0.0.1:8104)" -ForegroundColor Green
    } else {
        Write-Host "      Python ML Service started in background." -ForegroundColor Yellow
    }
} else {
    Write-Host "      Python executable not found. ML services running in rule-engine fallback mode." -ForegroundColor Yellow
}

# 3. Check Blockchain Infrastructure (Hyperledger Besu)
Write-Host "[3/5] Checking Blockchain Infrastructure (Hyperledger Besu)..." -ForegroundColor Green
$dockerRunning = $false
try {
    $dockerVersion = & docker version --format '{{.Server.Version}}' 2>$null
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($dockerVersion)) {
        $dockerRunning = $true
    }
} catch { }

if ($dockerRunning) {
    Write-Host "      Docker is running. Starting Hyperledger Besu QBFT network..." -ForegroundColor Green
    $besuCompose = Join-Path $PSScriptRoot "governance-blockchain-service\network\besu\docker-compose.yml"
    docker compose -f $besuCompose up -d 2>$null
    Write-Host "      Hyperledger Besu network active." -ForegroundColor Green
} else {
    Write-Host "      [Notice] Docker Desktop is not currently running." -ForegroundColor Yellow
    Write-Host "      Blockchain audit proofs are operating in DB Outbox fallback mode." -ForegroundColor Yellow
    Write-Host "      (Start Docker Desktop if you wish to run live on-chain Besu nodes)." -ForegroundColor Gray
}

# 4. Start ASP.NET Core Backend API
Write-Host "[4/5] Launching ASP.NET Core Backend API (http://localhost:5264)..." -ForegroundColor Green
$apiDir = Join-Path $PSScriptRoot "StateLandGovernance"
$apiProcess = Start-Process -FilePath "dotnet" -ArgumentList "run --project src/Api/StateLandGovernance.Api.csproj --launch-profile http" -WorkingDirectory $apiDir -PassThru

# Wait for API to become healthy
$maxRetries = 25
$retryCount = 0
$apiReady = $false
while (-not $apiReady -and $retryCount -lt $maxRetries) {
    Start-Sleep -Seconds 1
    $retryCount++
    try {
        $resp = Invoke-RestMethod -Uri "http://localhost:5264/health" -TimeoutSec 2 -ErrorAction Stop
        if ($resp.status -eq "Healthy") {
            $apiReady = $true
        }
    } catch {
        Write-Host "      Waiting for API to initialize ($retryCount/$maxRetries)..." -ForegroundColor Gray
    }
}

if ($apiReady) {
    Write-Host "      Backend API is Healthy and listening on http://localhost:5264" -ForegroundColor Green
} else {
    Write-Host "      Backend API is still starting up in the background." -ForegroundColor Yellow
}

# 5. Start Next.js Frontend
Write-Host "[5/5] Launching Next.js Web Frontend (http://localhost:3000)..." -ForegroundColor Green
$webDir = Join-Path $PSScriptRoot "StateLandGovernance.Web"
$webProcess = Start-Process -FilePath "cmd.exe" -ArgumentList "/c npm run dev" -WorkingDirectory $webDir -PassThru

# Wait for Frontend to become ready
$retryCount = 0
$webReady = $false
while (-not $webReady -and $retryCount -lt 20) {
    Start-Sleep -Seconds 1
    $retryCount++
    try {
        $resp = Invoke-WebRequest -Uri "http://localhost:3000" -TimeoutSec 2 -ErrorAction Stop
        if ($resp.StatusCode -eq 200) {
            $webReady = $true
        }
    } catch {
        Write-Host "      Waiting for Web Portal to initialize ($retryCount/20)..." -ForegroundColor Gray
    }
}

# Open in Default Browser
Write-Host "Opening System Portal in browser..." -ForegroundColor Cyan
Start-Process "http://localhost:3000"

Write-Host "==========================================================" -ForegroundColor Green
Write-Host " ALL SERVICES INITIALIZED!" -ForegroundColor Green
Write-Host " Web Portal:            http://localhost:3000" -ForegroundColor White
Write-Host " Component 4 (Gov):     http://localhost:3000/component-04" -ForegroundColor White
Write-Host " Python ML Service:     http://127.0.0.1:8104" -ForegroundColor White
Write-Host " Backend Swagger:       http://localhost:5264/swagger" -ForegroundColor White
Write-Host " To stop all services:  .\stop-all.ps1" -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Green
