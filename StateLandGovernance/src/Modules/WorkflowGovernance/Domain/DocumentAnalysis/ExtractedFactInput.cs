namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class ExtractedFactInput
{
    public ExtractedFactId Id { get; }
    public FactCode FactCode { get; }
    public AnalysisFactValue FactValue { get; }
    public ConfidenceScore? ConfidenceScore { get; }
    public AnalysisEvidenceReference? EvidenceReference { get; }
    public ExtractedFactInput(ExtractedFactId id, FactCode factCode, AnalysisFactValue factValue, ConfidenceScore? confidenceScore, AnalysisEvidenceReference? evidenceReference)
    {
        if (id.Value == Guid.Empty) throw new InvalidExtractedFactException("Id required.");
        Id = id;
        FactCode = factCode ?? throw new InvalidExtractedFactException("FactCode required.");
        FactValue = factValue ?? throw new InvalidExtractedFactException("FactValue required.");
        ConfidenceScore = confidenceScore;
        EvidenceReference = evidenceReference;
    }
}
