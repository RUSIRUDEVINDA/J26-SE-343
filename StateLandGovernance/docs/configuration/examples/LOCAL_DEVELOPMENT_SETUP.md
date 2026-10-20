# Local Development configuration - sanitized examples

These examples contain no working password verifier, existing case owner, active grant or production authority. Do not overwrite existing local files. The current working files were preserved by this review. Keep both runtime JSON files out of Git; do not run git add src/Api or git add .

## What actually loads the files

- `src/Api/Program.cs` loads `development-accounts.local.json` with optional:false only when the host is Development and LocalDevelopmentSignIn:Enabled is explicitly enabled before loading it. It uses reloadOnChange:false. That file is required for opted-in sign-in, not ordinary production startup.
- `tools/development/Start-LocalTestApi.ps1 -EnableLocalTestSignIn` sets Development, the opt-in, and loopback port 5264. It does not create either JSON file or set spatial options.
- `DevelopmentSpatialInputProvider` reads the configured WorkflowGovernance:Screening:DevelopmentSpatialFixturePath lazily. It additionally requires Development, EnableDevelopmentSpatialMocks and a selected parcel. The spatial JSON is unnecessary for sign-in alone; it is needed for the current uploaded-case research selector's no-conflict/overlap contexts.
- `UploadedCaseResearchSpatialProvider` reuses the two fixed synthetic fixture rows under the reserved Development case identifier. Those identifiers describe supported input fixtures, not permissions. Durable case/document assignment and explicit synthetic designation still authorize an uploaded case.

## New local checkout, without replacing existing work

From the workspace root, copy each example only if its destination does not exist:

```powershell
if (!(Test-Path -LiteralPath StateLandGovernance/src/Api/development-accounts.local.json)) {
    Copy-Item -LiteralPath StateLandGovernance/docs/configuration/examples/development-accounts.example.json -Destination StateLandGovernance/src/Api/development-accounts.local.json
}
if (!(Test-Path -LiteralPath StateLandGovernance/src/Api/portal-development-spatial.json)) {
    Copy-Item -LiteralPath StateLandGovernance/docs/configuration/examples/portal-development-spatial.example.json -Destination StateLandGovernance/src/Api/portal-development-spatial.json
}
```

Both destinations must stay local. Add their repository-relative paths to your own `.git/info/exclude` if absent, or propose narrowly scoped .gitignore entries separately. This review did not edit either ignore file.

1. For new fictional accounts replace each ActorId placeholder with a distinct stable UUID generated using `[guid]::NewGuid()`. Do not map a fictional applicant onto an existing case owner. Preserve existing actor mappings when testing an existing installation.
2. Generate a per-account random 16-byte salt and a 32-byte PBKDF2-HMAC-SHA256 verifier, 100000 iterations, encoded as Base64. These parameters are exactly `DevelopmentAccounts.Verify`. Enter the local password privately; do not copy salts, hashes or real passwords into Git. Empty verifier fields intentionally cannot sign in.
3. Populate only trusted, owner-approved directory entries in WorkflowGovernance:IntakeCoordinators and IntakeReviewers. Coordinators require ActorId, OfficeCode, CanManageUnassigned, ExpiresAtUtc and SourceReference. Eligible reviewers require ActorId, OfficeCode and DisplayName. Entries are not accepted from the browser. Do not make every Officer a coordinator.
4. Use the normal applicant upload -> authorized unassigned intake queue -> officer assignment lifecycle. Durable case/document grants supply reviewer capabilities. Leave AuthorityAssignments empty unless an existing explicitly approved exceptional scoped assignment is required; the unbound example is documentation only, not installed authority. Keep institution reviewers limited to their request/institution.
5. Configure the API's existing database and token-validation infrastructure locally through its existing settings contract. No connection string or signing material is provided here.

To generate a verifier locally in PowerShell (values remain in local memory, not terminal output):

```powershell
$localPassword = Read-Host 'Choose a local test password' -AsSecureString
$plain = [System.Net.NetworkCredential]::new('', $localPassword).Password
$localSalt = New-Object byte[] 16
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($localSalt)
$rng.Dispose()
$derive = [System.Security.Cryptography.Rfc2898DeriveBytes]::new($plain, $localSalt, 100000, [System.Security.Cryptography.HashAlgorithmName]::SHA256)
$localSaltBase64 = [Convert]::ToBase64String($localSalt)
$localHashBase64 = [Convert]::ToBase64String($derive.GetBytes(32))
$derive.Dispose()
$plain = $null
```

Assign `$localSaltBase64` and `$localHashBase64` to that account's PasswordSalt/PasswordHash in the local JSON through a local editor or private script; do not print or stage them. Restart the API after edits, then sign in normally. Existing sessions are invalidated on restart.

To enable spatial fixture reads explicitly in the launching shell:

```powershell
$env:WorkflowGovernance__Screening__EnableDevelopmentSpatialMocks = 'true'
$env:WorkflowGovernance__Screening__DevelopmentSpatialFixturePath = (Resolve-Path -LiteralPath StateLandGovernance/src/Api/portal-development-spatial.json).Path
powershell -File StateLandGovernance/tools/development/Start-LocalTestApi.ps1 -EnableLocalTestSignIn
```

Do not leave conflicting Screening values in the local JSON: its provider is loaded last and may override earlier environment settings. Preserve current reviewed settings rather than copying this example over them.

The optional legacy research provisioner additionally expects operator-created `research`, `reviewtest`, and `reviewapplicant` local profiles for its fixed synthetic fixtures. They are deliberately absent from this minimal example; the provisioner does not create grants or a sign-in directory. Use the ordinary upload/assignment flow for new manual tests. No identity, assignment, fixture, review or result was installed by preparing these examples.
