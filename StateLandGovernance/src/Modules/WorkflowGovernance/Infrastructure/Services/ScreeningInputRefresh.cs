namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Services;

using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;

public sealed class ScreeningInputRefresh(IServiceScopeFactory scopes) : IScreeningInputRefresh
{
    public async Task<ScreeningInputRevision> ReadAsync(Guid caseId, Guid snapshotId, Guid? parcelId, CancellationToken ct)
    {
        // A second call on the original repositories returns tracked aggregates, even when another
        // transaction has committed changed document/analysis/completeness inputs during C4 execution.
        await using var scope = scopes.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CaseScreeningService>()
            .ReadInputRevisionAsync(caseId, snapshotId, parcelId, ct);
    }
}
