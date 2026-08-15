namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

using System;
using System.Collections.Generic;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis.Events;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class DocumentAnalysis
{
    public DocumentAnalysisId Id { get; }
    public GovernedDocumentId GovernedDocumentId { get; }
    public DocumentVersionId DocumentVersionId { get; }
    public DocumentChecksum DocumentChecksum { get; }
    public int DocumentVersionNumber { get; }
    public int SourceDocumentRevision { get; }
    public DateTime CreatedAt { get; }
    public int Revision { get; }

    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public DocumentAnalysis(
        DocumentAnalysisId id,
        GovernedDocumentId governedDocumentId,
        DocumentVersionId documentVersionId,
        DocumentChecksum documentChecksum,
        int documentVersionNumber,
        int sourceDocumentRevision,
        DateTime createdAt)
    {
        if (id == default || id.Value == Guid.Empty) throw new InvalidDocumentAnalysisException("DocumentAnalysisId cannot be empty.");
        if (governedDocumentId == default || governedDocumentId.Value == Guid.Empty) throw new InvalidDocumentAnalysisException("GovernedDocumentId cannot be empty.");
        if (documentVersionId == default || documentVersionId.Value == Guid.Empty) throw new InvalidDocumentAnalysisException("DocumentVersionId cannot be empty.");
        if (documentChecksum == null) throw new InvalidDocumentAnalysisException("DocumentChecksum is required.");
        if (documentVersionNumber <= 0) throw new InvalidDocumentAnalysisException("DocumentVersionNumber must be positive.");
        if (sourceDocumentRevision <= 0) throw new InvalidDocumentAnalysisException("SourceDocumentRevision must be positive.");
        if (createdAt.Kind != DateTimeKind.Utc) throw new InvalidDocumentAnalysisException("CreatedAt must be UTC.");

        Id = id;
        GovernedDocumentId = governedDocumentId;
        DocumentVersionId = documentVersionId;
        DocumentChecksum = documentChecksum;
        DocumentVersionNumber = documentVersionNumber;
        SourceDocumentRevision = sourceDocumentRevision;
        CreatedAt = createdAt;
        Revision = 1;

        _domainEvents.Add(new DocumentAnalysisCreated(
            EventId: Guid.NewGuid(),
            OccurredOn: createdAt,
            DocumentAnalysisId: id,
            GovernedDocumentId: governedDocumentId,
            DocumentVersionId: documentVersionId,
            DocumentVersionNumber: documentVersionNumber,
            SourceDocumentRevision: sourceDocumentRevision,
            ChecksumAlgorithm: documentChecksum.Algorithm,
            ChecksumValue: documentChecksum.Value,
            DocumentAnalysisRevision: Revision
        ));
    }
}
