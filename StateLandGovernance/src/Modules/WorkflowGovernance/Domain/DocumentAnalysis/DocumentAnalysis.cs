namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

using System;
using System.Collections.Generic;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis.Events;
using System.Linq;
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
    public int Revision { get; private set; }

    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private readonly List<AnalysisRun> _runs = new();
    public IReadOnlyCollection<AnalysisRun> Runs => _runs.AsReadOnly();

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

    internal static int CalculateNextRunNumber(int? currentMaximum)
    {
        if (currentMaximum == null) return 1;
        if (currentMaximum <= 0) throw new DocumentAnalysisRunNumberOverflowException("Current maximum run number must be positive.");

        try
        {
            return checked(currentMaximum.Value + 1);
        }
        catch (OverflowException)
        {
            throw new DocumentAnalysisRunNumberOverflowException("Run number overflowed.");
        }
    }

    internal static int CalculateNextRevision(int currentRevision)
    {
        if (currentRevision <= 0) throw new DocumentAnalysisRevisionOverflowException("Current revision must be positive.");
        try
        {
            return checked(currentRevision + 1);
        }
        catch (OverflowException)
        {
            throw new DocumentAnalysisRevisionOverflowException("DocumentAnalysis revision overflowed.");
        }
    }

    public void RequestRun(
        AnalysisRunId analysisRunId,
        AnalysisModelReference modelReference,
        IReadOnlyCollection<AnalysisCapabilityCode> requestedCapabilities,
        DateTime requestedAt)
    {
        if (analysisRunId == default || analysisRunId.Value == Guid.Empty) throw new InvalidAnalysisRunException("AnalysisRunId cannot be empty.");
        if (modelReference == null) throw new InvalidAnalysisRunException("AnalysisModelReference cannot be null.");
        if (requestedCapabilities == null || !requestedCapabilities.Any()) throw new InvalidAnalysisRunException("Requested capabilities cannot be null or empty.");
        if (requestedAt.Kind != DateTimeKind.Utc) throw new InvalidAnalysisRunException("RequestedAt must be UTC.");

        var localCaps = requestedCapabilities.ToList();
        var distinctCaps = new HashSet<AnalysisCapabilityCode>();
        foreach (var cap in localCaps)
        {
            if (cap.Value == null) throw new InvalidAnalysisRunException("Requested capabilities contain invalid entries.");
            if (!distinctCaps.Add(cap))
            {
                throw new InvalidAnalysisRunException("Requested capabilities contain duplicates.");
            }
        }
        
        var normalizedCapabilities = distinctCaps
            .OrderBy(c => c.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingRun = _runs.FirstOrDefault(r => r.Id.Value == analysisRunId.Value);
        if (existingRun != null)
        {
            var isSameModel = existingRun.ModelReference.Equals(modelReference);

            var isSameTime = existingRun.RequestedAt == requestedAt;
            var isSameCaps = existingRun.RequestedCapabilities.SequenceEqual(normalizedCapabilities);

            if (isSameModel && isSameTime && isSameCaps)
            {
                throw new DuplicateAnalysisRunException("AnalysisRunId already exists with identical request data.");
            }

            throw new ConflictingAnalysisRunException("AnalysisRunId already exists with differing request data.");
        }

        var nextRunNumber = CalculateNextRunNumber(_runs.Count > 0 ? _runs.Max(r => r.RunNumber) : null);
        var nextRevision = CalculateNextRevision(Revision);

        var run = new AnalysisRun(
            analysisRunId,
            nextRunNumber,
            DocumentVersionId,
            DocumentChecksum,
            modelReference,
            normalizedCapabilities,
            requestedAt
        );

        var evt = new AnalysisRunRequested(
            Guid.NewGuid(),
            requestedAt,
            Id,
            analysisRunId,
            GovernedDocumentId,
            DocumentVersionId,
            DocumentChecksum.Algorithm,
            DocumentChecksum.Value,
            nextRunNumber,
            modelReference.Provider,
            modelReference.ModelName,
            modelReference.ModelVersion,
            normalizedCapabilities.Select(c => c.Value).ToArray(),
            nextRevision
        );

        _runs.Add(run);
        Revision = nextRevision;
        _domainEvents.Add(evt);
    }
}
