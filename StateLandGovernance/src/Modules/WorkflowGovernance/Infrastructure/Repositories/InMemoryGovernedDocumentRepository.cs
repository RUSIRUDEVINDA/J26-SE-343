namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Repositories;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

/// <summary>
/// Process-local in-memory repository for GovernedDocument aggregate roots.
/// For Development and Testing only.
/// </summary>
internal sealed class InMemoryGovernedDocumentRepository : IGovernedDocumentRepository
{
    private readonly ConcurrentDictionary<Guid, GovernedDocument> _documents = new();

    public Task<GovernedDocument?> GetByIdAsync(GovernedDocumentId id, CancellationToken cancellationToken = default)
    {
        _documents.TryGetValue(id.Value, out var doc);
        return Task.FromResult(doc);
    }

    public Task<IReadOnlyList<GovernedDocument>> GetByLeaseCaseIdAsync(LeaseCaseId leaseCaseId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<GovernedDocument> list = _documents.Values
            .Where(d => d.LeaseCaseId == leaseCaseId)
            .ToList();
        return Task.FromResult(list);
    }

    public Task AddAsync(GovernedDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        _documents[document.Id.Value] = document;
        return Task.CompletedTask;
    }
}
