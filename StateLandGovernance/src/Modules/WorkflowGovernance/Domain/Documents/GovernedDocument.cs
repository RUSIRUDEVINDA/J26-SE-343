namespace StateLandGovernance.WorkflowGovernance.Domain.Documents;

using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.Documents.Events;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed class GovernedDocument
{
    public GovernedDocumentId Id { get; }
    public LeaseCaseId LeaseCaseId { get; }
    public string LogicalCategory { get; }
    public DocumentVersionId ActiveVersionId { get; private set; }
    public int Revision { get; private set; }

    private readonly List<DocumentVersion> _versions = new();
    public IReadOnlyCollection<DocumentVersion> Versions => _versions.AsReadOnly();

    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public GovernedDocument(
        GovernedDocumentId id,
        LeaseCaseId leaseCaseId,
        string logicalCategory,
        DocumentVersionId initialVersionId,
        DocumentChecksum documentChecksum,
        DocumentContentReference documentContentReference,
        string originalFileName,
        string mediaType,
        long fileSizeInBytes,
        Guid actorId,
        DateTime actionTime,
        VerifiedAuthoritySnapshot authoritySnapshot)
    {
        if (id == default || id.Value == Guid.Empty) throw new InvalidDocumentException("GovernedDocumentId cannot be empty.");
        if (leaseCaseId == default || leaseCaseId.Value == Guid.Empty) throw new InvalidDocumentException("LeaseCaseId cannot be empty.");
        if (string.IsNullOrWhiteSpace(logicalCategory)) throw new InvalidDocumentException("LogicalCategory cannot be blank.");
        if (actorId == Guid.Empty) throw new InvalidDocumentException("ActorId cannot be empty.");
        if (authoritySnapshot == null) throw new MissingVerifiedAuthorityException("VerifiedAuthoritySnapshot is required.");
        
        var requiredScope = new AuthorityScope(AuthorityScopeKind.LeaseCase, leaseCaseId.Value.ToString());
        authoritySnapshot.EnsureAuthorizes(actorId, "DocumentSubmitter", requiredScope, actionTime);

        Id = id;
        LeaseCaseId = leaseCaseId;
        LogicalCategory = logicalCategory;
        ActiveVersionId = initialVersionId;
        Revision = 1;

        var initialVersion = new DocumentVersion(
            id: initialVersionId,
            versionNumber: 1,
            predecessorVersionId: null,
            checksum: documentChecksum,
            contentReference: documentContentReference,
            originalFileName: originalFileName,
            mediaType: mediaType,
            fileSizeInBytes: fileSizeInBytes,
            submittedByActorId: actorId,
            submittedAt: actionTime
        );

        _versions.Add(initialVersion);

        _domainEvents.Add(new DocumentRegistered(
            EventId: Guid.NewGuid(),
            OccurredOn: actionTime,
            DocumentId: Id,
            LeaseCaseId: LeaseCaseId,
            DocumentVersionId: initialVersionId,
            VersionNumber: 1,
            DocumentRevision: Revision
        ));

        _domainEvents.Add(new DocumentVersionAdded(
            EventId: Guid.NewGuid(),
            OccurredOn: actionTime,
            DocumentId: Id,
            LeaseCaseId: LeaseCaseId,
            DocumentVersionId: initialVersionId,
            VersionNumber: 1,
            PredecessorVersionId: null,
            DocumentRevision: Revision,
            ChecksumAlgorithm: documentChecksum.Algorithm,
            ChecksumValue: documentChecksum.Value
        ));
    }

    public void AddVersion(
        DocumentVersionId newVersionId,
        DocumentVersionId expectedPredecessorVersionId,
        DocumentChecksum documentChecksum,
        DocumentContentReference documentContentReference,
        string originalFileName,
        string mediaType,
        long fileSizeInBytes,
        Guid actorId,
        DateTime actionTime,
        VerifiedAuthoritySnapshot authoritySnapshot)
    {
        if (newVersionId == default || newVersionId.Value == Guid.Empty) throw new InvalidDocumentException("New DocumentVersionId cannot be empty.");
        if (expectedPredecessorVersionId == default || expectedPredecessorVersionId.Value == Guid.Empty) throw new DocumentVersionConflictException("Expected predecessor is required.");
        if (actorId == Guid.Empty) throw new InvalidDocumentException("ActorId cannot be empty.");
        if (actionTime.Kind != DateTimeKind.Utc) throw new InvalidDocumentException("ActionTime must be UTC.");
        if (authoritySnapshot == null) throw new MissingVerifiedAuthorityException("VerifiedAuthoritySnapshot is required.");

        var requiredScope = new AuthorityScope(AuthorityScopeKind.LeaseCase, LeaseCaseId.Value.ToString());
        authoritySnapshot.EnsureAuthorizes(actorId, "DocumentSubmitter", requiredScope, actionTime);

        if (expectedPredecessorVersionId != ActiveVersionId)
        {
            if (_versions.Any(v => v.Id == expectedPredecessorVersionId))
            {
                throw new DocumentVersionConflictException("Predecessor is stale.");
            }
            throw new DocumentVersionConflictException("Predecessor is missing from collection.");
        }

        if (_versions.Any(v => v.Id == newVersionId))
        {
            throw new DuplicateDocumentVersionException("DocumentVersionId already exists.");
        }

        if (_versions.Any(v => v.Checksum.Equals(documentChecksum)))
        {
            throw new DuplicateDocumentChecksumException("Checksum duplicates a historical version.");
        }

        int currentMaxVersion = _versions.Max(v => v.VersionNumber);
        int nextVersionNumber = CalculateNextVersionNumber(currentMaxVersion);

        var newVersion = new DocumentVersion(
            id: newVersionId,
            versionNumber: nextVersionNumber,
            predecessorVersionId: expectedPredecessorVersionId,
            checksum: documentChecksum,
            contentReference: documentContentReference,
            originalFileName: originalFileName,
            mediaType: mediaType,
            fileSizeInBytes: fileSizeInBytes,
            submittedByActorId: actorId,
            submittedAt: actionTime
        );

        _versions.Add(newVersion);
        ActiveVersionId = newVersionId;
        
        checked { Revision++; }

        _domainEvents.Add(new DocumentVersionAdded(
            EventId: Guid.NewGuid(),
            OccurredOn: actionTime,
            DocumentId: Id,
            LeaseCaseId: LeaseCaseId,
            DocumentVersionId: newVersionId,
            VersionNumber: nextVersionNumber,
            PredecessorVersionId: expectedPredecessorVersionId,
            DocumentRevision: Revision,
            ChecksumAlgorithm: documentChecksum.Algorithm,
            ChecksumValue: documentChecksum.Value
        ));
    }

    internal static int CalculateNextVersionNumber(int currentVersionNumber)
    {
        if (currentVersionNumber <= 0)
        {
            throw new InvalidDocumentException("Current version number must be positive.");
        }
        
        if (currentVersionNumber == int.MaxValue)
        {
            throw new DocumentVersionNumberOverflowException("Version number overflow.");
        }

        checked
        {
            return currentVersionNumber + 1;
        }
    }
}
