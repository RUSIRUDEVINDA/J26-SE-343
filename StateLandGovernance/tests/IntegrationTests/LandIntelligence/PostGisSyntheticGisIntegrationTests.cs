using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.IntegrationTests.LandIntelligence;

/// <summary>
/// Verifies PostGIS spatial operations against a controlled synthetic GIS dataset.
/// Requires PostgreSQL/PostGIS with LAND_INTELLIGENCE_CONNECTION configured in repo-root .env.
/// </summary>
[Collection(PostGisIntegrationCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Component", "LandIntelligence")]
public sealed class PostGisSyntheticGisIntegrationTests
{
    private const double DistanceToleranceMeters = 350;
    private const double AreaToleranceFraction = 0.35;

    private readonly ISpatialAnalysisService _spatialAnalysisService;
    private readonly SyntheticGisDataset _dataset;

    public PostGisSyntheticGisIntegrationTests(PostGisIntegrationFixture fixture)
    {
        _spatialAnalysisService = fixture.SpatialAnalysisService;
        _dataset = fixture.Dataset;
    }

    // --- Point-in-polygon (administrative district boundary) ---

    [Fact]
    public async Task PointInPolygon_ParcelA_inside_synthetic_district_boundary()
    {
        var result = await _spatialAnalysisService.IsPointInPolygonAsync(new PointInPolygonRequest
        {
            Latitude = _dataset.ParcelACentroidLatitude,
            Longitude = _dataset.ParcelACentroidLongitude,
            SpatialConstraintId = _dataset.DistrictBoundaryConstraintId
        });

        Assert.True(result.IsInside);
        Assert.Equal(_dataset.DistrictBoundaryConstraintId, result.SpatialConstraintId);
        Assert.Equal("database_constraint", result.GeometrySource);
    }

    [Fact]
    public async Task PointInPolygon_ParcelB_outside_synthetic_district_boundary()
    {
        var result = await _spatialAnalysisService.IsPointInPolygonAsync(new PointInPolygonRequest
        {
            Latitude = _dataset.ParcelBCentroidLatitude,
            Longitude = _dataset.ParcelBCentroidLongitude,
            SpatialConstraintId = _dataset.DistrictBoundaryConstraintId
        });

        Assert.False(result.IsInside);
    }

    // --- Polygon intersection (protected / restricted area) ---

    [Fact]
    public async Task PolygonIntersection_ParcelC_intersects_protected_area()
    {
        var result = await _spatialAnalysisService.CheckGeometriesIntersectAsync(new GeometryIntersectionRequest
        {
            LandParcelId = _dataset.ParcelCId,
            SpatialConstraintId = _dataset.ProtectedAreaConstraintId
        });

        Assert.True(result.Intersects);
        Assert.NotEqual("none", result.IntersectionType);
    }

    [Fact]
    public async Task PolygonIntersection_ParcelD_does_not_intersect_protected_area()
    {
        var result = await _spatialAnalysisService.CheckGeometriesIntersectAsync(new GeometryIntersectionRequest
        {
            LandParcelId = _dataset.ParcelDId,
            SpatialConstraintId = _dataset.ProtectedAreaConstraintId
        });

        Assert.False(result.Intersects);
        Assert.Equal("none", result.IntersectionType);
    }

    [Fact]
    public async Task FindParcelsIntersectingConstraint_detects_ParcelC_not_ParcelD_for_protected_area()
    {
        var results = await _spatialAnalysisService.FindParcelsIntersectingConstraintAsync(
            _dataset.ProtectedAreaConstraintId);

        Assert.Contains(results, item => item.LandParcelId == _dataset.ParcelCId);
        Assert.DoesNotContain(results, item => item.LandParcelId == _dataset.ParcelDId);
    }

    // --- Distance (road infrastructure) ---

    [Fact]
    public async Task Distance_ParcelE_road_within_two_kilometer_tolerance()
    {
        var result = await _spatialAnalysisService.CalculateDistanceBetweenParcelAndInfrastructureAsync(
            _dataset.ParcelEId,
            _dataset.ParcelERoadFeatureId);

        Assert.Equal(InfrastructureFeatureType.Road, result.InfrastructureType);
        Assert.Equal(SyntheticGisDataset.RoadNearParcelEName, result.InfrastructureName);
        AssertDistanceMeters(result.DistanceMeters, SyntheticGisDataset.ParcelERoadDistanceMeters);
    }

    [Fact]
    public async Task Distance_ParcelF_road_within_eight_kilometer_tolerance()
    {
        var result = await _spatialAnalysisService.CalculateDistanceBetweenParcelAndInfrastructureAsync(
            _dataset.ParcelFId,
            _dataset.ParcelFRoadFeatureId);

        Assert.Equal(InfrastructureFeatureType.Road, result.InfrastructureType);
        AssertDistanceMeters(result.DistanceMeters, SyntheticGisDataset.ParcelFRoadDistanceMeters);
    }

    // --- Proximity ---

    [Fact]
    public async Task Proximity_includes_ParcelA_excludes_ParcelB_within_five_kilometer_radius()
    {
        var results = await _spatialAnalysisService.FindParcelsNearPointAsync(new ProximitySearchRequest
        {
            Latitude = _dataset.ParcelACentroidLatitude,
            Longitude = _dataset.ParcelACentroidLongitude,
            RadiusMeters = 5_000,
            MaxResults = 100
        });

        var syntheticIds = results
            .Where(item => item.CadastralNumber.StartsWith(SyntheticGisDataset.CadastralPrefix, StringComparison.Ordinal))
            .Select(item => item.LandParcelId)
            .ToHashSet();

        Assert.Contains(_dataset.ParcelAId, syntheticIds);
        Assert.DoesNotContain(_dataset.ParcelBId, syntheticIds);
    }

    // --- Bounding-box filtering ---

    [Fact]
    public async Task BoundingBox_includes_ParcelG_excludes_ParcelH()
    {
        var results = await _spatialAnalysisService.FilterParcelsAsync(new SpatialFilterRequest
        {
            MinLatitude = 6.90,
            MaxLatitude = 6.95,
            MinLongitude = 79.84,
            MaxLongitude = 79.88,
            MaxResults = 100
        });

        Assert.Contains(results, item => item.LandParcelId == _dataset.ParcelGId);
        Assert.DoesNotContain(results, item => item.LandParcelId == _dataset.ParcelHId);
    }

    // --- Area calculation ---

    [Fact]
    public async Task AreaCalculation_returns_postgis_boundary_area_for_synthetic_parcel_polygon()
    {
        var result = await _spatialAnalysisService.CalculateParcelAreaAsync(_dataset.ParcelAId);

        Assert.Equal(_dataset.ParcelAId, result.LandParcelId);
        Assert.Equal("postgis_boundary", result.AreaSource);
        Assert.NotNull(result.CalculatedAreaSquareMeters);
        Assert.True(result.CalculatedAreaSquareMeters > 0);

        var expected = SyntheticGisDataset.ExpectedParcelAreaSquareMeters;
        var lower = expected * (1 - AreaToleranceFraction);
        var upper = expected * (1 + AreaToleranceFraction);
        Assert.InRange(result.CalculatedAreaSquareMeters.Value, lower, upper);
    }

    // --- Constraint detection ---

    [Fact]
    public async Task ConstraintDetection_identifies_protected_area_for_ParcelC()
    {
        var detections = await _spatialAnalysisService.DetectSpatialConstraintsForParcelAsync(_dataset.ParcelCId);

        Assert.Contains(
            detections,
            item => item.ConstraintId == _dataset.ProtectedAreaConstraintId
                    && item.Description == SyntheticGisDataset.ProtectedAreaDescription);
    }

    [Fact]
    public async Task AnalyzeConstraints_maps_protected_area_detection_for_ParcelC()
    {
        var constraints = await _spatialAnalysisService.AnalyzeConstraintsAsync(_dataset.ParcelCId);

        Assert.Contains(
            constraints,
            item => item.Description == SyntheticGisDataset.ProtectedAreaDescription
                    && item.Type == SpatialConstraintType.BufferZone);
    }

    // --- Synthetic layer presence (water + mixed infrastructure) ---

    [Fact]
    public async Task PointInPolygon_point_inside_synthetic_water_body_polygon()
    {
        var result = await _spatialAnalysisService.IsPointInPolygonAsync(new PointInPolygonRequest
        {
            Latitude = 6.9262,
            Longitude = 79.8602,
            SpatialConstraintId = _dataset.WaterBodyConstraintId
        });

        Assert.True(result.IsInside);
    }

    [Fact]
    public async Task Distance_water_supply_infrastructure_returns_positive_meters_for_ParcelC()
    {
        var result = await _spatialAnalysisService.CalculateDistanceBetweenParcelAndInfrastructureAsync(
            _dataset.ParcelCId,
            _dataset.WaterSupplyFeatureId);

        Assert.Equal(InfrastructureFeatureType.WaterSupply, result.InfrastructureType);
        Assert.True(result.DistanceMeters > 0);
        Assert.True(result.DistanceMeters < 2_000);
    }

    [Fact]
    public async Task Distance_railway_infrastructure_returns_positive_meters_for_ParcelA()
    {
        var result = await _spatialAnalysisService.CalculateDistanceBetweenParcelAndInfrastructureAsync(
            _dataset.ParcelAId,
            _dataset.RailwayFeatureId);

        Assert.Equal(InfrastructureFeatureType.Railway, result.InfrastructureType);
        Assert.True(result.DistanceMeters > 0);
        Assert.True(result.DistanceMeters < 2_000);
    }

    [Fact]
    public async Task FilterParcels_intersecting_constraint_returns_ParcelC_for_protected_area()
    {
        var results = await _spatialAnalysisService.FilterParcelsAsync(new SpatialFilterRequest
        {
            IntersectingConstraintId = _dataset.ProtectedAreaConstraintId,
            MaxResults = 100
        });

        Assert.Contains(results, item => item.LandParcelId == _dataset.ParcelCId);
        Assert.DoesNotContain(results, item => item.LandParcelId == _dataset.ParcelDId);
    }

    private static void AssertDistanceMeters(double actual, double expected) =>
        Assert.InRange(actual, expected - DistanceToleranceMeters, expected + DistanceToleranceMeters);
}
