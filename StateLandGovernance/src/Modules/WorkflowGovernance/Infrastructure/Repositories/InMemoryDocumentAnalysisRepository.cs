namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Repositories;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;

/// <summary>
/// Process-local in-memory repository for DocumentAnalysis aggregate roots.
/// For Development and Testing only.
/// </summary>
internal sealed class InMemoryDocumentAnalysisRepository : IDocumentAnalysisRepository
{
    private readonly ConcurrentDictionary<Guid, DocumentAnalysis> _analyses = new();

    public Task<DocumentAnalysis?> GetByIdAsync(DocumentAnalysisId id, CancellationToken cancellationToken = default)
    {
        _analyses.TryGetValue(id.Value, out var analysis);
        return Task.FromResult(analysis);
    }

    public Task<DocumentAnalysis?> GetByDocumentVersionIdAsync(DocumentVersionId documentVersionId, CancellationToken cancellationToken = default)
    {
        var found = _analyses.Values.FirstOrDefault(a => a.DocumentVersionId == documentVersionId);
        return Task.FromResult(found);
    }

    public Task<VerifiedFactSnapshot?> GetSnapshotByIdAsync(VerifiedFactSnapshotId snapshotId, CancellationToken cancellationToken = default)
    {
        var found = _analyses.Values
            .SelectMany(a => a.VerifiedFactSnapshots)
            .FirstOrDefault(s => s.Id == snapshotId);
        return Task.FromResult(found);
    }

    public Task AddAsync(DocumentAnalysis documentAnalysis, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentAnalysis);
        _analyses[documentAnalysis.Id.Value] = documentAnalysis;
        return Task.CompletedTask;
    }
}
