namespace StateLandGovernance.UnitTests.WorkflowGovernance.Infrastructure;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations;

/// <summary>
/// Test-only in-memory implementation of IAnalysisArtifactWriter that stores exact
/// byte arrays and allows retrieving them for round-trip provenance assertions.
/// </summary>
public sealed class InMemoryAnalysisArtifactWriter : IAnalysisArtifactWriter
{
    public sealed record StoredArtifact(
        string StorageReference,
        string ContentType,
        byte[] Bytes,
        string ChecksumAlgorithm,
        string ChecksumValue);

    private readonly ConcurrentDictionary<string, StoredArtifact> _storage = new(StringComparer.Ordinal);

    /// <summary>
    /// Optional hook to simulate persistence errors or enforce cancellation.
    /// </summary>
    public Func<string, ReadOnlyMemory<byte>, Task>? OnBeforeWrite { get; set; }

    public async Task<AnalysisArtifactWriteReceipt> WriteAsync(
        string proposedKey,
        string contentType,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (OnBeforeWrite is not null)
        {
            await OnBeforeWrite(proposedKey, content);
        }

        var bytes = content.ToArray();
        var checksum = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var storageReference = proposedKey;

        var stored = new StoredArtifact(
            StorageReference: storageReference,
            ContentType: contentType,
            Bytes: bytes,
            ChecksumAlgorithm: "SHA-256",
            ChecksumValue: checksum);

        _storage[storageReference] = stored;

        return new AnalysisArtifactWriteReceipt(
            StorageReference: storageReference,
            ChecksumAlgorithm: "SHA-256",
            ChecksumValue: checksum,
            ContentLength: bytes.LongLength);
    }

    public bool TryGetArtifact(string storageReference, out StoredArtifact? artifact)
    {
        return _storage.TryGetValue(storageReference, out artifact);
    }

    public StoredArtifact GetArtifact(string storageReference)
    {
        if (!_storage.TryGetValue(storageReference, out var artifact))
        {
            throw new KeyNotFoundException($"No artifact stored at reference '{storageReference}'.");
        }
        return artifact;
    }

    public IReadOnlyCollection<StoredArtifact> GetAll() => _storage.Values.ToList();

    public int Count => _storage.Count;
}
