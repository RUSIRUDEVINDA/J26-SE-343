namespace StateLandGovernance.WorkflowGovernance.Application.Interfaces;

using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;

/// <summary>
/// Port for resolving governed proposal templates by ID and version.
/// Protects Application handlers from hardcoding template definitions or requirements.
/// </summary>
public interface IProposalTemplateProvider
{
    Task<ProposalTemplate?> GetTemplateAsync(ProposalTemplateId id, string version, CancellationToken cancellationToken = default);
}
