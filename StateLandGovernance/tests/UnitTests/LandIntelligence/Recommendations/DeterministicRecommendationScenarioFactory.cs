using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.UnitTests.LandIntelligence.Recommendations;

internal enum ScenarioParcelRole
{
    HighSuitability,
    MediumSuitability,
    LowSuitability,
    EnvironmentalRestriction,
    PoorAccessibility,
    WrongLandUse,
    WrongLocation,
    MissingData,
    RegulatoryComplexity
}

internal static class DeterministicRecommendationScenarioFactory
{
    public const string AgriculturalPrefix = "AGRI";
    public const string ResidentialPrefix = "RES";
    public const string CommercialPrefix = "COM";

    public static IReadOnlyList<LandParcel> CreateScenarioParcels(LandUseType purpose, string prefix) =>
    [
        CreateParcel(ScenarioParcelRole.HighSuitability, purpose, $"{prefix}-A"),
        CreateParcel(ScenarioParcelRole.MediumSuitability, purpose, $"{prefix}-B"),
        CreateParcel(ScenarioParcelRole.LowSuitability, purpose, $"{prefix}-C"),
        CreateParcel(ScenarioParcelRole.EnvironmentalRestriction, purpose, $"{prefix}-D"),
        CreateParcel(ScenarioParcelRole.PoorAccessibility, purpose, $"{prefix}-E"),
        CreateParcel(ScenarioParcelRole.WrongLandUse, purpose, $"{prefix}-F"),
        CreateParcel(ScenarioParcelRole.WrongLocation, purpose, $"{prefix}-G"),
        CreateParcel(ScenarioParcelRole.MissingData, purpose, $"{prefix}-H"),
        CreateParcel(ScenarioParcelRole.RegulatoryComplexity, purpose, $"{prefix}-I")
    ];

    public static LandParcel CreateParcel(ScenarioParcelRole role, LandUseType targetPurpose, string cadastralNumber)
    {
        var (category, use, area, province, district, roadDistance, characteristics, spatialSeverity, envSeverity, regulatoryCount) =
            ResolveProfile(role, targetPurpose);

        var parcel = new LandParcel(
            new ParcelIdentifier(cadastralNumber, "SCENARIO-PLAN"),
            new LandCategory(category, $"[SCENARIO] {role}"),
            new LandArea(area, AreaUnit.Hectares),
            new AdministrativeLocation(province, district, $"{district} DS"),
            new SpatialReference(6.9271, 79.8612, "EPSG:4326"),
            new LandUse(use, $"[SCENARIO] {use}"),
            characteristics);

        if (roadDistance.HasValue)
        {
            parcel.AddInfrastructureFeature(new InfrastructureFeature(
                InfrastructureFeatureType.Road,
                "[SCENARIO] Access Road",
                roadDistance.Value));
        }

        if (spatialSeverity is not null)
        {
            parcel.AddSpatialConstraint(new SpatialConstraint(
                SpatialConstraintType.Setback,
                $"[SCENARIO] Spatial constraint ({spatialSeverity})",
                spatialSeverity.Value));
        }

        if (envSeverity is not null)
        {
            parcel.AddEnvironmentalRestriction(new EnvironmentalRestriction(
                EnvironmentalRestrictionType.Wetland,
                $"[SCENARIO] Environmental restriction ({envSeverity})",
                envSeverity.Value));
        }

        if (regulatoryCount > 0)
        {
            for (var index = 1; index <= regulatoryCount; index++)
            {
                parcel.AddRegulatoryReference(new RegulatoryReference(
                    $"{cadastralNumber}-GZ-{index:000}",
                    $"[SCENARIO] Regulatory reference {index}",
                    new DateOnly(2026, 1, index)));
            }
        }

        return parcel;
    }

    private static (
        LandCategoryType Category,
        LandUseType Use,
        decimal AreaHectares,
        string Province,
        string District,
        decimal? RoadDistanceMeters,
        LandCharacteristics? Characteristics,
        RestrictionSeverity? SpatialSeverity,
        RestrictionSeverity? EnvironmentalSeverity,
        int RegulatoryReferenceCount) ResolveProfile(ScenarioParcelRole role, LandUseType targetPurpose)
    {
        var wrongUse = targetPurpose switch
        {
            LandUseType.Agricultural => LandUseType.Industrial,
            LandUseType.Residential => LandUseType.Commercial,
            LandUseType.Commercial => LandUseType.Agricultural,
            _ => LandUseType.Other
        };

        return role switch
        {
            ScenarioParcelRole.HighSuitability => (
                LandCategoryType.StateLand,
                targetPurpose,
                5m,
                "Western",
                "Colombo",
                250m,
                new LandCharacteristics("Loam", "Flat", 20m),
                null,
                null,
                0),
            ScenarioParcelRole.MediumSuitability => (
                LandCategoryType.StateLand,
                targetPurpose,
                4.5m,
                "Western",
                "Gampaha",
                1200m,
                new LandCharacteristics("Loam", "Gently sloping", 25m),
                RestrictionSeverity.Medium,
                null,
                0),
            ScenarioParcelRole.LowSuitability => (
                LandCategoryType.StateLand,
                targetPurpose,
                5m,
                "Western",
                "Colombo",
                250m,
                new LandCharacteristics("Loam", "Flat", 20m),
                RestrictionSeverity.High,
                null,
                0),
            ScenarioParcelRole.EnvironmentalRestriction => (
                LandCategoryType.StateLand,
                targetPurpose,
                5m,
                "Western",
                "Colombo",
                250m,
                new LandCharacteristics("Loam", "Flat", 20m),
                null,
                RestrictionSeverity.Prohibitive,
                0),
            ScenarioParcelRole.PoorAccessibility => (
                LandCategoryType.StateLand,
                targetPurpose,
                5m,
                "Western",
                "Colombo",
                8000m,
                new LandCharacteristics("Loam", "Flat", 20m),
                null,
                null,
                0),
            ScenarioParcelRole.WrongLandUse => (
                LandCategoryType.StateLand,
                wrongUse,
                5m,
                "Western",
                "Colombo",
                250m,
                new LandCharacteristics("Loam", "Flat", 20m),
                null,
                null,
                0),
            ScenarioParcelRole.WrongLocation => (
                LandCategoryType.StateLand,
                targetPurpose,
                5m,
                "Central",
                "Kandy",
                250m,
                new LandCharacteristics("Loam", "Flat", 20m),
                null,
                null,
                0),
            ScenarioParcelRole.MissingData => (
                LandCategoryType.StateLand,
                targetPurpose,
                5m,
                "Western",
                "Colombo",
                250m,
                null,
                null,
                null,
                0),
            ScenarioParcelRole.RegulatoryComplexity => (
                LandCategoryType.StateLand,
                targetPurpose,
                5m,
                "Western",
                "Colombo",
                250m,
                new LandCharacteristics("Loam", "Flat", 20m),
                null,
                null,
                5),
            _ => throw new ArgumentOutOfRangeException(nameof(role))
        };
    }
}
