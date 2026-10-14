using StateLandGovernance.WorkflowGovernance.Application.Interfaces;

namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;

/// <summary>
/// Unit of work for the in-memory WorkflowGovernance repositories.
/// Aggregate mutations are applied immediately in repository Add/Update;
/// CommitAsync acknowledges the unit boundary for application handlers.
/// </summary>
internal sealed class InMemoryWorkflowGovernanceUnitOfWork : IWorkflowGovernanceUnitOfWork
{
    public Task<int> CommitAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(0);
}
