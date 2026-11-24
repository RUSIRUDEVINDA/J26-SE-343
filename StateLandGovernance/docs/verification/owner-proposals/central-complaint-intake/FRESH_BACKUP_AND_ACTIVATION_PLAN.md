# Fresh backup and isolated restore plan - NOT EXECUTED

This plan is for separate ordinary-activation approval. No fresh ordinary archive or ordinary restore was created in the implementation task. Earlier backups remain untouched. The ordinary three-complaint data is broader than the old one-complaint archive.

## Required baseline

Before any deployment, record forced-read-only identity (`current_database`, host/port, role and transaction_read_only), all17current migration IDs, normalized full C4 schema, every C4 table's row count and stable sorted row digest, current table/column runtime ACLs and role flags. Record each current complaint/classification/review association and saved regulatory/workflow/audit/outbox history without publishing text/profiles/credentials. Capture current private settings and verified API binary/process identity privately. Recheck approved source/SQL/settings manifests, model identity and disabled workers/sandboxes. The expected only schema delta is migration20261022200907; stop on any unknown change.

All fingerprint queries must use `BEGIN READ ONLY` or forced `default_transaction_read_only=on`. Example per-table fingerprint (repeat for every catalogue-validated C4 table, with properly quoted identifiers):

```sql
BEGIN READ ONLY;
SELECT current_database(), current_user, inet_server_port(), current_setting('transaction_read_only');
SELECT "MigrationId" FROM governance_intelligence.__ef_migrations_history ORDER BY "MigrationId";
SELECT count(*), md5(coalesce(string_agg(row_to_json(t)::text, '' ORDER BY row_to_json(t)::text), ''))
FROM governance_intelligence.registered_complaints t;
COMMIT;
```

Quiesce ordinary C4 writers for the comparison window after recording a new backup baseline, or use a shared exported read-only snapshot for catalogue/fingerprints/pg_dump. If writers remain active, recheck all fingerprints after backup; if they differ, preserve the new rows and create a versioned new backup/baseline. Do not delete concurrent records or compare incompatible snapshot times as corruption.

## Windows archive commands (future approved use only)

Use the separately authorized operator account when prompted. Do not put its password in commands or this document. Runtime credentials remain unchanged and do not receive DDL.

```powershell
Set-Location 'D:\AI-Driven-Digital-Lease-Governance-for-State-Land-Management\AI-Driven-Digital-Lease-Governance-for-State-Land-Management'
$pgBin = 'C:\Program Files\PostgreSQL\18\bin'
$backupStamp = (Get-Date).ToUniversalTime().ToString('yyyyMMdd_HHmmss')
$backupFolder = Join-Path (Get-Location) '.tmp\central-complaint-backups'
New-Item -ItemType Directory -Force -Path $backupFolder | Out-Null
$centralArchive = Join-Path $backupFolder "c4_handover_local_$backupStamp.dump"
if (Test-Path -LiteralPath $centralArchive) { throw 'Refusing to overwrite an archive' }
$priorPgOptions = $env:PGOPTIONS
try {
  $env:PGOPTIONS = '-c default_transaction_read_only=on'
  & "$pgBin\pg_dump.exe" -W -h localhost -p 5432 -U postgres -d c4_handover_local `
    --format=custom --file=$centralArchive
  if ($LASTEXITCODE -ne 0) { throw 'Backup failed; do not activate' }
} finally {
  if ($null -eq $priorPgOptions) { Remove-Item Env:PGOPTIONS -ErrorAction SilentlyContinue }
  else { $env:PGOPTIONS = $priorPgOptions }
}
Get-Item -LiteralPath $centralArchive | Select-Object FullName,Length,CreationTimeUtc,LastWriteTimeUtc
Get-FileHash -Algorithm SHA256 -LiteralPath $centralArchive
& "$pgBin\pg_restore.exe" --list $centralArchive
if ($LASTEXITCODE -ne 0) { throw 'Archive listing failed; do not activate' }
```

Store the exact absolute path, bytes, UTC timestamps and SHA256 in a NEW versioned evidence file. Reopen/hash it using the actual activation account. Do not widen system/file permissions. `pg_restore --list` checks archive readability, not a complete restore: the next gate is mandatory.

## NEW isolated Docker restore

Use a new container name, new database, loopback-only unused port and private storage. Never restore over an existing ordinary or verification database. Confirm Docker daemon/image identity, no existing listener on the chosen port and sufficient disk. PostgreSQL18 is compatible with the archive's producer. Restore without ordinary ownership/ACL application; compare original ACL metadata separately and reconstruct only the isolated restricted-role test rights inside the new container.

Example after approval, with `$centralArchive` from above and port57659 verified free:

```powershell
$restoreContainer = "slg-central-restore-$backupStamp"
$restorePassword = [guid]::NewGuid().ToString('N')
docker run --name $restoreContainer -e POSTGRES_USER=central_restore `
  -e "POSTGRES_PASSWORD=$restorePassword" -e POSTGRES_DB=c4_central_restore `
  -p 127.0.0.1:57659:5432 -d postgres:18
if ($LASTEXITCODE -ne 0) { throw 'Isolated PostgreSQL creation failed' }
# Wait for pg_isready success before copying/restoring; do not start another instance.
docker exec $restoreContainer pg_isready -U central_restore -d c4_central_restore
if ($LASTEXITCODE -ne 0) { throw 'Isolated PostgreSQL not ready; no activation' }
docker cp $centralArchive "${restoreContainer}:/tmp/current-c4.dump"
if ($LASTEXITCODE -ne 0) { throw 'Archive copy failed' }
docker exec $restoreContainer pg_restore -U central_restore -d c4_central_restore `
  --no-owner --no-privileges --exit-on-error /tmp/current-c4.dump
if ($LASTEXITCODE -ne 0) { throw 'Isolated restore failed; no activation' }
```

Compare restored migration history, owner/ACL-normalized schema and ALL original-row fingerprints to the matching fresh ordinary snapshot. Explicitly verify every current complaint (including unlinked), associated original classification/model probabilities/review history, independent regulatory provenance, existing anomaly reports, audits/outbox/receipts. Do not infer preservation from counts alone. Confirm the backup hash did not change. Record mismatches and stop; no repair workaround is authorized.

Only after those comparisons pass: apply the single reviewed forward script in the NEW restore, confirm assignment table empty/no backfill and all original rows unchanged, apply only reviewed grant delta to a restricted test role, and rerun migration/authorization/persistence/concurrency/grant proofs. The existing CentralComplaintProof executable intentionally refuses restored ordinary targets and never loads .env; a reviewed restore-verification harness must retain that strict target guard. Do not weaken it to point at ordinary storage.

## Ordinary activation sequence after separate approval

Build/publish the exact reviewed package first to a NEW ignored directory. Verify all source/SQL/config hashes and run final gates. Stop the single identified old API, apply ONLY REVIEWED_FORWARD.sql and REVIEWED_ADDITIONAL_PERMISSIONS.sql with operator credentials on localhost:5432/c4_handover_local, merge only PROPOSED_ACTIVATION_SETTINGS.json into a preserved private environment, and start exactly one reviewed updated package. Preserve existing credentials/features, read-only peer/account connections and disabled worker/sandbox/Groq/blockchain settings. Never replay prior migrations or invoke account setup.

Verify old report and applicant retrieval before deliberate complaint assignments. Current rows initially remain Unassigned. Obtain explicit approval for the exact ordinary assignment/reassignment demonstration; it changes DS visibility. No new ordinary complaint or classifier call is needed for routing verification. Record actual event IDs, database hashes, authorization denials and model-call counters.

Rollback: stop updated API; disable central settings and remove timed grants; revoke ONLY new-table SELECT/INSERT BEFORE starting an old binary. Preserve table, migration and immutable event history; never destructive Down or overwrite ordinary storage. The old binary returns to the prior case-based complaint authorization contract, so make that recovery choice explicit. Partial failures require inspecting actual SQL/ACL/config state before proceeding. Recovery restores only to another NEW isolated database.
