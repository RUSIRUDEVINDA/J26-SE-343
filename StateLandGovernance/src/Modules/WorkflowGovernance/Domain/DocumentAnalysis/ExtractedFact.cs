namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using System;

public sealed class ExtractedFact
{
    public ExtractedFactId Id { get; }
    public AnalysisRunResultId AnalysisRunResultId { get; }
    public FactCode FactCode { get; }
    public AnalysisFactValue FactValue { get; }
    public ConfidenceScore? ConfidenceScore { get; }
    public AnalysisEvidenceReference? EvidenceReference { get; }

    internal ExtractedFact(
        ExtractedFactId id,
        AnalysisRunResultId analysisRunResultId,
        FactCode factCode,
        AnalysisFactValue factValue,
        ConfidenceScore? confidenceScore,
        AnalysisEvidenceReference? evidenceReference)
    {
        Id = id;
        AnalysisRunResultId = analysisRunResultId;
        FactCode = factCode;
        FactValue = factValue;
        ConfidenceScore = confidenceScore;
        EvidenceReference = evidenceReference;
    }
}
