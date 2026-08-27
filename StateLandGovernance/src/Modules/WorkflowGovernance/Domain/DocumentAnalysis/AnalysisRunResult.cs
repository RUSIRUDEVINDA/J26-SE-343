namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class AnalysisRunResult
{
    public AnalysisRunResultId Id { get; }
    public AnalysisRunId AnalysisRunId { get; }
    public DocumentVersionId DocumentVersionId { get; }
    public DocumentChecksum DocumentChecksum { get; }
    public AnalysisModelReference ModelReference { get; }
    public AnalysisResultOutcome Outcome { get; }
    public DateTime CompletedAt { get; }

    private readonly List<AnalysisCapabilityCode> _requestedCapabilities;
    public IReadOnlyCollection<AnalysisCapabilityCode> RequestedCapabilities => _requestedCapabilities.AsReadOnly();

    private readonly List<AnalysisResultArtifactReference> _artifacts;
    public IReadOnlyCollection<AnalysisResultArtifactReference> Artifacts => _artifacts.AsReadOnly();

    private readonly List<ExtractedFact> _extractedFacts;
    public IReadOnlyCollection<ExtractedFact> ExtractedFacts => _extractedFacts.AsReadOnly();

    internal AnalysisRunResult(
        AnalysisRunResultId id,
        AnalysisRunId analysisRunId,
        DocumentVersionId documentVersionId,
        DocumentChecksum documentChecksum,
        AnalysisModelReference modelReference,
        IReadOnlyCollection<AnalysisCapabilityCode> requestedCapabilities,
        AnalysisResultOutcome outcome,
        IReadOnlyCollection<AnalysisResultArtifactReference> artifacts,
        IReadOnlyCollection<ExtractedFactInput> factInputs,
        DateTime completedAt)
    {
        if (id.Value == Guid.Empty) throw new InvalidAnalysisRunResultException("Id required.");
        
        if (!Enum.IsDefined(typeof(AnalysisResultOutcome), outcome)) throw new InvalidAnalysisRunResultException("Undefined outcome.");

        Id = id;
        if (analysisRunId.Value == Guid.Empty) throw new InvalidAnalysisRunResultException("AnalysisRunId required.");
        AnalysisRunId = analysisRunId;
        if (documentVersionId.Value == Guid.Empty) throw new InvalidAnalysisRunResultException("DocumentVersionId required.");
        DocumentVersionId = documentVersionId;
        DocumentChecksum = documentChecksum ?? throw new InvalidAnalysisRunResultException("DocumentChecksum required.");
        ModelReference = modelReference ?? throw new InvalidAnalysisRunResultException("ModelReference required.");
        
        if (requestedCapabilities == null) throw new InvalidAnalysisRunResultException("Requested capabilities cannot be null.");
        if (requestedCapabilities.Any(c => string.IsNullOrWhiteSpace(c.Value))) throw new InvalidAnalysisRunResultException("Requested capabilities contain invalid entries.");
        _requestedCapabilities = new List<AnalysisCapabilityCode>(requestedCapabilities);
        
        if (artifacts == null) throw new InvalidAnalysisRunResultException("Artifacts cannot be null.");
        if (artifacts.Any(a => a == null)) throw new InvalidAnalysisRunResultException("Artifacts cannot contain null.");
        var artList = artifacts.ToList();
        if (artList.Select(a => a.Id).Distinct().Count() != artList.Count) throw new InvalidAnalysisResultArtifactException("Duplicate artifact ID.");

        if (factInputs == null) throw new InvalidAnalysisRunResultException("Facts cannot be null.");
        if (factInputs.Any(f => f == null)) throw new InvalidAnalysisRunResultException("Facts cannot contain null.");
        var factList = factInputs.ToList();
        if (factList.Select(f => f.Id).Distinct().Count() != factList.Count) throw new InvalidExtractedFactException("Duplicate fact ID.");

        foreach (var f in factList)
        {
            if (f.EvidenceReference != null && !artList.Any(a => a.Id.Equals(f.EvidenceReference.ArtifactId)))
                throw new InvalidAnalysisEvidenceException("Evidence refers to non-existent artifact.");
        }

        if (outcome == AnalysisResultOutcome.OutputsProduced && artList.Count == 0 && factList.Count == 0)
            throw new InvalidAnalysisRunResultException("OutputsProduced requires at least one output.");
        if (outcome == AnalysisResultOutcome.NoFindings && factList.Count > 0)
            throw new InvalidAnalysisRunResultException("NoFindings cannot contain facts.");

        Outcome = outcome;
        _artifacts = artList;
        _extractedFacts = factList.Select(f => new ExtractedFact(f.Id, id, f.FactCode, f.FactValue, f.ConfidenceScore, f.EvidenceReference)).ToList();
        if (completedAt.Kind != DateTimeKind.Utc) throw new InvalidAnalysisRunTransitionException("completedAt must be UTC.");
        CompletedAt = completedAt;
    }
}


