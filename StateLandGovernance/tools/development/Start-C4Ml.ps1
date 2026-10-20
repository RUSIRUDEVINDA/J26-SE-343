$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$runtime = Join-Path $repoRoot '.tmp/c4-ml-venv/Scripts/python.exe'
if (!(Test-Path -LiteralPath $runtime)) { throw 'Create the isolated C4 virtual environment using the PP1 startup guide first.' }
Push-Location (Join-Path $repoRoot 'StateLandGovernance/src/Modules/GovernanceIntelligence/ML')
try {
    & $runtime -m anomaly_detection.cli verify-bundle
    if ($LASTEXITCODE -ne 0) { throw 'Frozen anomaly bundle verification failed. No service was started.' }
    & $runtime -m uvicorn src.http_service:app --host 127.0.0.1 --port 8104 --ws none
} finally { Pop-Location }
