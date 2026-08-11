using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Infrastructure.DependencyInjection;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.LandIntelligence.Infrastructure.PostGIS;

namespace StateLandGovernance.IntegrationTests.LandIntelligence;

[Trait("Category", "Integration")]
[Trait("Component", "LandIntelligence")]
public sealed class PostGisSpatialAnalysisIntegrationTests : IAsyncLifetime
{
    private const string IntersectingParcelCadastralNumber = "SYNTHETIC-SPATIAL-PARCEL-001";
    private const string NonIntersectingParcelCadastralNumber = "SYNTHETIC-SPATIAL-PARCEL-002";
    private const string ConstraintDescription = "[SYNTHETIC] SYNTHETIC-SPATIAL-CONSTRAINT-001";
    private const string InfrastructureName = "SYNTHETIC-SPATIAL-INFRASTRUCTURE-001";

    private const double IntersectingCentroidLatitude = 6.9271;
    private const double IntersectingCentroidLongitude = 79.8612;
    private const double NonIntersectingCentroidLatitude = 7.50;
    private const double NonIntersectingCentroidLongitude = 80.50;
    private const double InfrastructureLatitude = 6.9280;
    private const double InfrastructureLongitude = 79.8620;

    private static readonly GeometryFactory GeometryFactory = NtsGeometryServices.Instance
        .CreateGeometryFactory(PostGisConfiguration.DefaultSpatialReferenceSystemId);

    private ServiceProvider? _serviceProvider;
    private ISpatialAnalysisService? _spatialAnalysisService;
    private LandIntelligenceDbContext? _dbContext;

    private Guid _intersectingParcelId;
    private Guid _nonIntersectingParcelId;
    private Guid _constraintId;
    private Guid _infrastructureFeatureId;

    public async Task InitializeAsync()
    {
        var configuration = LandIntelligenceIntegrationConfiguration.LoadApiConfiguration();

        var services = new ServiceCollection();
        services.AddLandIntelligenceInfrastructure(configuration);

        _serviceProvider = services.BuildServiceProvider();
        _spatialAnalysisService = _serviceProvider.GetRequiredService<ISpatialAnalysisService>();
        _dbContext = _serviceProvider.GetRequiredService<LandIntelligenceDbContext>();

        await RemoveSyntheticRecordsAsync();
        await SeedSyntheticSpatialDataAsync();
    }

    [Fact]
    public async Task IsPointInPolygonAsync_returns_true_for_point_inside_constraint_polygon()
    {
        var result = await _spatialAnalysisService!.IsPointInPolygonAsync(new PointInPolygonRequest
        {
            Latitude = IntersectingCentroidLatitude,
            Longitude = IntersectingCentroidLongitude,
            SpatialConstraintId = _constraintId
        });

        Assert.True(result.IsInside);
        Assert.Equal(_constraintId, result.SpatialConstraintId);
        Assert.Equal("database_constraint", result.GeometrySource);
    }

    [Fact]
    public async Task IsPointInPolygonAsync_returns_false_for_point_outside_constraint_polygon()
    {
        var result = await _spatialAnalysisService!.IsPointInPolygonAsync(new PointInPolygonRequest
        {
            Latitude = NonIntersectingCentroidLatitude,
            Longitude = NonIntersectingCentroidLongitude,
            SpatialConstraintId = _constraintId
        });

        Assert.False(result.IsInside);
        Assert.Equal(_constraintId, result.SpatialConstraintId);
    }

    [Fact]
    public async Task FindParcelsIntersectingConstraintAsync_detects_intersecting_parcel_and_excludes_non_intersecting_parcel()
    {
        var results = await _spatialAnalysisService!.FindParcelsIntersectingConstraintAsync(_constraintId);

        Assert.Contains(results, item => item.LandParcelId == _intersectingParcelId);
        Assert.DoesNotContain(results, item => item.LandParcelId == _nonIntersectingParcelId);

        var intersecting = results.Single(item => item.LandParcelId == _intersectingParcelId);
        Assert.Equal(IntersectingParcelCadastralNumber, intersecting.CadastralNumber);
        Assert.True(intersecting.IntersectsCentroid || intersecting.IntersectsBoundary);
    }

    [Fact]
    public async Task CheckGeometriesIntersectAsync_detects_intersection_for_intersecting_parcel_only()
    {
        var intersecting = await _spatialAnalysisService!.CheckGeometriesIntersectAsync(new GeometryIntersectionRequest
        {
            LandParcelId = _intersectingParcelId,
            SpatialConstraintId = _constraintId
        });

        var nonIntersecting = await _spatialAnalysisService.CheckGeometriesIntersectAsync(new GeometryIntersectionRequest
        {
            LandParcelId = _nonIntersectingParcelId,
            SpatialConstraintId = _constraintId
        });

        Assert.True(intersecting.Intersects);
        Assert.NotEqual("none", intersecting.IntersectionType);
        Assert.False(nonIntersecting.Intersects);
        Assert.Equal("none", nonIntersecting.IntersectionType);
    }

    [Fact]
    public async Task FindParcelsNearPointAsync_includes_nearby_parcel_and_excludes_far_parcel()
    {
        var results = await _spatialAnalysisService!.FindParcelsNearPointAsync(new ProximitySearchRequest
        {
            Latitude = IntersectingCentroidLatitude,
            Longitude = IntersectingCentroidLongitude,
            RadiusMeters = 5_000,
            MaxResults = 50
        });

        Assert.Contains(results, item => item.LandParcelId == _intersectingParcelId);
        Assert.DoesNotContain(results, item => item.LandParcelId == _nonIntersectingParcelId);

        var nearby = results.Single(item => item.LandParcelId == _intersectingParcelId);
        Assert.True(nearby.DistanceMeters >= 0);
        Assert.True(nearby.DistanceMeters <= 5_000);
    }

    [Fact]
    public async Task CalculateDistanceBetweenParcelAndInfrastructureAsync_returns_positive_distance()
    {
        var result = await _spatialAnalysisService!.CalculateDistanceBetweenParcelAndInfrastructureAsync(
            _intersectingParcelId,
            _infrastructureFeatureId);

        Assert.Equal(_intersectingParcelId, result.LandParcelId);
        Assert.Equal(_infrastructureFeatureId, result.InfrastructureFeatureId);
        Assert.Equal(InfrastructureName, result.InfrastructureName);
        Assert.Equal(InfrastructureFeatureType.Road, result.InfrastructureType);
        Assert.True(result.DistanceMeters > 0);
        Assert.True(result.DistanceMeters < 1_000);
    }

    [Fact]
    public async Task FilterParcelsAsync_bounding_box_includes_inside_parcel_and_excludes_outside_parcel()
    {
        var results = await _spatialAnalysisService!.FilterParcelsAsync(new SpatialFilterRequest
        {
            MinLatitude = 6.90,
            MaxLatitude = 6.95,
            MinLongitude = 79.84,
            MaxLongitude = 79.88,
            MaxResults = 50
        });

        Assert.Contains(results, item => item.LandParcelId == _intersectingParcelId);
        Assert.DoesNotContain(results, item => item.LandParcelId == _nonIntersectingParcelId);
    }

    [Fact]
    public async Task CalculateParcelAreaAsync_returns_positive_area_for_boundary_geometry()
    {
        var result = await _spatialAnalysisService!.CalculateParcelAreaAsync(_intersectingParcelId);

        Assert.Equal(_intersectingParcelId, result.LandParcelId);
        Assert.NotNull(result.CalculatedAreaSquareMeters);
        Assert.True(result.CalculatedAreaSquareMeters > 0);
        Assert.Equal("postgis_boundary", result.AreaSource);
    }

    [Fact]
    public async Task DetectSpatialConstraintsForParcelAsync_identifies_synthetic_constraint_affecting_parcel()
    {
        var detections = await _spatialAnalysisService!.DetectSpatialConstraintsForParcelAsync(_intersectingParcelId);

        Assert.Contains(
            detections,
            item => item.ConstraintId == _constraintId && item.Description == ConstraintDescription);

        var detection = detections.Single(item => item.ConstraintId == _constraintId);
        Assert.True(detection.IntersectsCentroid || detection.IntersectsBoundary);
        Assert.Contains("constraint_intersects", detection.DetectionReason, StringComparison.OrdinalIgnoreCase);
    }

    public async Task DisposeAsync()
    {
        if (_dbContext is not null)
        {
            await RemoveSyntheticRecordsAsync();
            await _dbContext.DisposeAsync();
        }

        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }
    }

    private async Task SeedSyntheticSpatialDataAsync()
    {
        var intersectingParcel = CreateParcelEntity(
            IntersectingParcelCadastralNumber,
            IntersectingCentroidLongitude,
            IntersectingCentroidLatitude,
            CreateParcelBoundary(
                (79.8600, 6.9260),
                (79.8625, 6.9260),
                (79.8625, 6.9285),
                (79.8600, 6.9285)));

        var nonIntersectingParcel = CreateParcelEntity(
            NonIntersectingParcelCadastralNumber,
            NonIntersectingCentroidLongitude,
            NonIntersectingCentroidLatitude,
            CreateParcelBoundary(
                (80.4990, 7.4990),
                (80.5010, 7.4990),
                (80.5010, 7.5010),
                (80.4990, 7.5010)));

        _intersectingParcelId = intersectingParcel.Id;
        _nonIntersectingParcelId = nonIntersectingParcel.Id;
        _constraintId = Guid.NewGuid();
        _infrastructureFeatureId = Guid.NewGuid();

        var constraint = new SpatialConstraintEntity
        {
            Id = _constraintId,
            LandParcelId = intersectingParcel.Id,
            Type = SpatialConstraintType.BufferZone,
            Description = ConstraintDescription,
            Severity = RestrictionSeverity.Medium,
            ConstraintGeometry = CreateConstraintGeometry(),
            SpatialReferenceSystemId = PostGisConfiguration.DefaultSpatialReferenceSystemId
        };

        var infrastructure = new InfrastructureFeatureEntity
        {
            Id = _infrastructureFeatureId,
            LandParcelId = intersectingParcel.Id,
            Type = InfrastructureFeatureType.Road,
            Name = InfrastructureName,
            Description = "[SYNTHETIC] Test road feature for distance analysis.",
            Location = GeometryFactory.CreatePoint(new Coordinate(InfrastructureLongitude, InfrastructureLatitude)),
            SpatialReferenceSystemId = PostGisConfiguration.DefaultSpatialReferenceSystemId
        };

        _dbContext!.LandParcels.Add(intersectingParcel);
        _dbContext.LandParcels.Add(nonIntersectingParcel);
        _dbContext.SpatialConstraints.Add(constraint);
        _dbContext.InfrastructureFeatures.Add(infrastructure);

        await _dbContext.SaveChangesAsync();
    }

    private async Task RemoveSyntheticRecordsAsync()
    {
        if (_dbContext is null)
        {
            return;
        }

        var syntheticParcels = await _dbContext.LandParcels
            .Where(parcel =>
                parcel.CadastralNumber == IntersectingParcelCadastralNumber
                || parcel.CadastralNumber == NonIntersectingParcelCadastralNumber)
            .ToListAsync();

        if (syntheticParcels.Count == 0)
        {
            return;
        }

        _dbContext.LandParcels.RemoveRange(syntheticParcels);
        await _dbContext.SaveChangesAsync();
    }

    private static LandParcelEntity CreateParcelEntity(
        string cadastralNumber,
        double centroidLongitude,
        double centroidLatitude,
        MultiPolygon boundary)
    {
        return new LandParcelEntity
        {
            Id = Guid.NewGuid(),
            CadastralNumber = cadastralNumber,
            SurveyPlanReference = "[SYNTHETIC] SPATIAL-TEST",
            LandCategoryId = Guid.Parse("aaaaaaaa-0001-4000-8000-000000000001"),
            CurrentLandUseId = Guid.Parse("bbbbbbbb-0001-4000-8000-000000000001"),
            AreaValue = 2.5m,
            AreaUnit = AreaUnit.Hectares,
            Province = "[SYNTHETIC] Western",
            District = "[SYNTHETIC] Colombo",
            DivisionalSecretariat = "[SYNTHETIC] Colombo DS",
            GramaNiladhariDivision = "[SYNTHETIC] GN-Spatial-Test",
            Centroid = GeometryFactory.CreatePoint(new Coordinate(centroidLongitude, centroidLatitude)),
            Boundary = boundary,
            SpatialReferenceSystemId = PostGisConfiguration.DefaultSpatialReferenceSystemId,
            SoilType = "[SYNTHETIC] Test soil",
            TerrainDescription = "[SYNTHETIC] Flat terrain",
            ElevationMeters = 10m
        };
    }

    private static MultiPolygon CreateParcelBoundary(params (double Longitude, double Latitude)[] ring)
    {
        return CreateMultiPolygon(ring);
    }

    private static MultiPolygon CreateConstraintGeometry()
    {
        return CreateMultiPolygon(
            (79.8400, 6.9000),
            (79.8800, 6.9000),
            (79.8800, 6.9500),
            (79.8400, 6.9500));
    }

    private static MultiPolygon CreateMultiPolygon(params (double Longitude, double Latitude)[] ring)
    {
        var coordinates = ring
            .Select(point => new Coordinate(point.Longitude, point.Latitude))
            .ToList();

        if (!coordinates[0].Equals2D(coordinates[^1]))
        {
            coordinates.Add(new Coordinate(coordinates[0].X, coordinates[0].Y));
        }

        var polygon = GeometryFactory.CreatePolygon(coordinates.ToArray());
        return GeometryFactory.CreateMultiPolygon(new[] { polygon });
    }
}
