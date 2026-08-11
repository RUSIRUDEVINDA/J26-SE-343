using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence;

/// <summary>
/// Synthetic reference data for Component 1 lookup tables. Not real government data.
/// </summary>
internal static class LandIntelligenceSeedData
{
    public static readonly Guid StateLandCategoryId = Guid.Parse("aaaaaaaa-0001-4000-8000-000000000001");
    public static readonly Guid CrownLandCategoryId = Guid.Parse("aaaaaaaa-0001-4000-8000-000000000002");
    public static readonly Guid ReservedLandCategoryId = Guid.Parse("aaaaaaaa-0001-4000-8000-000000000003");
    public static readonly Guid OtherLandCategoryId = Guid.Parse("aaaaaaaa-0001-4000-8000-000000000099");

    public static readonly Guid AgriculturalLandUseId = Guid.Parse("bbbbbbbb-0001-4000-8000-000000000001");
    public static readonly Guid CommercialLandUseId = Guid.Parse("bbbbbbbb-0001-4000-8000-000000000002");
    public static readonly Guid IndustrialLandUseId = Guid.Parse("bbbbbbbb-0001-4000-8000-000000000003");
    public static readonly Guid ResidentialLandUseId = Guid.Parse("bbbbbbbb-0001-4000-8000-000000000004");
    public static readonly Guid TourismLandUseId = Guid.Parse("bbbbbbbb-0001-4000-8000-000000000005");
    public static readonly Guid ConservationLandUseId = Guid.Parse("bbbbbbbb-0001-4000-8000-000000000006");
    public static readonly Guid MixedUseLandUseId = Guid.Parse("bbbbbbbb-0001-4000-8000-000000000007");
    public static readonly Guid OtherLandUseId = Guid.Parse("bbbbbbbb-0001-4000-8000-000000000099");

    public static IEnumerable<LandCategoryEntity> Categories =>
    [
        new()
        {
            Id = StateLandCategoryId,
            Type = LandCategoryType.StateLand,
            Name = "State Land",
            Description = "[SYNTHETIC] State-owned land category for development/testing."
        },
        new()
        {
            Id = CrownLandCategoryId,
            Type = LandCategoryType.CrownLand,
            Name = "Crown Land",
            Description = "[SYNTHETIC] Crown land category for development/testing."
        },
        new()
        {
            Id = ReservedLandCategoryId,
            Type = LandCategoryType.ReservedLand,
            Name = "Reserved Land",
            Description = "[SYNTHETIC] Reserved land category for development/testing."
        },
        new()
        {
            Id = OtherLandCategoryId,
            Type = LandCategoryType.Other,
            Name = "Other",
            Description = "[SYNTHETIC] Other land category for development/testing."
        }
    ];

    public static IEnumerable<LandUseEntity> LandUses =>
    [
        new() { Id = AgriculturalLandUseId, Type = LandUseType.Agricultural, Name = "Agricultural", Description = "[SYNTHETIC]" },
        new() { Id = CommercialLandUseId, Type = LandUseType.Commercial, Name = "Commercial", Description = "[SYNTHETIC]" },
        new() { Id = IndustrialLandUseId, Type = LandUseType.Industrial, Name = "Industrial", Description = "[SYNTHETIC]" },
        new() { Id = ResidentialLandUseId, Type = LandUseType.Residential, Name = "Residential", Description = "[SYNTHETIC]" },
        new() { Id = TourismLandUseId, Type = LandUseType.Tourism, Name = "Tourism", Description = "[SYNTHETIC]" },
        new() { Id = ConservationLandUseId, Type = LandUseType.Conservation, Name = "Conservation", Description = "[SYNTHETIC]" },
        new() { Id = MixedUseLandUseId, Type = LandUseType.MixedUse, Name = "Mixed Use", Description = "[SYNTHETIC]" },
        new() { Id = OtherLandUseId, Type = LandUseType.Other, Name = "Other", Description = "[SYNTHETIC]" }
    ];

    public static Guid GetCategoryId(LandCategoryType type) => type switch
    {
        LandCategoryType.StateLand => StateLandCategoryId,
        LandCategoryType.CrownLand => CrownLandCategoryId,
        LandCategoryType.ReservedLand => ReservedLandCategoryId,
        _ => OtherLandCategoryId
    };

    public static Guid GetLandUseId(LandUseType type) => type switch
    {
        LandUseType.Agricultural => AgriculturalLandUseId,
        LandUseType.Commercial => CommercialLandUseId,
        LandUseType.Industrial => IndustrialLandUseId,
        LandUseType.Residential => ResidentialLandUseId,
        LandUseType.Tourism => TourismLandUseId,
        LandUseType.Conservation => ConservationLandUseId,
        LandUseType.MixedUse => MixedUseLandUseId,
        _ => OtherLandUseId
    };
}
