# Isolated PostgreSQL planning proof

This executable contains labelled synthetic test facts, authority, routing and a C4 readiness test double. It is not live-case acceptance and is not registered in the normal API. It reuses the unit-test synthetic snapshot helper and real PostgreSQL adapters, planner and domain approval/revision operations.

The destination is fixed to localhost port 55434, database `repair_planning_20261016`, role `batch4b` in the existing isolated test cluster. It never discovers production/repository connections or resets data. Start/provision only that isolated cluster/database before use. Do not redirect it to the development database.

```powershell
# First initialization only: rejects an existing fixture rather than replacing it.
dotnet run --project StateLandGovernance/tools/WorkflowPlanningPersistenceProof -- --write
# Fresh process: verifies preserved active plan, approval/supersession and two-plan history.
dotnet run --project StateLandGovernance/tools/WorkflowPlanningPersistenceProof --no-build --no-restore -- --read
```

The output deliberately includes identifiers and metadata only. Full input bindings are persisted and checked. Case status remains InWorkflow; there is no execution/completion/handoff. This is a manual isolated PostgreSQL proof, not a container integration-suite pass.

## Stabilization acceptance host

`--stabilization-host` is a separate, loopback-only acceptance composition. It uses the **actual C4 deterministic engines and PostgreSQL adapters** with explicitly synthetic identities, documents, policy and spatial/governance coverage. It loads no repository `.env`, C1 host, production issuer or downstream transport. The ordinary API never registers its fixtures. See [the exact browser/API procedure](../../docs/verification/STABILIZATION_ACCEPTANCE.md).

The default database is the preserved `stabilization_acceptance_20261018` on localhost 55439. `--isolated-database=stabilization_acceptance_<unique_test_name>` may select an explicitly created **fresh isolated database**; host/port/role remain fixed and the prefix is validated. It never creates, deletes or resets databases. Restart uses the existing case and does not reseed it.

`stabilization-scenario.py` exercises real HTTP transitions; its optional second argument selects a separate evidence-run directory. Run write phases once, in the documented order, only against a fresh isolated fixture. `retrieve` and `permissions` are safe read/rejection probes. `stabilization-browser.py` reads the actual Next.js UI with explicitly named test sessions; it neither intercepts requests nor substitutes API responses.

Earlier `--execution-complete-read` fixtures retain their stored decisions, plans and final records. Their old screening/post-workflow bindings can now be stale under the stricter contract; retrieval reports that truthfully rather than reapproving or overwriting them. Their C4/downstream doubles remain historical fixture evidence, not actual-engine acceptance. Do not rerun legacy write modes over existing cases.
