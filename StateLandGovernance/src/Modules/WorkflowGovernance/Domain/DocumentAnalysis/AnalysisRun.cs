namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;

public sealed class AnalysisRun
{
    public AnalysisRunId Id { get; }
    public int RunNumber { get; }
    public DocumentVersionId DocumentVersionId { get; }
    public DocumentChecksum DocumentChecksum { get; }
    public AnalysisModelReference ModelReference { get; }

    private readonly List<AnalysisCapabilityCode> _requestedCapabilities;
    public IReadOnlyCollection<AnalysisCapabilityCode> RequestedCapabilities => _requestedCapabilities.AsReadOnly();

    public DateTime RequestedAt { get; }
    public AnalysisRunState State { get; }

    internal AnalysisRun(
        AnalysisRunId id,
        int runNumber,
        DocumentVersionId documentVersionId,
        DocumentChecksum documentChecksum,
        AnalysisModelReference modelReference,
        IReadOnlyCollection<AnalysisCapabilityCode> requestedCapabilities,
        DateTime requestedAt)
    {
        Id = id;
        RunNumber = runNumber;
        DocumentVersionId = documentVersionId;
        DocumentChecksum = documentChecksum;
        ModelReference = modelReference;
        _requestedCapabilities = new List<AnalysisCapabilityCode>(requestedCapabilities);
        RequestedAt = requestedAt;
        State = AnalysisRunState.Requested;
    }
}
