# Approved Step 13 activation — stopped at API bind

2026-10-24, Asia/Colombo. The approved activation reached successful ordinary migrations and preservation verification, then stopped at the API startup failure. No forced retry, rollback, grant or ordinary business-record test was performed.

## Completed

- Published fresh native .NET API into `.tmp/c4-step13-approved-local/api`.
- Built a fresh Next.js frontend with webpack into `.tmp/c4-step13-approved-local/web`, targeting `http://localhost:5264`. Build ID **AEGg-BevAWL_DD4P-ysYx**. This build is prepared, **not currently served**.
- Verified current C4 source against the packaged source and captured package SHA256 manifest. Browser static assets contain no fixture port 58118.
- Existing protected configuration and original 23-migration preflight passed.
- Created fresh custom-format backup `.tmp/c4-step13-approved-local/before.dump`, SHA256 **0d627ddab2f93f6106452d3a9ca482c66d03cd12c1b8bd4c98ab6ae9122490c5**.
- Restored that backup into isolated Docker database `127.0.0.1:57657/c4_activation_step13_20261024`. All table row counts/digests, columns, sequences and migration history matched. Ordinary baseline remained unchanged before the activation phase.
- Revalidated old frontend PID 32340 and old API PID 26788, then stopped only those processes. ML and PostgreSQL were retained.
- Applied hash-verified original Step 12 SQL bytes, then Step 13 SQL bytes, through psql stdin with ON_ERROR_STOP and finite lock/statement timeouts.
- Verified exact history after each migration, unchanged non-history business rows, sequences, columns and runtime table grants.

## Blocker and final state

New API PID **28012** exited with:

```text
Failed to bind to address http://127.0.0.1:5264: address already in use.
SocketException (10048)
```

The planned child environment set `ASPNETCORE_URLS` to both `http://127.0.0.1:5264` and `http://localhost:5264`. These overlap on IPv4. The stack includes `LocalhostListenOptions.BindAsync`; final inspection shows no other listener on 5264. This is consistent with a self-collision from the duplicated address configuration. No product-source workaround is needed.

The readiness plan's dual-address setting was incorrect. The minimal proposed correction for a separately approved continuation is **one** binding, `ASPNETCORE_URLS=http://localhost:5264`, retaining the same protected candidate configuration and already-built package. Recheck the 25-migration postflight and free ports, start that API once, verify health, then start the prepared frontend and perform the read-only acceptance checks. **Do not replay either migration.**

| Surface | Actual final state |
| --- | --- |
| Ordinary database | state_land_governance_c4, OID 34144, 42 tables, **25 migrations** |
| Latest migrations | 20261024112715_AllowIndividualFindingClosure; 20261024120539_AllowDirectOfficerDisposition |
| Closure index | ux_regulatory_finding_closure present with expected unique partial definition |
| Ordinary business preservation | Non-history row digests, sequences, columns and runtime table grants unchanged after both scripts |
| API 5264 | **Stopped**; new PID 28012 exited; final GET refused connection |
| Frontend 3000 | **Stopped**; new frontend never launched; final GET refused connection |
| ML 8104 | Original PID **27388**, HTTP 200, ready, model_version v2; not restarted |
| PostgreSQL 5432 | Original PID **7024**, running; not restarted |
| Read-only browser acceptance | **Not run** because API/frontend activation did not become healthy |

Evidence under `.tmp/c4-step13-approved-local`: `before.json`, `before.dump`, `backup-proof.json`, `package-sha256.json`, both `after-<migration>.json` snapshots, `constraints.json`, `pids.json`, `api.stdout.log` and `api.stderr.log`. Final read-only migration/health evidence is `.tmp/c4-step13-readiness/database.json` and `health.json`; those files were refreshed after the stop and supersede the earlier 23-migration observation. Build/publish logs are `.tmp/c4-step13-approved-build.log` and `c4-step13-approved-publish.log`.

No ordinary application row, proposal/demo record, ML model, regulatory rule, Conflict Detection implementation, peer component or blockchain changed. No grants, destructive Down, restore over ordinary data, staging, commit or push. Per the owner's explicit stop condition, activation remains paused at this blocker pending direction.
