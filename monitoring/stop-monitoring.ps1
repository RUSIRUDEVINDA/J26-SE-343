$ErrorActionPreference = "Stop"
$monitoringDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
docker compose -f (Join-Path $monitoringDirectory "docker-compose.yml") down
