namespace StateLandGovernance.UnitTests.WorkflowGovernance.Infrastructure;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;

/// <summary>
/// Test-only in-memory IDocumentContentReader keyed strictly by
/// (GovernedDocumentId, DocumentVersionId, ContentReference).
/// Returns fresh MemoryStream per read call to prevent already-consumed stream bugs.
/// </summary>
public sealed class TestDocumentContentReader : IDocumentContentReader
{
    private readonly ConcurrentDictionary<(Guid DocumentId, Guid VersionId, string ContentReference), byte[]> _storage = new();

    public void Store(Guid documentId, Guid versionId, string contentReference, byte[] contentBytes)
    {
        _storage[(documentId, versionId, contentReference)] = contentBytes;
    }

    public Task<Stream?> ReadContentAsync(
        GovernedDocumentId documentId,
        DocumentVersionId versionId,
        string contentReference,
        CancellationToken cancellationToken = default)
    {
        if (_storage.TryGetValue((documentId.Value, versionId.Value, contentReference), out var bytes))
        {
            return Task.FromResult<Stream?>(new MemoryStream(bytes));
        }

        return Task.FromResult<Stream?>(null);
    }
}
