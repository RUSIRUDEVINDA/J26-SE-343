using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Domain.ValueObjects;

/// <summary>
/// Metadata describing where an enrichable attribute value originated and how trustworthy it is.
/// </summary>
public sealed record AttributeProvenance
{
    public AttributeProvenanceSourceType SourceType { get; }
    public string? SourceName { get; }
    public decimal? Confidence { get; }
    public DateTimeOffset? CollectedAt { get; }
    public bool Verified { get; }

    public AttributeProvenance(
        AttributeProvenanceSourceType sourceType,
        string? sourceName = null,
        decimal? confidence = null,
        DateTimeOffset? collectedAt = null,
        bool verified = false)
    {
        if (!Enum.IsDefined(sourceType))
        {
            sourceType = AttributeProvenanceSourceType.Unknown;
        }

        if (confidence is < 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence), "Confidence must be between 0 and 1.");
        }

        SourceType = sourceType;
        SourceName = string.IsNullOrWhiteSpace(sourceName) ? null : sourceName.Trim();
        Confidence = confidence;
        CollectedAt = collectedAt;
        Verified = verified;
    }

    public static AttributeProvenance Unknown(string? sourceName = null) =>
        new(AttributeProvenanceSourceType.Unknown, sourceName);

    public static AttributeProvenance Official(string sourceName, bool verified = true, DateTimeOffset? collectedAt = null) =>
        new(AttributeProvenanceSourceType.Official, sourceName, confidence: verified ? 1m : null, collectedAt, verified);

    public static AttributeProvenance Derived(string sourceName, decimal? confidence = null, DateTimeOffset? collectedAt = null) =>
        new(AttributeProvenanceSourceType.Derived, sourceName, confidence, collectedAt);

    public static AttributeProvenance Synthetic(string sourceName, DateTimeOffset? collectedAt = null) =>
        new(AttributeProvenanceSourceType.Synthetic, sourceName, confidence: 1m, collectedAt, verified: false);

    public static AttributeProvenance Imputed(string sourceName, decimal? confidence = null) =>
        new(AttributeProvenanceSourceType.Imputed, sourceName, confidence);
}
