namespace StateLandGovernance.WorkflowGovernance.Application.Interfaces;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

/// <summary>
/// Application repository abstraction for the GovernedDocument aggregate root.
/// </summary>
public interface IGovernedDocumentRepository
{
    Task<GovernedDocument?> GetByIdAsync(GovernedDocumentId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GovernedDocument>> GetByLeaseCaseIdAsync(LeaseCaseId leaseCaseId, CancellationToken cancellationToken = default);

    Task AddAsync(GovernedDocument document, CancellationToken cancellationToken = default);
}
