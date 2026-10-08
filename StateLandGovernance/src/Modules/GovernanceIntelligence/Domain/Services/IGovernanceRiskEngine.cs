using System;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.GovernanceIntelligence.Domain.Services;

/// <summary>
/// Domain service contract for the Governance Risk & Corruption Intelligence Engine.
/// </summary>
public interface IGovernanceRiskEngine
{
    /// <summary>
    /// Evaluates governance risk observations and produces explainable risk indicators.
    /// </summary>
    /// <param name="input">Governance evidence collections to evaluate.</param>
    /// <param name="evaluationTimestamp">UTC evaluation timestamp.</param>
    /// <returns>Deterministic governance risk assessment result.</returns>
    GovernanceRiskAssessmentResult AssessRisk(GovernanceRiskEvaluationInput input, DateTime evaluationTimestamp);
}
