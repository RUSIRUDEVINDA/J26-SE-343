namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Storage;

using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations;

/// <summary>
/// Local filesystem development implementation of analysis artifact storage.
/// Implements IAnalysisArtifactWriter to persist machine-generated OCR page and document transcripts,
/// and IAnalysisArtifactReader to read back persisted artifact bytes for presentation.
/// </summary>
public sealed class LocalFileAnalysisArtifactStore : IAnalysisArtifactWriter, IAnalysisArtifactReader
{
    private readonly string _rootPath;
    private readonly string _rootPathWithSeparator;
    private readonly ILogger<LocalFileAnalysisArtifactStore> _logger;

    public LocalFileAnalysisArtifactStore(
        IOptions<LocalFileStorageOptions> options,
        ILogger<LocalFileAnalysisArtifactStore> logger)
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

    public async Task<AnalysisArtifactWriteReceipt> WriteAsync(
        string proposedKey,
        string contentType,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(proposedKey))
        {
            throw new ArgumentException("Proposed artifact key cannot be null or whitespace.", nameof(proposedKey));
        }

        var normalizedKey = proposedKey.Trim().Replace('\\', '/').TrimStart('/');
        var targetPath = ResolveSafePath(normalizedKey);

        var targetDir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        var tempFilePath = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var sha256Hex = Convert.ToHexString(SHA256.HashData(content.Span)).ToLowerInvariant();

        try
        {
            await File.WriteAllBytesAsync(tempFilePath, content.ToArray(), cancellationToken);
            File.Move(tempFilePath, targetPath, overwrite: true);

            _logger.LogInformation(
                "Persisted analysis artifact {Reference} ({Bytes} bytes, SHA-256: {Checksum})",
                normalizedKey, content.Length, sha256Hex);

            return new AnalysisArtifactWriteReceipt(
                StorageReference: normalizedKey,
                ChecksumAlgorithm: "SHA-256",
                ChecksumValue: sha256Hex,
                ContentLength: content.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist analysis artifact to {Target}", normalizedKey);
            if (File.Exists(tempFilePath))
            {
                try { File.Delete(tempFilePath); } catch { /* best effort */ }
            }
            throw;
        }
    }

    public async Task<ReadOnlyMemory<byte>?> ReadAsync(
        string storageReference,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageReference))
        {
            return null;
        }

        try
        {
            var targetPath = ResolveSafePath(storageReference);

            if (!File.Exists(targetPath))
            {
                _logger.LogWarning("Analysis artifact not found at reference: {Reference}", storageReference);
                return null;
            }

            var bytes = await File.ReadAllBytesAsync(targetPath, cancellationToken);
            return new ReadOnlyMemory<byte>(bytes);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Path traversal or invalid artifact reference rejected: {Reference}", storageReference);
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
