using System;
using StateLandGovernance.LeaseFeasibility.Domain.Enums;

namespace StateLandGovernance.LeaseFeasibility.Domain.ValueObjects;

public sealed record ApprovalProbability
{
    public string ModelVersion { get; }
    public decimal Probability { get; }
    public RiskLevel EstimatedRiskLevel { get; }

    public ApprovalProbability(
        string modelVersion,
        decimal probability,
        RiskLevel estimatedRiskLevel)
    {
        if (string.IsNullOrWhiteSpace(modelVersion))
        {
            throw new ArgumentException("Model version is required.", nameof(modelVersion));
        }

        if (probability is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(probability), "Probability must be between 0 and 1.");
        }

        ModelVersion = modelVersion.Trim();
        Probability = probability;
        EstimatedRiskLevel = estimatedRiskLevel;
    }
}
