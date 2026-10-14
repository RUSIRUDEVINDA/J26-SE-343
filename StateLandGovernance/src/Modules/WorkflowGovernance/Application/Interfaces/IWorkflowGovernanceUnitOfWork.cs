namespace StateLandGovernance.WorkflowGovernance.Application.Interfaces;

using System.Threading;
using System.Threading.Tasks;

public interface IWorkflowGovernanceUnitOfWork
{
    Task<int> CommitAsync(CancellationToken cancellationToken = default);
}
