namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;

using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;

/// <summary>No approved policy is implied by the Development fallback.</summary>
public sealed class UnconfiguredCompletenessProviders : IDocumentRequirementProvider, IProposalTemplateProvider
{
    public Task<IReadOnlyList<DocumentRequirementSnapshot>?> GetRequirementSetAsync(string identifier, string version,
        CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DocumentRequirementSnapshot>?>(null);
    public Task<ProposalTemplate?> GetTemplateAsync(ProposalTemplateId id, string version,
        CancellationToken cancellationToken = default) => Task.FromResult<ProposalTemplate?>(null);
}
