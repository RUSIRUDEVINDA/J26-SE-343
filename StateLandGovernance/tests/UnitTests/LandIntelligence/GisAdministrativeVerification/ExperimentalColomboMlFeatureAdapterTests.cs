using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.UnitTests.LandIntelligence.GisAdministrativeVerification;

public sealed class ExperimentalColomboMlFeatureAdapterTests
{
    private readonly ExperimentalColomboMlFeatureAdapter _adapter = new();

    [Fact]
    public void BuildPayload_matches_experimental_schema_keys_and_does_not_activate_model()
    {
        var parcel = CreateParcel();
        var road = CreateAvailableRoad();
        var water = CreateAvailableWater();
        var soil = CreateAvailableSoil();
        var environmental = new EnvironmentalSpatialConstraintEnrichmentResult
        {
            ParcelId = parcel.Id,
            Status = EnvironmentalSpatialConstraintEnrichmentStatus.Available,
            GeometryBasis = AdministrativeLocationGeometryBasis.Centroid,
            IntersectsSoilConservationArea = false,
            ConservationAreas = [],
            ErosionDataStatus = ErosionDataStatus.Unavailable,
            ErosionObservations = [],
            Evidence = ["No mapped conservation intersection in assessed layer."],
            SourceName = "LandIntelligence_GIS",
            SourceLayer = "soil_conservation_areas"
        };

        var payload = _adapter.BuildPayload(
            parcel,
            LandUseType.Agricultural,
            road,
            water,
            soil,
            environmental,
            LandParcelGisEnrichmentOverallStatus.Partial);

        Assert.False(payload.ModelActivationEnabled);
        Assert.True(payload.ExperimentalPredictionSupported);
        Assert.Null(payload.ExperimentalPredictionAbstentionReason);
        Assert.Equal("Agricultural", payload.RequestedPurpose);
        Assert.Equal("StateLand", payload.LandCategory);
        Assert.Equal(1.5, payload.AreaHectares);
        Assert.Equal(42.5, payload.DistanceToRoadM);
        Assert.Equal(120.0, payload.DistanceToWaterM);
        Assert.Equal("Alluvial soils of variable drainage and texture; flat terrain", payload.DerivedSoilGroup);
        Assert.Null(payload.EnvironmentalRestrictionType);
        Assert.Null(payload.EnvironmentalRestrictionSeverity);
        Assert.False(payload.SpatialConstraintPresent);
        Assert.Equal(GisEnrichmentCoverageDefaults.OsmMotorRoadsLayer, payload.RoadSourceLayer);
        Assert.Equal("residential", payload.RoadHighwayClass);
        Assert.Equal("9001", payload.RoadOsmId);
        Assert.Equal(GisEnrichmentCoverageDefaults.OsmMotorRoadFilterPolicyVersion, payload.RoadFilterPolicyVersion);
        Assert.Equal(nameof(ExperimentalGisAssessmentStatus.AssessedPartial), payload.GisEnrichmentStatus);
        Assert.Contains("distance_to_road_m", payload.GisDerivedFields);
        Assert.Contains("environmental_restriction_type", payload.SimulatedOrUnavailableFields);
        Assert.DoesNotContain(payload.RemainingMismatches, m => m.Contains("fabricate", StringComparison.OrdinalIgnoreCase) && m.Contains("area"));
    }

    [Fact]
    public void BuildPayload_does_not_coerce_missing_road_distance_to_zero()
    {
        var parcel = CreateParcel();
        var road = new RoadAccessibilityEnrichmentResult
        {
            ParcelId = parcel.Id,
            Status = RoadAccessibilityEnrichmentStatus.OutsideCoverage,
            DistanceMeters = null,
            Evidence = ["Outside coverage"],
            SourceName = "LandIntelligence_GIS",
            SourceLayer = GisEnrichmentCoverageDefaults.OsmMotorRoadsLayer
        };

        var payload = _adapter.BuildPayload(parcel, LandUseType.Commercial, road, null, null, null);

        Assert.Null(payload.DistanceToRoadM);
        Assert.False(payload.ExperimentalPredictionSupported);
        Assert.Contains("abstained", payload.ExperimentalPredictionAbstentionReason ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.Equal(nameof(ExperimentalGisAssessmentStatus.Unavailable), payload.GisEnrichmentStatus);
        Assert.Contains(payload.RemainingMismatches, m => m.Contains("OutsideCoverage", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildPayload_abstains_when_conservation_assessment_unavailable_without_coercing_false()
    {
        var parcel = CreateParcel();
        var environmental = new EnvironmentalSpatialConstraintEnrichmentResult
        {
            ParcelId = parcel.Id,
            Status = EnvironmentalSpatialConstraintEnrichmentStatus.Unavailable,
            IntersectsSoilConservationArea = false,
            ConservationAreas = [],
            ErosionDataStatus = ErosionDataStatus.Unavailable,
            ErosionObservations = [],
            Evidence = ["Layer missing"],
            SourceName = "LandIntelligence_GIS",
            SourceLayer = "soil_conservation_areas"
        };

        var payload = _adapter.BuildPayload(
            parcel,
            LandUseType.Agricultural,
            CreateAvailableRoad(),
            CreateAvailableWater(),
            CreateAvailableSoil(),
            environmental,
            LandParcelGisEnrichmentOverallStatus.Partial);

        Assert.Null(payload.SpatialConstraintPresent);
        Assert.False(payload.ExperimentalPredictionSupported);
        Assert.Contains("null", payload.ExperimentalPredictionAbstentionReason ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            payload.RemainingMismatches,
            m => m.Contains("not coerced to false", StringComparison.OrdinalIgnoreCase)
                 || m.Contains("Never coerce", StringComparison.OrdinalIgnoreCase)
                 || m.Contains("remains null", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildPayload_conservation_false_is_not_unknown_when_assessment_available()
    {
        var parcel = CreateParcel();
        var environmental = new EnvironmentalSpatialConstraintEnrichmentResult
        {
            ParcelId = parcel.Id,
            Status = EnvironmentalSpatialConstraintEnrichmentStatus.Available,
            IntersectsSoilConservationArea = false,
            ConservationAreas = [],
            ErosionDataStatus = ErosionDataStatus.Available,
            ErosionObservations = [],
            Evidence = [],
            SourceName = "LandIntelligence_GIS",
            SourceLayer = "soil_conservation_areas"
        };

        var payload = _adapter.BuildPayload(
            parcel,
            LandUseType.Residential,
            CreateAvailableRoad(),
            CreateAvailableWater(),
            CreateAvailableSoil(),
            environmental,
            LandParcelGisEnrichmentOverallStatus.Complete);

        Assert.False(payload.SpatialConstraintPresent);
        Assert.True(payload.ExperimentalPredictionSupported);
        Assert.Equal(nameof(ExperimentalGisAssessmentStatus.AssessedComplete), payload.GisEnrichmentStatus);
    }

    [Fact]
    public void MapExperimentalStatus_does_not_silently_replace_domain_enum_meanings()
    {
        Assert.Equal(
            ExperimentalGisAssessmentStatus.AssessedComplete,
            ExperimentalColomboMlFeatureAdapter.MapExperimentalStatus(
                LandParcelGisEnrichmentOverallStatus.Complete,
                CreateAvailableRoad(),
                CreateAvailableWater(),
                CreateAvailableSoil(),
                new EnvironmentalSpatialConstraintEnrichmentResult
                {
                    ParcelId = Guid.NewGuid(),
                    Status = EnvironmentalSpatialConstraintEnrichmentStatus.Available,
                    IntersectsSoilConservationArea = true,
                    ConservationAreas = [],
                    ErosionDataStatus = ErosionDataStatus.Available,
                    ErosionObservations = [],
                    Evidence = [],
                    SourceName = "LandIntelligence_GIS",
                    SourceLayer = "soil_conservation_areas"
                }));

        Assert.NotEqual(
            nameof(GisEnrichmentOverallStatus.Complete),
            nameof(ExperimentalGisAssessmentStatus.AssessedComplete));
    }

    private static LandParcel CreateParcel() =>
        new(
            new ParcelIdentifier("CAD-1", null),
            new LandCategory(LandCategoryType.StateLand, null),
            new LandArea(1.5m, AreaUnit.Hectares),
            new AdministrativeLocation("Western", "Colombo", "Colombo", null),
            new SpatialReference(6.93, 79.86, "EPSG:4326"));

    private static RoadAccessibilityEnrichmentResult CreateAvailableRoad() =>
        new()
        {
            ParcelId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            RoadId = Guid.NewGuid(),
            RoadName = "Sample Street",
            RoadType = GisReferenceRoadType.Unspecified,
            DistanceMeters = 42.5,
            GeometryBasis = AdministrativeLocationGeometryBasis.Centroid,
            Status = RoadAccessibilityEnrichmentStatus.Available,
            Evidence = [],
            SourceName =
                $"{GisEnrichmentCoverageDefaults.OsmMotorRoadSourceName}/{GisEnrichmentCoverageDefaults.OsmMotorRoadsLayer}@{GisEnrichmentCoverageDefaults.OsmMotorRoadFilterPolicyVersion}",
            SourceLayer = GisEnrichmentCoverageDefaults.OsmMotorRoadsLayer,
            HighwayClass = "residential",
            OsmId = "9001",
            FilterPolicyVersion = GisEnrichmentCoverageDefaults.OsmMotorRoadFilterPolicyVersion
        };

    private static WaterProximityEnrichmentResult CreateAvailableWater() =>
        new()
        {
            ParcelId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            FeatureId = Guid.NewGuid(),
            FeatureName = "Canal",
            FeatureType = GisReferenceWaterFeatureType.Canal,
            DistanceMeters = 120.0,
            Status = WaterProximityEnrichmentStatus.Available,
            Evidence = [],
            SourceName = "LandIntelligence_GIS",
            SourceLayer = "canals"
        };

    private static SoilGroupEnrichmentResult CreateAvailableSoil() =>
        new()
        {
            ParcelId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            OfficialSoilTypePreserved = false,
            PrimarySoilGroup = "Alluvial soils of variable drainage and texture; flat terrain",
            PrimarySoilGroupId = Guid.NewGuid(),
            Status = SoilGroupEnrichmentStatus.Available,
            Evidence = [],
            Overlaps = [],
            SourceName = "LandIntelligence_GIS",
            SourceLayer = "soil_groups"
        };
}
