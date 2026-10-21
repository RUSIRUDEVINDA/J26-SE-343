namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations;

using StateLandGovernance.WorkflowGovernance.Application.Interfaces;

public sealed class UnavailableHumanEvidencePageReader : IHumanEvidencePageReader
{
    public Task<string?> ReadAsync(Guid documentVersionId, Guid runId, int pageNumber, CancellationToken ct) =>
        Task.FromResult<string?>(null);
}
