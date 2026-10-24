param([int]$Port = 8009)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$servicePath = Join-Path $projectRoot 'src/Modules/WorkflowGovernance/document-intelligence-service'
Push-Location -LiteralPath $servicePath
try {
    # Uses the service's existing .env. This service exposes HTTP endpoints only.
    # Avoid Uvicorn's incompatible auto-selected WebSockets package in the local Python installation.
    & python -m uvicorn app.main:app --host 127.0.0.1 --port $Port --ws none
    if ($LASTEXITCODE -ne 0) { throw 'Document-intelligence service exited unsuccessfully.' }
}
finally { Pop-Location }
