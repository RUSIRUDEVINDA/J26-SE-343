$ErrorActionPreference = 'Stop'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$apiProject = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../src/Api'))
Push-Location $apiProject
try { dotnet run --no-launch-profile -- --setup-demo-accounts; if ($LASTEXITCODE -ne 0) { throw 'Account setup failed.' } }
finally { Pop-Location }
