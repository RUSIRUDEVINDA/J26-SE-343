$ErrorActionPreference = "Stop"

$monitoringDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$besuCompose = Join-Path $monitoringDirectory "..\governance-blockchain-service\network\besu\docker-compose.yml"
$monitoringCompose = Join-Path $monitoringDirectory "docker-compose.yml"

Write-Host "Ensuring the Besu network exists..."
docker compose -f $besuCompose up -d
if ($LASTEXITCODE -ne 0) {
    throw "Besu Compose startup failed with exit code $LASTEXITCODE."
}

Write-Host "Starting Prometheus, Blackbox Exporter, and Grafana..."
docker compose -f $monitoringCompose up -d
if ($LASTEXITCODE -ne 0) {
    throw "Monitoring Compose startup failed with exit code $LASTEXITCODE."
}

Write-Host ""
Write-Host "Grafana:    http://localhost:3001  (default local login: admin / admin)"
Write-Host "Dashboard:  http://localhost:3001/d/state-land-viva-e2e/e61a9cb"
Write-Host "Prometheus: http://localhost:9090/targets"
