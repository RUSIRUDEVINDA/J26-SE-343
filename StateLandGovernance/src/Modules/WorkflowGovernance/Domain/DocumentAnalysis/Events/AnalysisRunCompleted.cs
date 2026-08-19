namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis.Events;

using System;
using System.Collections.Generic;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using System.Collections.ObjectModel;
using System.Linq;

public sealed class AnalysisRunCompleted : IDomainEvent
{
    public Guid EventId { get; }
    public DateTime OccurredOn { get; }
    public DocumentAnalysisId DocumentAnalysisId { get; }
    public AnalysisRunId AnalysisRunId { get; }
    public AnalysisRunResultId AnalysisRunResultId { get; }
    public GovernedDocumentId GovernedDocumentId { get; }
    public DocumentVersionId DocumentVersionId { get; }
    public string ChecksumAlgorithm { get; }
    public string ChecksumValue { get; }
    public int RunNumber { get; }
    public string ModelProvider { get; }
    public string ModelName { get; }
    public string ModelVersion { get; }
    public IReadOnlyCollection<string> RequestedCapabilities { get; }
    public AnalysisResultOutcome Outcome { get; }
    public int ArtifactCount { get; }
    public int ExtractedFactCount { get; }
    public int DocumentAnalysisRevision { get; }

    public AnalysisRunCompleted(
        Guid eventId,
        DateTime occurredOn,
        DocumentAnalysisId documentAnalysisId,
        AnalysisRunId analysisRunId,
        AnalysisRunResultId analysisRunResultId,
        GovernedDocumentId governedDocumentId,
        DocumentVersionId documentVersionId,
        string checksumAlgorithm,
        string checksumValue,
        int runNumber,
        string modelProvider,
        string modelName,
        string modelVersion,
        IEnumerable<string> requestedCapabilities,
        AnalysisResultOutcome outcome,
        int artifactCount,
        int extractedFactCount,
        int documentAnalysisRevision)
    {
        EventId = eventId;
        OccurredOn = occurredOn;
        DocumentAnalysisId = documentAnalysisId;
        AnalysisRunId = analysisRunId;
        AnalysisRunResultId = analysisRunResultId;
        GovernedDocumentId = governedDocumentId;
        DocumentVersionId = documentVersionId;
        ChecksumAlgorithm = checksumAlgorithm;
        ChecksumValue = checksumValue;
        RunNumber = runNumber;
        ModelProvider = modelProvider;
        ModelName = modelName;
        ModelVersion = modelVersion;
        RequestedCapabilities = new ReadOnlyCollection<string>(requestedCapabilities.ToList());
        Outcome = outcome;
        ArtifactCount = artifactCount;
        ExtractedFactCount = extractedFactCount;
        DocumentAnalysisRevision = documentAnalysisRevision;
    }
}
