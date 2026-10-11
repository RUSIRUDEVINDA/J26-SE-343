namespace StateLandGovernance.WorkflowGovernance.Application.Interfaces;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;

/// <summary>
/// Port for resolving governed document requirement sets by identifier and version.
/// Protects Application handlers from client injection of arbitrary legal requirements.
/// </summary>
public interface IDocumentRequirementProvider
{
    Task<IReadOnlyList<DocumentRequirementSnapshot>?> GetRequirementSetAsync(
        string requirementSetIdentifier,
        string requirementSetVersion,
        CancellationToken cancellationToken = default);
}
