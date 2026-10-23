param(
    [Parameter(Mandatory=$true)][string]$CandidateEnvironmentPath,
    [switch]$ValidateOnly,
    [switch]$ApprovedActivation
)
$ErrorActionPreference='Stop'
if (!$ValidateOnly -and !$ApprovedActivation) { throw 'Separate explicit ordinary activation approval is required. Use -ValidateOnly during preparation.' }
$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$evidence=Join-Path $repoRoot 'StateLandGovernance/docs/verification/owner-proposals/central-complaint-intake'
$package=Join-Path $repoRoot '.tmp/c4-manual-evidence-api'
$manifest=Get-Content -LiteralPath (Join-Path $evidence 'final-ds-evidence/PACKAGE_SHA256.json') -Raw | ConvertFrom-Json
foreach($p in $manifest.PSObject.Properties) {
    $file=[IO.Path]::GetFullPath((Join-Path $package $p.Name))
    if (!$file.StartsWith($package+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid package manifest path.' }
    if (!(Test-Path -LiteralPath $file) -or (Get-FileHash -Algorithm SHA256 -LiteralPath $file).Hash.ToLowerInvariant() -ne $p.Value) { throw 'Reviewed package hash mismatch.' }
}
$baseline=Get-Content -LiteralPath (Join-Path $repoRoot '.tmp/c4-rename-backup-20261023_0840/renamed-private-baseline.local.json') -Raw | ConvertFrom-Json
if ((Get-FileHash -Algorithm SHA256 -LiteralPath $baseline.ExistingPrivatePath).Hash.ToLowerInvariant() -ne $baseline.ExistingPrivateSha256) { throw 'Existing private environment changed; repeat preparation.' }
$original=Get-Content -LiteralPath $baseline.ExistingPrivatePath -Raw | ConvertFrom-Json
$candidate=Get-Content -LiteralPath $CandidateEnvironmentPath -Raw | ConvertFrom-Json
$expected=@{}
foreach($p in $original.PSObject.Properties) { $expected[$p.Name]=[string]$p.Value }
foreach($p in $baseline.OnlyCandidateDelta.PSObject.Properties) { $expected[$p.Name]=[string]$p.Value }
if (@($candidate.PSObject.Properties).Count -ne $expected.Count) { throw 'Unexpected candidate key count.' }
foreach($p in $candidate.PSObject.Properties) { if (!$expected.ContainsKey($p.Name) -or [string]$p.Value -cne $expected[$p.Name]) { throw 'Candidate differs from exact reviewed configuration delta.' } }
if ($candidate.ASPNETCORE_ENVIRONMENT -ne 'Development') { throw 'Development only.' }
foreach($key in @('LAND_INTELLIGENCE_CONNECTION','WORKFLOW_GOVERNANCE_CONNECTION','PORTAL_ACCOUNTS_CONNECTION')) {
    if ($candidate.$key -notlike '*default_transaction_read_only=on*') { throw 'Read-only peer/account protection missing.' }
}
foreach($key in @('GovernanceIntelligence__Blockchain__OutboxWorker__Enabled','GovernanceIntelligence__ComplaintSandbox__WritesEnabled','GovernanceIntelligence__WorkflowAnomalySandbox__WritesEnabled','GovernanceIntelligence__ConflictSandbox__WritesEnabled','GovernanceIntelligence__EarlyScreeningSandbox__WritesEnabled')) {
    if ($candidate.$key -ne 'false') { throw 'Worker/sandbox must remain explicitly disabled.' }
}
$connection=@{}
foreach($part in $candidate.GOVERNANCE_INTELLIGENCE_CONNECTION.Split(';')) { if($part.Contains('=')) {$pair=$part.Split('=',2);$connection[$pair[0].Trim().ToLowerInvariant()]=$pair[1].Trim().Trim('"')} }
if ($connection.host -notin @('localhost','127.0.0.1') -or $connection.database -ne 'state_land_governance_c4' -or $connection.username -ne 'slg_c4_regulatory_runtime' -or ($connection.ContainsKey('port') -and $connection.port -ne '5432')) { throw 'Wrong C4 target/runtime identity.' }
$grantPath=$candidate.GovernanceIntelligence__CentralComplaints__GrantFilePath
if (![IO.Path]::IsPathRooted($grantPath)) { throw 'Server permission path must be absolute.' }
$document=Get-Content -LiteralPath $grantPath -Raw | ConvertFrom-Json
$reviewed=Get-Content -LiteralPath (Join-Path $evidence 'PERSISTENT_PROPOSED_GRANTS.json') -Raw | ConvertFrom-Json
if ($document.SchemaVersion -ne 'c4-central-permissions/1' -or @($document.PSObject.Properties).Count -ne 2 -or @($document.Grants).Count -ne 1) { throw 'Unexpected server grant document.' }
$grant=@($document.Grants)[0];$expectedGrant=@($reviewed.Grants)[0]
if (@($grant.PSObject.Properties).Count -ne @($expectedGrant.PSObject.Properties).Count) { throw 'Unexpected grant fields or expiry.' }
foreach($p in $expectedGrant.PSObject.Properties) {
    if($p.Name -eq 'Revoked') { if($grant.Revoked -isnot [bool]) { throw 'Revoked must be explicit boolean.' };continue }
    $actualProperty=$grant.PSObject.Properties[$p.Name]
    if($null -eq $actualProperty -or (ConvertTo-Json -InputObject $actualProperty.Value -Depth 10 -Compress) -cne (ConvertTo-Json -InputObject $p.Value -Depth 10 -Compress)) { throw 'Persistent grant differs from exact reviewed actor/permissions/source.' }
}
if($grant.Persistent -ne $true -or $null -ne $grant.ExpiresAtUtc) { throw 'Expected explicit persistent permission, with no expiry workaround.' }
$reportPath=$candidate.GovernanceIntelligence__CommissionerReportReads__GrantFilePath
if ($candidate.GovernanceIntelligence__CommissionerReportReads__Enabled -ne 'true' -or $reportPath -cne $baseline.ReportGrantPath) { throw 'Exact separate report-read source required.' }
$report=Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
$reportReviewed=Get-Content -LiteralPath (Join-Path $evidence 'ui-polish-and-report-read-review/PROPOSED_REPORT_READ_GRANT.json') -Raw | ConvertFrom-Json
if ($report.SchemaVersion -ne 'c4-central-permissions/1' -or @($report.PSObject.Properties).Count -ne 2 -or @($report.Grants).Count -ne 1) { throw 'Unexpected report grant document.' }
$r=@($report.Grants)[0];$er=@($reportReviewed.Grants)[0]
if (@($r.PSObject.Properties).Count -ne @($er.PSObject.Properties).Count) { throw 'Unexpected report permission fields.' }
foreach($p in $er.PSObject.Properties) {
    if($p.Name -eq 'Revoked') { if($r.Revoked -isnot [bool]) { throw 'Report revoked flag must be boolean.' };continue }
    $actual=$r.PSObject.Properties[$p.Name]
    if($null -eq $actual -or (ConvertTo-Json -InputObject $actual.Value -Depth 10 -Compress) -cne (ConvertTo-Json -InputObject $p.Value -Depth 10 -Compress)) { throw 'Report authority differs from exact reviewed grant.' }
}
if($ValidateOnly) {
    Write-Output ('Validated persistent grant/package/exact candidate protections. Revoked='+$grant.Revoked+'. No environment loaded or process started.')
    return
}
if([DateTimeOffset]::UtcNow -lt [DateTimeOffset]::Parse($grant.ValidFromUtc)) { throw 'Grant provisioning date is in the future.' }
if(Get-NetTCPConnection -State Listen -LocalPort 5264 -ErrorAction SilentlyContinue) { throw '5264 already has a listener. Identify and stop the approved prior API before replacement.' }
# SQL/history/grants must have been separately approved and verified immediately before this launch.
$saved=@{}
try {
    foreach($p in $candidate.PSObject.Properties) { $saved[$p.Name]=[Environment]::GetEnvironmentVariable($p.Name,'Process');[Environment]::SetEnvironmentVariable($p.Name,[string]$p.Value,'Process') }
    $api=Start-Process -WindowStyle Hidden -FilePath (Join-Path $package 'StateLandGovernance.Api.exe') -WorkingDirectory (Join-Path $repoRoot 'StateLandGovernance/src/Api') -RedirectStandardOutput (Join-Path $repoRoot '.tmp/c4-evidence-api.stdout.log') -RedirectStandardError (Join-Path $repoRoot '.tmp/c4-evidence-api.stderr.log') -PassThru
    Write-Output ('Started one approved API PID '+$api.Id+'. Reauthenticate. Do not restart an older binary as a handling-policy rollback.')
} finally { foreach($key in $saved.Keys) { [Environment]::SetEnvironmentVariable($key,$saved[$key],'Process') } }
