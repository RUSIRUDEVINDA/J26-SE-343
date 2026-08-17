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
    /// <param name="input">The financial profile input data.</param>
    /// <param name="evaluationTimestamp">UTC evaluation timestamp.</param>
    /// <returns>A deterministic financial feasibility assessment.</returns>
    FinancialFeasibilityAssessment EvaluateFeasibility(object input, DateTime evaluationTimestamp);
}
