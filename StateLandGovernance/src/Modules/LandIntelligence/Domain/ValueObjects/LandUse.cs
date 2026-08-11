using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Domain.ValueObjects;

public sealed record LandUse
{
    public LandUseType Type { get; }
    public string? Description { get; }

    public LandUse(LandUseType type, string? description = null)
    {
        Type = type;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }
}
