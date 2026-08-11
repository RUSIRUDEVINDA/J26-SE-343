using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Domain.ValueObjects;

public sealed record LandCategory
{
    public LandCategoryType Type { get; }
    public string? Description { get; }

    public LandCategory(LandCategoryType type, string? description = null)
    {
        Type = type;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }
}
