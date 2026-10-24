namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
public sealed class UnavailableWorkflowPlanningStore : IWorkflowPlanningStore
{
    public Task<IReadOnlyList<RoutingPolicyDto>> GetPoliciesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<RoutingPolicyDto>>([]);
    public Task<IReadOnlyList<WorkflowPlanDto>> GetPlansAsync(Guid id, CancellationToken ct) => Task.FromResult<IReadOnlyList<WorkflowPlanDto>>([]);
    public Task<bool> HasExecutionAsync(Guid id, CancellationToken ct) => Task.FromResult(false);
    public Task SaveAsync(Guid id, WorkflowPlanDto plan, CancellationToken ct) => throw new InvalidOperationException("Durable planning storage is not configured.");
}
