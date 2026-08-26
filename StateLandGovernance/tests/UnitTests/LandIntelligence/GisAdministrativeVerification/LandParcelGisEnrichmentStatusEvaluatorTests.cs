using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.UnitTests.LandIntelligence.GisAdministrativeVerification;

public sealed class LandParcelGisEnrichmentStatusEvaluatorTests
{
    [Fact]
    public void DetermineOverallStatus_returns_Unavailable_when_administrative_is_unavailable()
    {
        var administrative = CreateAdministrative(AdministrativeLocationVerificationStatus.Unavailable);

        var status = LandParcelGisEnrichmentStatusEvaluator.DetermineOverallStatus(
            administrative,
            road: CreateRoad(RoadAccessibilityEnrichmentStatus.Unavailable),
            water: CreateWater(WaterProximityEnrichmentStatus.Unavailable),
            soil: CreateSoil(SoilGroupEnrichmentStatus.Unavailable),
            environmental: CreateEnvironmental(
                EnvironmentalSpatialConstraintEnrichmentStatus.Unavailable,
                ErosionDataStatus.Unavailable),
            failures: []);

        Assert.Equal(LandParcelGisEnrichmentOverallStatus.Unavailable, status);
    }

    [Fact]
    public void DetermineOverallStatus_returns_Complete_when_all_sections_are_available()
    {
        var status = LandParcelGisEnrichmentStatusEvaluator.DetermineOverallStatus(
            CreateAdministrative(AdministrativeLocationVerificationStatus.Verified),
            CreateRoad(RoadAccessibilityEnrichmentStatus.Available),
            CreateWater(WaterProximityEnrichmentStatus.Available),
            CreateSoil(SoilGroupEnrichmentStatus.Available),
            CreateEnvironmental(
                EnvironmentalSpatialConstraintEnrichmentStatus.Available,
                ErosionDataStatus.Available),
            failures: []);

        Assert.Equal(LandParcelGisEnrichmentOverallStatus.Complete, status);
    }

    [Fact]
    public void DetermineOverallStatus_returns_Partial_when_erosion_data_is_unavailable()
    {
        var status = LandParcelGisEnrichmentStatusEvaluator.DetermineOverallStatus(
            CreateAdministrative(AdministrativeLocationVerificationStatus.Verified),
            CreateRoad(RoadAccessibilityEnrichmentStatus.Available),
            CreateWater(WaterProximityEnrichmentStatus.Available),
            CreateSoil(SoilGroupEnrichmentStatus.Available),
            CreateEnvironmental(
                EnvironmentalSpatialConstraintEnrichmentStatus.Available,
                ErosionDataStatus.Unavailable),
            failures: []);

        Assert.Equal(LandParcelGisEnrichmentOverallStatus.Partial, status);
    }

    [Fact]
    public void DetermineOverallStatus_returns_Partial_when_one_dataset_is_unavailable()
    {
        var status = LandParcelGisEnrichmentStatusEvaluator.DetermineOverallStatus(
            CreateAdministrative(AdministrativeLocationVerificationStatus.Verified),
            CreateRoad(RoadAccessibilityEnrichmentStatus.Available),
            CreateWater(WaterProximityEnrichmentStatus.Available),
            CreateSoil(SoilGroupEnrichmentStatus.Unavailable),
            CreateEnvironmental(
                EnvironmentalSpatialConstraintEnrichmentStatus.Available,
                ErosionDataStatus.Unavailable),
            failures: []);

        Assert.Equal(LandParcelGisEnrichmentOverallStatus.Partial, status);
    }

    [Fact]
    public void DetermineOverallStatus_returns_Partial_when_section_throws()
    {
        var status = LandParcelGisEnrichmentStatusEvaluator.DetermineOverallStatus(
            CreateAdministrative(AdministrativeLocationVerificationStatus.Verified),
            CreateRoad(RoadAccessibilityEnrichmentStatus.Available),
            CreateWater(WaterProximityEnrichmentStatus.Available),
            CreateSoil(SoilGroupEnrichmentStatus.Available),
            environmental: null,
            failures:
            [
                new LandParcelGisEnrichmentSectionFailure
                {
                    Section = "Environmental",
                    Message = "Simulated failure"
                }
            ]);

        Assert.Equal(LandParcelGisEnrichmentOverallStatus.Partial, status);
    }

    [Fact]
    public void ResolveGeometryBasis_returns_shared_basis_when_all_sections_agree()
    {
        var basis = LandParcelGisEnrichmentStatusEvaluator.ResolveGeometryBasis(
            CreateAdministrative(AdministrativeLocationVerificationStatus.Verified),
            CreateRoad(RoadAccessibilityEnrichmentStatus.Available),
            CreateWater(WaterProximityEnrichmentStatus.Available),
            CreateSoil(SoilGroupEnrichmentStatus.Available),
            CreateEnvironmental(
                EnvironmentalSpatialConstraintEnrichmentStatus.Available,
                ErosionDataStatus.Unavailable));

        Assert.Equal(AdministrativeLocationGeometryBasis.Centroid, basis);
    }

    [Fact]
    public void BuildWarnings_includes_erosion_unavailable_without_claiming_safety()
    {
        var warnings = LandParcelGisEnrichmentStatusEvaluator.BuildWarnings(
            CreateAdministrative(AdministrativeLocationVerificationStatus.Verified),
            CreateRoad(RoadAccessibilityEnrichmentStatus.Available),
            CreateWater(WaterProximityEnrichmentStatus.Available),
            CreateSoil(SoilGroupEnrichmentStatus.Available),
            CreateEnvironmental(
                EnvironmentalSpatialConstraintEnrichmentStatus.Available,
                ErosionDataStatus.Unavailable),
            failures: []);

        Assert.Contains(
            warnings,
            warning => warning.Contains("Soil erosion GIS observations are unavailable", StringComparison.Ordinal));
        Assert.Contains(
            warnings,
            warning => warning.Contains("must not be interpreted", StringComparison.Ordinal));
    }

    private static AdministrativeLocationVerificationResult CreateAdministrative(
        AdministrativeLocationVerificationStatus status) =>
        new()
        {
            ParcelId = Guid.NewGuid(),
            StoredProvince = "Southern Province",
            DetectedProvince = status == AdministrativeLocationVerificationStatus.Unavailable ? null : "Southern",
            ProvinceMatches = status == AdministrativeLocationVerificationStatus.Verified,
            StoredDistrict = "Hambantota",
            DetectedDistrict = status == AdministrativeLocationVerificationStatus.Unavailable ? null : "Hambantota",
            DistrictMatches = status == AdministrativeLocationVerificationStatus.Verified,
            GeometryBasis = status == AdministrativeLocationVerificationStatus.Unavailable
                ? null
                : AdministrativeLocationGeometryBasis.Centroid,
            SourceName = "LandIntelligence_GIS",
            SourceLayer = "district_boundaries",
            Status = status,
            Evidence = ["Administrative evidence"],
            StoredProvinceProvenance = new AttributeProvenanceDto(
                AttributeProvenanceSourceType.Official, "Parcel", 1m, DateTimeOffset.UtcNow, true),
            StoredDistrictProvenance = new AttributeProvenanceDto(
                AttributeProvenanceSourceType.Official, "Parcel", 1m, DateTimeOffset.UtcNow, true),
            BoundarySourceProvenance = new AttributeProvenanceDto(
                AttributeProvenanceSourceType.ExternalAuthoritative, "GIS", 1m, DateTimeOffset.UtcNow, true)
        };

    private static RoadAccessibilityEnrichmentResult CreateRoad(RoadAccessibilityEnrichmentStatus status) =>
        new()
        {
            ParcelId = Guid.NewGuid(),
            RoadId = status == RoadAccessibilityEnrichmentStatus.Available ? Guid.NewGuid() : null,
            RoadName = status == RoadAccessibilityEnrichmentStatus.Available ? "Expressway A" : null,
            RoadType = status == RoadAccessibilityEnrichmentStatus.Available
                ? GisReferenceRoadType.Expressway
                : null,
            DistanceMeters = status == RoadAccessibilityEnrichmentStatus.Available ? 2400d : null,
            GeometryBasis = AdministrativeLocationGeometryBasis.Centroid,
            Status = status,
            Evidence = ["Road evidence"],
            SourceName = "LandIntelligence_GIS",
            SourceLayer = "expressways"
        };

    private static WaterProximityEnrichmentResult CreateWater(WaterProximityEnrichmentStatus status) =>
        new()
        {
            ParcelId = Guid.NewGuid(),
            FeatureId = status == WaterProximityEnrichmentStatus.Available ? Guid.NewGuid() : null,
            FeatureName = status == WaterProximityEnrichmentStatus.Available ? "Canal A" : null,
            FeatureType = status == WaterProximityEnrichmentStatus.Available
                ? GisReferenceWaterFeatureType.Canal
                : null,
            DistanceMeters = status == WaterProximityEnrichmentStatus.Available ? 780d : null,
            GeometryBasis = AdministrativeLocationGeometryBasis.Centroid,
            Status = status,
            Evidence = ["Water evidence"],
            SourceName = "LandIntelligence_GIS",
            SourceLayer = "canals"
        };

    private static SoilGroupEnrichmentResult CreateSoil(SoilGroupEnrichmentStatus status) =>
        new()
        {
            ParcelId = Guid.NewGuid(),
            OfficialSoilTypePreserved = true,
            PrimarySoilGroup = status == SoilGroupEnrichmentStatus.Available ? "Red Yellow Latosols" : null,
            GeometryBasis = AdministrativeLocationGeometryBasis.Centroid,
            Status = status,
            Evidence = ["Soil evidence"],
            Overlaps = [],
            SourceName = "LandIntelligence_GIS",
            SourceLayer = "soil_groups"
        };

    private static EnvironmentalSpatialConstraintEnrichmentResult CreateEnvironmental(
        EnvironmentalSpatialConstraintEnrichmentStatus status,
        ErosionDataStatus erosionDataStatus) =>
        new()
        {
            ParcelId = Guid.NewGuid(),
            Status = status,
            GeometryBasis = AdministrativeLocationGeometryBasis.Centroid,
            IntersectsSoilConservationArea = false,
            ConservationAreas = [],
            ErosionDataStatus = erosionDataStatus,
            ErosionObservations = [],
            Evidence = ["Environmental evidence"],
            SourceName = "LandIntelligence_GIS",
            SourceLayer = "soil_conservation_areas, soil_erosion"
        };
}
