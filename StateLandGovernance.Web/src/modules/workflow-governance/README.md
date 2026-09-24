# Component 4 — Workflow Governance

Placeholder module boundary for the frontend.

## Backend note (local API host)

The ASP.NET API registers **in-memory** `ILeaseCaseRepository` /
`IWorkflowGovernanceUnitOfWork` only in **Development** and **Testing**.
That store is process-local and non-durable — data is lost on restart.

Production hosting throws at startup if durable WorkflowGovernance persistence
has not been configured. Do not treat the in-memory store as production-ready.

Land Intelligence search / recommendations do not depend on WorkflowGovernance
lease persistence.
