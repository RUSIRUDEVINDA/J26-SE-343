# C4 Step 9 controlled local activation — stopped at attachment-grant gate

2026-10-24, Asia/Colombo. **ACTIVATION STOPPED. No API, ML, or frontend service was started.** The approved migrations committed; the mandatory narrow attachment-permission script did not execute, so the runtime role has no rights on the new table and the compatible API was not validated or launched.

## Completed guarded actions

The read-only preflight passed before any write: `state_land_governance_c4`, OID `34144`, 41 tables, 21 migrations, original rows/schema/sequences/durable grants, private configuration/package, model hashes, and backup SHA256 `1b5f7ba0872f0cf59fe9f118c06cb3079f9c6a965120b568e31647e3f3032dd0` matched the approved baseline. The archive listing was readable.

The exact reviewed migration hashes matched:

- Step 1 `REVIEWED_FORWARD.sql`: `5af0fe8d358b9c22cad2c8a68007b8503f492230d155e46a0c7b34a191916c6f`
- Step 2 `REVIEWED_FORWARD.sql`: `bf2bf502852ecc08e4abdd63f108955cb7a397ffae27a5bdb2020f070f029489`

The guarded operator script then applied, in order:

1. `20261024000805_AddInvestigationEvidenceAndFinalization`
2. `20261024022733_AllowRegulatoryNoFollowupRequired`

Both post-migration guards passed before the next action. No prior migration was replayed and no EF startup migration was used.

## Stop condition

`psql` reported that it could not open the reviewed `REVIEWED_ADDITIONAL_PERMISSIONS.sql` during the grant invocation. The workspace subsequently lists that file, so this record does not claim it was deleted or altered; its operator-context access/open failure is unresolved. Per the approved instruction to stop at any mandatory gate failure, no substitute file, retry, broader grant, service startup, or recovery action was attempted.

## Read-only post-stop state

`STEP9_STOPPED_POST_MIGRATION_STATE.json` records:

- Database `state_land_governance_c4`, OID `34144`
- 42 tables; 23 migrations
- Both approved migration identities present
- `officer_investigation_attachments` exists and has 0 rows
- `slg_c4_regulatory_runtime` has `[]` rights on that table

No business record was created, modified, reset, restored, or deleted. The API, ML and frontend listeners remain offline. No Git operation occurred.

## Required continuation decision

Before continuing, inspect and approve the exact operator-context access method for the already reviewed narrow grant script. Re-run a fresh read-only preservation/grant preflight, apply only that unchanged script if its checksum and contents match, verify exactly `SELECT` and `INSERT`, then validate/start the approved services. Do not rerun either applied migration, execute Down, restore over ordinary storage, or use a different grant script without explicit review.
