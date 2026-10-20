namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;
public sealed class UnavailableWorkflowExecutionStore : IWorkflowExecutionStore
{
    public Task<WorkflowExecution?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult<WorkflowExecution?>(null);
    public Task SaveAsync(WorkflowExecution execution, CancellationToken ct) => throw new InvalidOperationException("Durable PostgreSQL workflow execution storage is not configured.");
    public Task<IReadOnlyList<Guid>> RejectedCasesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>());
}
