namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis.Events;

using System;
using System.Collections.Generic;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;

public sealed record AnalysisRunRequested : IDomainEvent
{
    public Guid EventId { get; }
    public DateTime OccurredOn { get; }
    public DocumentAnalysisId DocumentAnalysisId { get; }
    public AnalysisRunId AnalysisRunId { get; }
    public GovernedDocumentId GovernedDocumentId { get; }
    public DocumentVersionId DocumentVersionId { get; }
    public string ChecksumAlgorithm { get; }
    public string ChecksumValue { get; }
    public int RunNumber { get; }
    public string ModelProvider { get; }
    public string ModelName { get; }
    public string ModelVersion { get; }
    
    private readonly string[] _requestedCapabilities;
    public IReadOnlyCollection<string> RequestedCapabilities => Array.AsReadOnly(_requestedCapabilities);
    
    public int DocumentAnalysisRevision { get; }
    
    public AnalysisRunRequested(
        Guid eventId,
        DateTime occurredOn,
        DocumentAnalysisId documentAnalysisId,
        AnalysisRunId analysisRunId,
        GovernedDocumentId governedDocumentId,
        DocumentVersionId documentVersionId,
        string checksumAlgorithm,
        string checksumValue,
        int runNumber,
        string modelProvider,
        string modelName,
        string modelVersion,
        string[] requestedCapabilities,
        int documentAnalysisRevision)
    {
        EventId = eventId;
        OccurredOn = occurredOn;
        DocumentAnalysisId = documentAnalysisId;
        AnalysisRunId = analysisRunId;
        GovernedDocumentId = governedDocumentId;
        DocumentVersionId = documentVersionId;
        ChecksumAlgorithm = checksumAlgorithm;
        ChecksumValue = checksumValue;
        RunNumber = runNumber;
        ModelProvider = modelProvider;
        ModelName = modelName;
        ModelVersion = modelVersion;
        
        // Defensive copy
        if (requestedCapabilities != null)
        {
            _requestedCapabilities = new string[requestedCapabilities.Length];
            Array.Copy(requestedCapabilities, _requestedCapabilities, requestedCapabilities.Length);
        }
        else
        {
            _requestedCapabilities = Array.Empty<string>();
        }
        
        DocumentAnalysisRevision = documentAnalysisRevision;
    }
}
