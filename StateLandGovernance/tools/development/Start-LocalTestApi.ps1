param([switch]$EnableLocalTestSignIn)
$ErrorActionPreference='Stop'
if (-not $EnableLocalTestSignIn) { throw 'Explicit opt-in required: -EnableLocalTestSignIn. Test accounts are local Development only.' }
$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:ASPNETCORE_URLS='http://localhost:5264'
$env:Logging__EventLog__LogLevel__Default='None'
$env:LocalDevelopmentSignIn__Enabled='true'
Push-Location (Join-Path $repoRoot 'src/Api')
try { dotnet run --no-launch-profile } finally { Pop-Location }
