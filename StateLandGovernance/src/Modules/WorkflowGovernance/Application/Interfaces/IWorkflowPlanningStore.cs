namespace StateLandGovernance.WorkflowGovernance.Application.Interfaces;

using StateLandGovernance.WorkflowGovernance.Application.DTOs;
public interface IWorkflowPlanningStore
{
    Task<IReadOnlyList<RoutingPolicyDto>> GetPoliciesAsync(CancellationToken ct);
    Task<IReadOnlyList<WorkflowPlanDto>> GetPlansAsync(Guid caseId, CancellationToken ct);
    Task SaveAsync(Guid caseId, WorkflowPlanDto plan, CancellationToken ct);
    Task<bool> HasExecutionAsync(Guid caseId, CancellationToken ct);
}
