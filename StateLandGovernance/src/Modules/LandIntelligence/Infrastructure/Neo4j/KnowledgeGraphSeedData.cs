using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Neo4j;

/// <summary>
/// Synthetic knowledge graph reference nodes aligned with PostgreSQL seed IDs. Not real government data.
/// </summary>
internal static class KnowledgeGraphSeedData
{
    public static IEnumerable<LandCategoryGraphNodeDto> Categories =>
        LandIntelligenceSeedData.Categories.Select(category => new LandCategoryGraphNodeDto(
            category.Id,
            category.Type.ToString(),
            category.Name,
            category.Description));

    public static IEnumerable<LandUseGraphNodeDto> LandUses =>
        LandIntelligenceSeedData.LandUses.Select(use => new LandUseGraphNodeDto(
            use.Id,
            use.Type.ToString(),
            use.Name,
            use.Description));

    public static readonly Guid SyntheticWesternColomboAreaId =
        Guid.Parse("cccccccc-0001-4000-8000-000000000001");

    public static readonly Guid SyntheticCentralKandyAreaId =
        Guid.Parse("cccccccc-0001-4000-8000-000000000002");

    public static AdministrativeAreaGraphNodeDto SyntheticWesternColomboArea => new(
        SyntheticWesternColomboAreaId,
        "Western",
        "Colombo",
        "Colombo DS",
        "GN-Synthetic-001");

    public static AdministrativeAreaGraphNodeDto SyntheticCentralKandyArea => new(
        SyntheticCentralKandyAreaId,
        "Central",
        "Kandy",
        "Kandy DS",
        "GN-Synthetic-002");

    public static RegulationGraphNodeDto SyntheticStateLandRegulation => new(
        Guid.Parse("dddddddd-0001-4000-8000-000000000001"),
        "SYNTH-GZ-2026-001",
        "[SYNTHETIC] State Land Administration Regulation",
        new DateOnly(2026, 1, 15),
        "[SYNTHETIC] Reference regulation for knowledge graph testing.");

    public static SpatialConstraintGraphNodeDto SyntheticBufferZoneConstraint => new(
        Guid.Parse("eeeeeeee-0001-4000-8000-000000000001"),
        SpatialConstraintType.BufferZone.ToString(),
        "[SYNTHETIC] 100m buffer from main road reserve",
        RestrictionSeverity.Medium.ToString());

    public static InfrastructureFeatureGraphNodeDto SyntheticHighwayFeature => new(
        Guid.Parse("ffffffff-0001-4000-8000-000000000001"),
        InfrastructureFeatureType.Road.ToString(),
        "[SYNTHETIC] Outer Circular Highway",
        850m,
        "[SYNTHETIC] Major highway proximity reference.");

    public static EnvironmentalAreaGraphNodeDto SyntheticWetlandArea => new(
        Guid.Parse("11111111-0001-4000-8000-000000000001"),
        EnvironmentalRestrictionType.Wetland.ToString(),
        "[SYNTHETIC] Adjacent wetland protection zone",
        RestrictionSeverity.High.ToString());

    public static Guid GetCategoryId(LandCategoryType type) =>
        LandIntelligenceSeedData.GetCategoryId(type);

    public static Guid GetLandUseId(LandUseType type) =>
        LandIntelligenceSeedData.GetLandUseId(type);
}
