namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;

using System;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed record WorkflowRecommendationReference
{
    public string Identifier { get; }
    public AnalysisModelReference Model { get; }
    public DateTime GeneratedAt { get; }
    public ConfidenceScore? Confidence { get; }

    public WorkflowRecommendationReference(
        string identifier,
        AnalysisModelReference model,
        DateTime generatedAt,
        ConfidenceScore? confidence)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new InvalidWorkflowPlanException("Recommendation identifier cannot be empty.");
            
        Identifier = identifier.Trim();
        if (Identifier.Any(char.IsControl))
            throw new InvalidWorkflowPlanException("Recommendation identifier cannot contain control characters.");

        if (generatedAt.Kind != DateTimeKind.Utc)
            throw new InvalidWorkflowPlanException("Recommendation GeneratedAt must be UTC.");
            
        Model = model ?? throw new InvalidWorkflowPlanException("AnalysisModelReference is required.");
        GeneratedAt = generatedAt;
        Confidence = confidence;
    }
}
