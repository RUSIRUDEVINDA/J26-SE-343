# C4 Step 8B — final local test blockers

2026-10-24, Asia/Colombo. **LOCAL READY FOR ACTIVATION APPROVAL.** This verification made no ordinary database, grant, migration, configuration, package, service, model, Git-index, commit or push change.

## Complaint HTTP contract

`RegisteredComplaintHttpTests.cs` had stale expectations for an authenticated applicant, an unassigned Officer, and the test's supposed reviewer. The last identity was never assigned the complaint. The established policy maps each out-of-scope lookup and review write to `KeyNotFoundException`, which the controller returns as a uniform `404 Not Found`; this prevents complaint enumeration. Anonymous callers remain `401`; a central capability denial occurs before an identifier lookup and remains `403` where applicable.

Only the untracked test source `StateLandGovernance/tests/UnitTests/Api/RegisteredComplaintHttpTests.cs` changed. Its three out-of-scope GET assertions and its three out-of-scope review assertions now expect `NotFound`. No production authorization code changed.

- API unit tests: **40 passed, 0 failed, 0 skipped**.
- `git diff --check` has no applicable tracked diff because this test source is an existing untracked working-tree file; no staging was performed.

## Isolated Governance verification

The test target was explicitly `127.0.0.1:57657`, never ordinary `localhost:5432`. The final disposable database was `component4_screening_test_step8b_20261024_broad`; the server reported internal Docker port 5432 and OID 65315. PostgreSQL OIDs are cluster-local, so the isolation guarantee is the fixed external loopback endpoint, test-only name prefix, and separate Docker cluster—not a comparison of OIDs across clusters.

Migration-owning integration tests each require an empty database and independently call `Database.MigrateAsync()`. A single shared-database run correctly exposed collisions (`pg_namespace` duplicate, duplicate column, duplicate relation): **974 passed, 8 failed**. Those are harness-isolation collisions, not application failures. The suite was then qualified by partitioning without weakening a test:

- Remaining project partition: **972 passed, 0 failed**.
- Complaint-classification migration/persistence cases: **6 passed, 0 failed**, each with a fresh disposable database.
- Early Governance safety guards: **2 passed, 0 failed**.
- Early Governance migration/persistence/referral cases: **2 passed, 0 failed**, each with a fresh disposable database.

That is **982 passed, 0 failed, 0 skipped** across the complete project test inventory, with every write directed to isolated databases prefixed `component4_screening_test_step8b_20261024_`.

The Python smoke launcher used `.tmp/c4-ml-venv/Scripts/python.exe` (Python 3.11.9) and imported its required FastAPI/ML packages. Existing real anomaly smoke tests ran in the 972-test partition against the frozen bundle. The frozen hashes were rechecked after testing:

- Complaint classifier v2: `63b37eb7175e74a8dcde82bf042fcf9dcbb4f0432fbe3b4677aac037007a20ae`
- Workflow anomaly v1: `59ed4a73d56d9f06798c4ebc4684a633afe72841c41ed0b3ba4e7bb658782b46`

Architecture tests: **7 passed, 0 failed, 0 skipped**. NuGet vulnerability-feed connectivity warnings remain external warnings only.

## Ordinary preservation

The final existing read-only preflight passed and recorded:

- Database `state_land_governance_c4`, OID **34144**.
- **41** tables and **21** migrations.
- Original rows, schema, migration history, sequences and durable grants unchanged.
- Existing private configuration, permissions and published package unchanged.
- Backup SHA256 rechecked: `1b5f7ba0872f0cf59fe9f118c06cb3079f9c6a965120b568e31647e3f3032dd0`.
- No application listeners observed at the preflight check; no ordinary services were started or stopped.
- Ordinary writes and service actions by this task: **0**.

Evidence: `STEP8B_ISOLATED_DATABASE.json` and `step3-sequential-navigation-20261024/READ_ONLY_PREFLIGHT.json`.

## Remaining qualification limits

The source-snapshot provenance limitation from Steps 7B-2/7B-3 remains separate from local activation readiness. It was not rebuilt or changed here. Manual browser verification and the controlled ordinary migration/deployment remain pending separate approval.

No functional test failures remain for this Step 8B scope.
