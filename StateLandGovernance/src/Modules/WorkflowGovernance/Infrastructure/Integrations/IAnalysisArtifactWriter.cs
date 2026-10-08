namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Infrastructure abstraction for persisting analysis artifact byte streams
/// (such as raw/clean OCR page transcripts and full document transcripts)
/// and obtaining durable, verifiable storage references and cryptographic receipts.
/// </summary>
public interface IAnalysisArtifactWriter
{
    /// <summary>
    /// Writes artifact content bytes and returns an immutable storage receipt.
    /// </summary>
    /// <param name="proposedKey">The proposed logical artifact key / relative path.</param>
    /// <param name="contentType">MIME content type of the artifact.</param>
    /// <param name="content">Exact artifact payload bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A receipt containing the final resolvable storage reference, checksum algorithm, checksum value, and byte length.</returns>
    Task<AnalysisArtifactWriteReceipt> WriteAsync(
        string proposedKey,
        string contentType,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Cryptographic and location receipt confirming durable storage of an analysis artifact.
/// </summary>
public sealed record AnalysisArtifactWriteReceipt(
    string StorageReference,
    string ChecksumAlgorithm,
    string ChecksumValue,
    long ContentLength);
