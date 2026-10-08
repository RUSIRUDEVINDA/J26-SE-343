using System;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.GovernanceIntelligence.Domain.Services;

/// <summary>
/// Domain service interface for synthesizing transparent, explainable governance decision-support findings.
/// </summary>
public interface IExplainableGovernanceEngine
{
    /// <summary>
    /// Synthesizes structured, transparent, human-readable explanations from summarized evidence contracts.
    /// </summary>
    GovernanceExplanationResult SynthesizeExplanation(GovernanceExplanationInput input, DateTime evaluationTimestamp);
}
