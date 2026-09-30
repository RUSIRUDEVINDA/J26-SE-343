# Stop any running StateLandGovernance.Api (releases locked bin\Debug DLLs)
$processes = Get-Process -Name "StateLandGovernance.Api" -ErrorAction SilentlyContinue
if (-not $processes) {
    Write-Host "No StateLandGovernance.Api process is running."
    exit 0
}
$processes | ForEach-Object {
    Write-Host "Stopping PID $($_.Id)..."
    Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
}
Write-Host "Done. You can run .\run-api.ps1 again."
