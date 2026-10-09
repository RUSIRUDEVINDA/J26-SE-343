namespace StateLandGovernance.WorkflowGovernance.Application.Interfaces;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;

/// <summary>
/// Application port for persisting raw incoming document bytes to storage before governance registration.
/// Guarantees that metadata (SHA-256 digest, byte length, content reference) is derived strictly from
/// the actual written bytes rather than untrusted client parameters.
/// </summary>
public interface IDocumentContentWriter
{
    Task<DocumentContentReceipt> WriteAsync(
        Stream content,
        string originalFileName,
        string mediaType,
        Guid? documentId = null,
        Guid? versionId = null,
        CancellationToken cancellationToken = default);
}
