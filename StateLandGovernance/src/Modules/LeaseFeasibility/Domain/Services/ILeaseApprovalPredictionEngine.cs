using System;
using StateLandGovernance.LeaseFeasibility.Domain.ValueObjects;

namespace StateLandGovernance.LeaseFeasibility.Domain.Services;

/// <summary>
/// Domain service contract for predicting lease approval outcomes.
/// </summary>
public interface ILeaseApprovalPredictionEngine
{
    /// <summary>
    /// Predicts the probability of lease approval based on financial profiles.
    /// </summary>
    /// <param name="input">The financial profile and history input data.</param>
    /// <param name="predictionTimestamp">UTC prediction timestamp.</param>
    /// <returns>A predicted approval probability.</returns>
    ApprovalProbability PredictApproval(object input, DateTime predictionTimestamp);
}
