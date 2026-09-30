namespace StateLandGovernance.WorkflowGovernance.Application.Interfaces;

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;

/// <summary>
/// Internal Application port for reading document content bytes from storage for an exact document version.
/// Protects Application and Domain from direct dependence on MinIO/S3 or object-store credentials.
/// 
/// Invariants:
/// 1. Exact-Version Provenance: Requires explicit GovernedDocumentId and DocumentVersionId.
///    Callers cannot accidentally read the latest or active version.
/// 2. Eventual Infrastructure Pipeline:
///    - Read stored bytes for the specified exact DocumentVersionId.
///    - Compute SHA-256 hash across the retrieved stream/bytes.
///    - Compare against the persisted DocumentChecksum.
///    - Only upon an exact match are bytes submitted for analysis.
/// 3. Evidence Classification: SHA-256 serves solely as integrity and change-detection evidence,
///    establishing version/checksum traceability. It does not independently provide authorship,
///    legal authenticity, non-repudiation, or immutable storage.
/// </summary>
public interface IDocumentContentReader
{
    Task<Stream?> ReadContentAsync(
        GovernedDocumentId documentId,
        DocumentVersionId versionId,
        string contentReference,
        CancellationToken cancellationToken = default);
}
