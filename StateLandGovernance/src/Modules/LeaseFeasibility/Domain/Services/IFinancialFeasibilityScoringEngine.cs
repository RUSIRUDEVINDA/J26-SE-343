using System;
using StateLandGovernance.LeaseFeasibility.Domain.Entities;
using StateLandGovernance.LeaseFeasibility.Domain.ValueObjects;

namespace StateLandGovernance.LeaseFeasibility.Domain.Services;

/// <summary>
/// Domain service contract for evaluating financial feasibility.
/// </summary>
public interface IFinancialFeasibilityScoringEngine
{
    /// <summary>
    /// Evaluates financial evidence and produces a feasibility assessment.
    /// </summary>
    /// <param name="input">The typed, unit-explicit scoring input.</param>
    /// <param name="evaluationTimestamp">Evaluation timestamp supplied by the caller's TimeProvider.</param>
    /// <returns>A deterministic financial feasibility assessment.</returns>
    FinancialFeasibilityAssessment EvaluateFeasibility(
        FinancialFeasibilityScoringInput input,
        DateTimeOffset evaluationTimestamp);
}
