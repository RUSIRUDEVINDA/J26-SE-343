namespace StateLandGovernance.WorkflowGovernance.Domain.Documents;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class DocumentVersion
{
    public DocumentVersionId Id { get; }
    public int VersionNumber { get; }
    public DocumentVersionId? PredecessorVersionId { get; }
    public DocumentChecksum Checksum { get; }
    public DocumentContentReference ContentReference { get; }
    public string OriginalFileName { get; }
    public string MediaType { get; }
    public long FileSizeInBytes { get; }
    public Guid SubmittedByActorId { get; }
    public DateTime SubmittedAt { get; }

    internal DocumentVersion(
        DocumentVersionId id,
        int versionNumber,
        DocumentVersionId? predecessorVersionId,
        DocumentChecksum checksum,
        DocumentContentReference contentReference,
        string originalFileName,
        string mediaType,
        long fileSizeInBytes,
        Guid submittedByActorId,
        DateTime submittedAt)
    {
        if (id == default || id.Value == Guid.Empty) throw new InvalidDocumentException("DocumentVersionId cannot be empty.");
        if (versionNumber < 1) throw new InvalidDocumentException("VersionNumber must be positive.");
        if (versionNumber == 1 && predecessorVersionId != null) throw new InvalidDocumentException("Initial version cannot have a predecessor.");
        if (checksum == null) throw new InvalidDocumentException("Checksum is required.");
        if (contentReference == null) throw new InvalidDocumentException("ContentReference is required.");
        if (string.IsNullOrWhiteSpace(originalFileName)) throw new InvalidDocumentException("OriginalFileName cannot be blank.");
        if (string.IsNullOrWhiteSpace(mediaType)) throw new InvalidDocumentException("MediaType cannot be blank.");
        if (fileSizeInBytes <= 0) throw new InvalidDocumentException("FileSizeInBytes must be greater than zero.");
        if (submittedByActorId == Guid.Empty) throw new InvalidDocumentException("SubmittedByActorId cannot be empty.");
        if (submittedAt.Kind != DateTimeKind.Utc) throw new InvalidDocumentException("SubmittedAt must be UTC.");

        Id = id;
        VersionNumber = versionNumber;
        PredecessorVersionId = predecessorVersionId;
        Checksum = checksum;
        ContentReference = contentReference;
        OriginalFileName = originalFileName;
        MediaType = mediaType;
        FileSizeInBytes = fileSizeInBytes;
        SubmittedByActorId = submittedByActorId;
        SubmittedAt = submittedAt;
    }
}
