namespace StateLandGovernance.LandIntelligence.Domain.ValueObjects;

public sealed record RecommendationEvidence
{
    public string Source { get; }
    public string Description { get; }
    public string? RelatedCriterionName { get; }

    public RecommendationEvidence(
        string source,
        string description,
        string? relatedCriterionName = null)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("Evidence source is required.", nameof(source));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Evidence description is required.", nameof(description));
        }

        Source = source.Trim();
        Description = description.Trim();
        RelatedCriterionName = string.IsNullOrWhiteSpace(relatedCriterionName)
            ? null
            : relatedCriterionName.Trim();
    }
}
