namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Storage;

using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;

/// <summary>
/// Local filesystem development implementation of document content storage.
/// Implements IDocumentContentWriter to persist uploaded source documents and compute authoritative SHA-256 receipts,
/// and IDocumentContentReader to stream persisted bytes during analysis.
/// </summary>
public sealed class LocalFileDocumentContentStore : IDocumentContentWriter, IDocumentContentReader
{
    private readonly string _rootPath;
    private readonly string _rootPathWithSeparator;
    private readonly ILogger<LocalFileDocumentContentStore> _logger;

    public LocalFileDocumentContentStore(
        IOptions<LocalFileStorageOptions> options,
        ILogger<LocalFileDocumentContentStore> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var opts = options.Value;
        opts.Validate();

        _rootPath = Path.GetFullPath(opts.RootPath);
        _rootPathWithSeparator = _rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                 + Path.DirectorySeparatorChar;

        Directory.CreateDirectory(_rootPath);
    }

    public async Task<DocumentContentReceipt> WriteAsync(
        Stream content,
        string originalFileName,
        string mediaType,
        Guid? documentId = null,
        Guid? versionId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            throw new ArgumentException("Original file name cannot be null or empty.", nameof(originalFileName));
        }

        var docId = documentId ?? Guid.NewGuid();
        var verId = versionId ?? Guid.NewGuid();

        // Safe relative storage key: documents/{docId}/{verId}/source.bin
        var relativeKey = $"documents/{docId:D}/{verId:D}/source.bin";
        var targetPath = ResolveSafePath(relativeKey);

        var targetDir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        var tempFilePath = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";

        long totalBytes = 0;
        string sha256Hex;

        try
        {
            using (var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var buffer = new byte[81920]; // 80 KB
                int bytesRead;

                await using (var fileStream = new FileStream(
                    tempFilePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true))
                {
                    while ((bytesRead = await content.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                        sha256.AppendData(buffer, 0, bytesRead);
                        totalBytes += bytesRead;
                    }

                    await fileStream.FlushAsync(cancellationToken);
                }

                sha256Hex = Convert.ToHexString(sha256.GetHashAndReset()).ToLowerInvariant();
            }

            // Atomic move to final target path
            File.Move(tempFilePath, targetPath, overwrite: true);

            _logger.LogInformation(
                "Persisted document {DocumentId} version {VersionId} ({Bytes} bytes, SHA-256: {Checksum}) to {Reference}",
                docId, verId, totalBytes, sha256Hex, relativeKey);

            return DocumentContentReceipt.CreateSha256(
                contentReference: relativeKey,
                sha256Hex: sha256Hex,
                originalFileName: Path.GetFileName(originalFileName),
                mediaType: mediaType,
                fileSizeInBytes: totalBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist document content to {Target}", relativeKey);
            if (File.Exists(tempFilePath))
            {
                try { File.Delete(tempFilePath); } catch { /* best effort cleanup */ }
            }
            throw;
        }
    }

    public Task<Stream?> ReadContentAsync(
        GovernedDocumentId documentId,
        DocumentVersionId versionId,
        string contentReference,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentReference))
        {
            return Task.FromResult<Stream?>(null);
        }

        try
        {
            var targetPath = ResolveSafePath(contentReference);
            if (!File.Exists(targetPath))
            {
                _logger.LogWarning("Document content not found at reference: {Reference}", contentReference);
                return Task.FromResult<Stream?>(null);
            }

            Stream stream = new FileStream(
                targetPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);

            return Task.FromResult<Stream?>(stream);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Path traversal or invalid reference rejected: {Reference}", contentReference);
            throw;
        }
    }

    private string ResolveSafePath(string relativePath)
    {
        var trimmed = relativePath.Trim();
        if (trimmed.StartsWith('/') || trimmed.StartsWith('\\') || Path.IsPathRooted(trimmed))
        {
            throw new ArgumentException($"Invalid relative path or path traversal detected: '{relativePath}'.", nameof(relativePath));
        }

        var normalized = trimmed.Replace('\\', '/');

        if (normalized.Contains("..", StringComparison.Ordinal) ||
            normalized.Contains(':', StringComparison.Ordinal))
        {
            throw new ArgumentException($"Invalid relative path or path traversal detected: '{relativePath}'.", nameof(relativePath));
        }

        var combined = Path.GetFullPath(Path.Combine(_rootPath, normalized));

        if (!combined.StartsWith(_rootPathWithSeparator, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(combined, _rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Resolved path escapes the storage root: '{relativePath}'.", nameof(relativePath));
        }

        return combined;
    }
}
