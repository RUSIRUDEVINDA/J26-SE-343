# Isolated registered-complaint persistence proof

Runs the existing C4 service, EF repositories and real PostgreSQL transactions/constraints.
Never loads `.env`, starts the ordinary API, calls a real ML service or regulatory engine,
or creates C3/account/worker state. Classifier predictions and assignment ports are explicitly
test doubles; success here is not a live ML demonstration.

Set `C4_COMPLAINTS_ISOLATED_TEST_CONNECTION` explicitly. The program refuses hosts other than
127.0.0.1, ports below 55000 and database names outside `c4_complaints_test_` or
`c4_complaints_restore_`. Use a fresh dedicated PostgreSQL container/cluster and database.
No reset, database deletion or overwrite is implemented.

Build with `dotnet build RegisteredComplaintPersistenceProof.csproj -p:NuGetAudit=false`.
Invoke the resulting DLL with `dotnet` to avoid Windows apphost path-length limits.

Default mode requires an empty migration history. It applies the C4 baseline, seeds one
explicitly fictional legacy assessment, applies the complaints migration and verifies
compatibility with the following regulatory provenance migration. No other module is loaded.

`--restored` requires the already-upgraded schema and performs no migrations. Use only against
a new isolated backup restore. Restore equality must be checked before adding test fixtures.
The preparation tested this mode as the restricted runtime role, with only the proposed INSERT
grants and column-level UPDATE on Revision/PayloadJson. Tests add fictional records to that copy.

Checks cover concurrent unique insert, applicant-scoped keys, optimistic revision races,
rollback after an assessment INSERT, durable registration before inference, honest inference
failure, replay/conflict, same-record retry, prediction association/history, applicant ownership,
officer assignment/revocation and reasoned triage with stale-update rejection.

The clock deliberately retains seven .NET ticks beyond a whole second. PostgreSQL truncates
indexed timestamps to microseconds; aggregate saves must not rewrite immutable indexed fields.
This reproduced the former restricted-column permission failure and verifies the correction
without widening the Revision/PayloadJson UPDATE grant.

The completed preparation used the previously authorized standalone loopback test cluster on
57643 because Docker Desktop's daemon was unavailable. This is native PostgreSQL isolation,
not a container or ordinary database. The cluster is stopped after verification, with test
databases retained. See the complaints activation preparation artifacts for results and backup.
