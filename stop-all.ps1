# Stop all running State Land Governance services (API, Frontend Web, Python ML, Blockchain)
# Usage: .\stop-all.ps1

Write-Host "Stopping all State Land Governance services..." -ForegroundColor Yellow

# 1. Stop Backend API
$apiProcesses = Get-Process -Name "StateLandGovernance.Api" -ErrorAction SilentlyContinue
if ($apiProcesses) {
    Write-Host "Stopping Backend API processes..." -ForegroundColor Gray
    $apiProcesses | Stop-Process -Force -ErrorAction SilentlyContinue
}

# 2. Stop Frontend Node processes
$nodeProcesses = Get-Process -Name "node" -ErrorAction SilentlyContinue
if ($nodeProcesses) {
    Write-Host "Stopping Next.js Node processes..." -ForegroundColor Gray
    $nodeProcesses | Stop-Process -Force -ErrorAction SilentlyContinue
}

# 3. Stop Python ML processes on port 8104
try {
    $mlPortConn = Get-NetTCPConnection -LocalPort 8104 -ErrorAction SilentlyContinue
    if ($mlPortConn) {
        $mlPortConn | ForEach-Object {
            Write-Host "Stopping Python ML process (PID $($_.OwningProcess))..." -ForegroundColor Gray
            Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue
        }
    }
} catch { }

# 4. Stop Blockchain Docker network if running
try {
    $besuCompose = Join-Path $PSScriptRoot "governance-blockchain-service\network\besu\docker-compose.yml"
    if (Test-Path $besuCompose) {
        docker compose -f $besuCompose down 2>$null
    }
} catch { }

Write-Host "All services stopped." -ForegroundColor Green
