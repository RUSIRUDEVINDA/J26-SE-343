namespace StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;

public interface IWorkflowExecutionStore
{
    Task<WorkflowExecution?> GetAsync(Guid caseId, CancellationToken ct);
    Task SaveAsync(WorkflowExecution execution, CancellationToken ct);
    Task<IReadOnlyList<Guid>> RejectedCasesAsync(CancellationToken ct);
}
