# WorkflowGovernance Infrastructure

## Lease-case persistence (local host only)

`AddWorkflowGovernanceInfrastructure(IHostEnvironment)` registers an **in-memory**
`ILeaseCaseRepository` and `IWorkflowGovernanceUnitOfWork` only when the host
environment is **Development** or **Testing**.

| Environment | Behaviour |
|-------------|-----------|
| Development / Testing | Process-local in-memory store (non-durable; lost on restart) |
| Production (and other) | Throws at registration — durable EF (or equivalent) must be provided |

This exists so the full `StateLandGovernance.Api` host can resolve WorkflowGovernance
handlers during local Land Intelligence frontend work. It is **not** an established
production design and must not be silently substituted in Production.

Unit tests call `AddWorkflowGovernanceInfrastructure(allowInMemoryLeasePersistence: true)`.
