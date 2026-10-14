using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Domain.ValueObjects;

public sealed record RecommendationCriterion
{
    public CriterionCategory Category { get; }
    public string Name { get; }
    public decimal Weight { get; }
    public decimal Score { get; }
    public string? Summary { get; }

    public RecommendationCriterion(
        CriterionCategory category,
        string name,
        decimal weight,
        decimal score,
        string? summary = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Criterion name is required.", nameof(name));
        }

        if (weight is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(weight), "Weight must be between 0 and 1.");
        }

        if (score is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(score), "Score must be between 0 and 100.");
        }

        Category = category;
        Name = name.Trim();
        Weight = weight;
        Score = score;
        Summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
    }

    public decimal WeightedScore => Score * Weight;
}
