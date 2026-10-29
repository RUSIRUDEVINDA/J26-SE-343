# Central complaint ordinary activation — completed

2026-10-23, Asia/Colombo. **ACTIVATED. C4 implementation frozen for PP1.** This records the user's explicit persistent-authorization activation approval plus the later startup-correction approval. Prior stopped attempts/checkpoints remain historical evidence.

## Correction, preflight and deployment

Removed exactly the two automatic LandIntelligence Database.Migrate startup calls in Api/Program.cs. No replacement EnsureCreated/raw SQL/indirect startup migration was introduced. No module/context/DI/connection-string change. The explicit account setup command remains available outside normal startup; it was not invoked. The only registered background writer found is the existing blockchain outbox processor, conditional on its flag, which remains false. Portal account normal startup loading is read-only. No additional unreviewed automatic schema writer was found.

258focused backend and7architecture tests pass. The new compiled-startup test inspects Program/generated startup methods for migration/schema-create/raw-SQL/SaveChanges references, protecting both environment branches. Actual startup also preserved all peer/account fingerprints and emitted no migration/DDL message. No normal startup migration was run.

Published a NEW88-file package `.tmp/central-startup-corrected-api`, preserving earlier packages. [STARTUP_CORRECTED_PACKAGE_SHA256.json](STARTUP_CORRECTED_PACKAGE_SHA256.json) and [STARTUP_CORRECTED_SOURCE_SHA256.json](STARTUP_CORRECTED_SOURCE_SHA256.json) are versioned manifests. New launcher [Start-CentralComplaintsStartupCorrectedApprovedApi.ps1](../../../../tools/development/Start-CentralComplaintsStartupCorrectedApprovedApi.ps1) differs from the approved persistent launcher only in corrected package/manifest paths. Permission/settings/guards are unchanged; no obsolete timed launcher was used. The source change is limited to Program.cs plus its new architecture test and this versioned launcher.

The immediate preflight matched every original table fingerprint, ordinary schema,17migration history, old grants/column updates, private settings, model hashes, backup checksum and archive listing. All36tables in both user schemas matched the315322-byte verified backup, SHA256`ab6780784f2cf36b405b33c81d9e1a81da5e215f7cbcdc0418b61f988da433d4`. No baseline drift, so no expensive backup/restore repeat. Peer/account fingerprints were captured with forced read-only SELECTs. No existing API listener was present; no old process needed stopping.

Only unchanged COMBINED_REVIEWED_FORWARD.sql and COMBINED_REVIEWED_ADDITIONAL_PERMISSIONS.sql were executed against **localhost5432/c4_handover_local**. Applied exactly:

1. `20261022200907_AddCentralComplaintAssignments`
2. `20261022205550_AddComplaintHandlingEvents`

History now19. Added only SELECT and INSERT on complaint_assignment_events and complaint_handling_events to slg_c4_regulatory_runtime. Prior table grants and column UPDATE grants remain unchanged. Both new tables are empty, no backfill. Every original non-history table fingerprint remains unchanged; original migration-history rows are retained with exactly the two additions. No other database was migrated.

## Actual runtime verification

One API process PID27588 serves5264(IPv4/IPv6 listener rows are the same process). Health is Healthy/Live. Exact critical process environment matches the approved candidate: restricted C4 identity, read-only peer/account connections, disabled sandboxes/worker and persistent private permission path. The original private environment file was not overwritten. Reviewed files named NOT_ENABLED now supply the active approved process; their historical names were retained to avoid silently changing paths/settings.

The exact Commissioner actor/grant has persistent Read/Assign/Review plus nine explicit operations, Revoked=false and no expiry. Existing login works with unchanged credentials. Live revocation was exercised on the private server document using reads only: same JWT immediately received403; exact original bytes were restored in finally; same session then received200. A secure pre-verification document copy and timestamped evidence are retained. No account/profile/password was changed. Existing30-minute login/session rules remain.

**86 runtime checks pass,19saved reports retrieved** using actual stored-creator sessions. Central complaint authority does not broaden independent-report ownership. Verified central four queues, trusted DS selector, original complaint/classifications, Unassigned state and TakeDirect/AssignDs entry controls; applicant owner/public-history contract; anonymous/applicant/DS central denial; unassigned DS404 and empty inbox with no old C3 case-review fallback. All three existing complaints remain unassigned, preserving the approved no-backfill policy.

Direct and DS lifecycle execution, closure/reopening, reassignment races and immutable hash histories have genuine isolated proof. They were NOT replayed on ordinary complaints merely for testing. No ordinary assignment, investigation, note, decision, complaint submission/retry or classification/anomaly inference occurred. New assignment/handling event counts are zero. Existing saved reports are GET retrievals, never reruns.

Final preservation: **35original C4 data tables and45peer/account tables unchanged**, including original complaints, predictions/probabilities, research results, investigations, audits/outbox/receipts, account profiles/password hashes and C1/C3 state. Read-only connection protection remains. All old SQL/source/package manifests are preserved; the completion report's preactivation bytes are saved separately in FINAL_C4_COMPLETION_REPORT_BEFORE_ORDINARY_ACTIVATION.md.

The related portal and C4 ML listeners were absent. Started only their established launchers, with no duplicate service. ML frozen bundle check PASS, anomaly hash`59ed4a73d56d9f06798c4ebc4684a633afe72841c41ed0b3ba4e7bb658782b46`, threshold0.5743688923115832; ML8104ready/modelv2/PID36612. Frontend3000ready/PID16692; five relevant routes returned200. Initial probes preceded warm-up, final probes succeeded. No workers/Groq/ledger activated.

## Evidence and practical limits

- [Immediate preflight](ORDINARY_ACTIVATION_IMMEDIATE_PREFLIGHT.json)
- [Exact SQL/grant actions and hashes](ORDINARY_ACTIVATION_SQL_EVIDENCE.json)
- [Authenticated runtime checks/report IDs/body hashes](ORDINARY_ACTIVATION_RUNTIME_EVIDENCE.json)
- [Actual process configuration and final preservation](ORDINARY_ACTIVATION_FINAL_PRESERVATION.json)
- [Portal HTTP/frozen ML readiness](ORDINARY_ACTIVATION_SERVICE_READINESS.json)
- [Manual startup/browser guide](ACTIVATED_CENTRAL_COMPLAINT_MANUAL_GUIDE.md)

No browser tool is available: HTTP200 is not visual/keyboard/responsive or authenticated click-through proof. Those checks remain manual.139frontend tests passed in the immediately preceding unchanged-frontend task, not rerun for the host-only correction. Three existing C1 TypeScript errors and NuGet-feed warnings remain; no full production frontend build claim. In-memory login sessions remain an existing deployment limitation. No new C4 feature work is proposed.

## Recovery

Use the reviewed fail-closed procedure only: stop the verified corrected API; revoke/disable central server permission; revoke only new-table SELECT/INSERT if rolling back the added deployment; preserve all tables/migrations/events. Keep API stopped until a compatible recovery package is reviewed. No incompatible old case/assignment-only binary restart, destructive Down, event deletion or restore over ordinary storage. Original backups are for NEW isolated recovery databases only. No recovery was necessary during this successful activation.

**C4 PP1 is activated and implementation-frozen. Real complaint handling requires normal authorized business action; current approval did not authorize test decisions. Standalone C3 work may now proceed.**
