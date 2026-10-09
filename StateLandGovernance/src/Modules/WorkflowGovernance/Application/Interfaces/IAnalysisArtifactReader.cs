namespace StateLandGovernance.WorkflowGovernance.Application.Interfaces;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Application port for reading persisted machine analysis artifacts (such as OCR document or page transcripts)
/// using logical storage references generated during analysis.
/// </summary>
public interface IAnalysisArtifactReader
{
    Task<ReadOnlyMemory<byte>?> ReadAsync(
        string storageReference,
        CancellationToken cancellationToken = default);
}
