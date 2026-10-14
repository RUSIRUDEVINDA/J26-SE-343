using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Infrastructure.DependencyInjection;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;
using Xunit.Abstractions;

namespace StateLandGovernance.IntegrationTests.LandIntelligence;

[Trait("Category", "Integration")]
[Trait("Component", "LandIntelligence")]
public sealed class RoadAccessibilityEnrichmentIntegrationTests : IAsyncLifetime
{
    private const double OutsideCoverageLatitude = 6.9271;
    private const double OutsideCoverageLongitude = 79.8612;
    private const double DistanceToleranceMeters = 5d;

    private readonly ITestOutputHelper _output;
    private ServiceProvider? _serviceProvider;
    private ILandParcelRepository? _repository;
    private IRoadAccessibilityEnrichmentService? _enrichmentService;
    private LandIntelligenceDbContext? _dbContext;
    private double _hambantotaLatitude;
    private double _hambantotaLongitude;
    private double _roadPointLatitude;
    private double _roadPointLongitude;
    private readonly List<Guid> _createdParcelIds = [];

    public RoadAccessibilityEnrichmentIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        var configuration = LandIntelligenceIntegrationConfiguration.LoadApiConfiguration();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLandIntelligenceInfrastructure(configuration);

        _serviceProvider = services.BuildServiceProvider();
        _repository = _serviceProvider.GetRequiredService<ILandParcelRepository>();
        _enrichmentService = _serviceProvider.GetRequiredService<IRoadAccessibilityEnrichmentService>();
        _dbContext = _serviceProvider.GetRequiredService<LandIntelligenceDbContext>();

        var importService = _serviceProvider.GetRequiredService<IGisReferenceDataImportService>();
        await importService.ImportHambantotaPilotAsync(GisReferenceDataPaths.ResolveDataRoot(null));

        (_hambantotaLatitude, _hambantotaLongitude) = await ReadHambantotaInteriorPointAsync();
        (_roadPointLatitude, _roadPointLongitude) = await ReadRoadStartPointAsync();
    }

    [Fact]
    public async Task EnrichAsync_returns_nearest_road_for_parcel_inside_Hambantota_pilot()
    {
        Assert.NotNull(_repository);
        Assert.NotNull(_enrichmentService);

        var parcel = await PersistParcelAsync(
            _hambantotaLatitude,
            _hambantotaLongitude,
            boundary: null);

        var result = await _enrichmentService.EnrichAsync(parcel.Id);
        WriteResult(result);

        Assert.Equal(RoadAccessibilityEnrichmentStatus.Available, result.Status);
        Assert.NotNull(result.RoadId);
        Assert.NotNull(result.DistanceMeters);
        Assert.True(result.DistanceMeters >= 0);
        Assert.Equal(AdministrativeLocationGeometryBasis.Centroid, result.GeometryBasis);
    }

    [Fact]
    public async Task EnrichAsync_returns_distance_in_meters_using_geography()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);

        var parcel = await PersistParcelAsync(
            _hambantotaLatitude,
            _hambantotaLongitude);

        var result = await _enrichmentService.EnrichAsync(parcel.Id);

        Assert.Equal(RoadAccessibilityEnrichmentStatus.Available, result.Status);
        Assert.NotNull(result.DistanceMeters);
        Assert.InRange(result.DistanceMeters.Value, 0, 200_000);
    }

    [Fact]
    public async Task EnrichAsync_returns_near_zero_distance_when_parcel_is_on_road_geometry()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);

        var parcel = await PersistParcelAsync(_roadPointLatitude, _roadPointLongitude);

        var result = await _enrichmentService.EnrichAsync(parcel.Id);
        WriteResult(result);

        Assert.Equal(RoadAccessibilityEnrichmentStatus.Available, result.Status);
        Assert.NotNull(result.DistanceMeters);
        Assert.True(result.DistanceMeters.Value <= DistanceToleranceMeters);
    }

    [Fact]
    public async Task EnrichAsync_prefers_boundary_geometry_when_available()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);

        var boundary = CreateSmallBoundaryAround(_hambantotaLatitude, _hambantotaLongitude, delta: 0.002);
        var parcel = await PersistParcelAsync(
            _hambantotaLatitude,
            _hambantotaLongitude,
            boundary);

        var result = await _enrichmentService.EnrichAsync(parcel.Id);

        Assert.Equal(RoadAccessibilityEnrichmentStatus.Available, result.Status);
        Assert.Equal(AdministrativeLocationGeometryBasis.Boundary, result.GeometryBasis);
    }

    [Fact]
    public async Task EnrichAsync_uses_centroid_when_boundary_is_unavailable()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);

        var parcel = await PersistParcelAsync(_hambantotaLatitude, _hambantotaLongitude, boundary: null);

        var result = await _enrichmentService.EnrichAsync(parcel.Id);

        Assert.Equal(AdministrativeLocationGeometryBasis.Centroid, result.GeometryBasis);
    }

    [Fact]
    public async Task EnrichAsync_returns_Unavailable_for_parcel_outside_pilot_coverage()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);

        var parcel = await PersistParcelAsync(OutsideCoverageLatitude, OutsideCoverageLongitude);

        var result = await _enrichmentService.EnrichAsync(parcel.Id);

        Assert.Equal(RoadAccessibilityEnrichmentStatus.Unavailable, result.Status);
        Assert.Null(result.RoadId);
        Assert.Null(result.DistanceMeters);
    }

    [Fact]
    public async Task EnrichAsync_is_deterministic_for_repeated_calls()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);

        var parcel = await PersistParcelAsync(_hambantotaLatitude, _hambantotaLongitude);

        var first = await _enrichmentService.EnrichAsync(parcel.Id);
        var second = await _enrichmentService.EnrichAsync(parcel.Id);

        Assert.Equal(first.RoadId, second.RoadId);
        Assert.Equal(first.RoadName, second.RoadName);
        Assert.Equal(first.DistanceMeters, second.DistanceMeters);
        Assert.Equal(first.GeometryBasis, second.GeometryBasis);
    }

    [Fact]
    public async Task EnrichAsync_returns_gis_source_metadata_and_provenance()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);

        var parcel = await PersistParcelAsync(_hambantotaLatitude, _hambantotaLongitude);

        var result = await _enrichmentService.EnrichAsync(parcel.Id);

        Assert.Equal(GisReferenceDataPaths.SourceName, result.SourceName);
        Assert.False(string.IsNullOrWhiteSpace(result.SourceLayer));
        Assert.Equal(AttributeProvenanceSourceType.ExternalAuthoritative, result.RoadSourceProvenance!.SourceType);
        Assert.Equal(AttributeProvenanceSourceType.Derived, result.DistanceProvenance!.SourceType);
        Assert.Contains("gis_roads", result.Evidence[0], StringComparison.Ordinal);
    }

    private async Task<LandParcel> PersistParcelAsync(
        double latitude,
        double longitude,
        GeoBoundary? boundary = null)
    {
        Assert.NotNull(_repository);

        var cadastralNumber = $"H5-ROAD-{Guid.NewGuid():N}"[..24];
        var parcel = new LandParcel(
            new ParcelIdentifier(cadastralNumber, "H5-ROAD-PLAN"),
            new LandCategory(LandCategoryType.StateLand, "[SYNTHETIC] Road accessibility enrichment test parcel"),
            new LandArea(1m, AreaUnit.Hectares),
            new AdministrativeLocation("Southern Province", "Hambantota", "Hambantota DS"),
            new SpatialReference(latitude, longitude, boundary: boundary),
            null,
            null);

        await _repository.AddAsync(parcel);
        _createdParcelIds.Add(parcel.Id);
        return parcel;
    }

    private static GeoBoundary CreateSmallBoundaryAround(
        double latitude,
        double longitude,
        double delta) =>
        new([
            new GeoCoordinate(latitude - delta, longitude - delta),
            new GeoCoordinate(latitude - delta, longitude + delta),
            new GeoCoordinate(latitude + delta, longitude + delta),
            new GeoCoordinate(latitude + delta, longitude - delta)
        ]);

    private async Task<(double Latitude, double Longitude)> ReadHambantotaInteriorPointAsync()
    {
        Assert.NotNull(_dbContext);

        const string sql = """
            SELECT
                ST_Y(ST_PointOnSurface("Boundary")) AS "Latitude",
                ST_X(ST_PointOnSurface("Boundary")) AS "Longitude"
            FROM land_intelligence.gis_administrative_boundaries
            WHERE "Name" = @districtName
              AND "BoundaryType" = @districtType
            LIMIT 1
            """;

        return await ReadPointAsync(sql, GisReferenceDataPaths.HambantotaDistrictName, districtType: 2);
    }

    private async Task<(double Latitude, double Longitude)> ReadRoadStartPointAsync()
    {
        Assert.NotNull(_dbContext);

        const string sql = """
            SELECT
                ST_Y(ST_StartPoint("Geometry")) AS "Latitude",
                ST_X(ST_StartPoint("Geometry")) AS "Longitude"
            FROM land_intelligence.gis_roads
            WHERE "Geometry" IS NOT NULL
            LIMIT 1
            """;

        return await ReadPointAsync(sql);
    }

    private async Task<(double Latitude, double Longitude)> ReadPointAsync(
        string sql,
        string? districtName = null,
        int? districtType = null)
    {
        Assert.NotNull(_dbContext);

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        if (districtName is not null)
        {
            var districtNameParameter = command.CreateParameter();
            districtNameParameter.ParameterName = "districtName";
            districtNameParameter.Value = districtName;
            command.Parameters.Add(districtNameParameter);

            var districtTypeParameter = command.CreateParameter();
            districtTypeParameter.ParameterName = "districtType";
            districtTypeParameter.Value = districtType!.Value;
            command.Parameters.Add(districtTypeParameter);
        }

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Expected geometry point was not found for integration tests.");
        }

        return (
            reader.GetDouble(reader.GetOrdinal("Latitude")),
            reader.GetDouble(reader.GetOrdinal("Longitude")));
    }

    private void WriteResult(RoadAccessibilityEnrichmentResult result)
    {
        _output.WriteLine(
            $"Status={result.Status}, road={result.RoadName}, type={result.RoadType}, distanceMeters={result.DistanceMeters}, basis={result.GeometryBasis}");
    }

    public async Task DisposeAsync()
    {
        if (_dbContext is not null && _createdParcelIds.Count > 0)
        {
            var parcels = await _dbContext.LandParcels
                .Where(parcel => _createdParcelIds.Contains(parcel.Id))
                .ToListAsync();

            _dbContext.LandParcels.RemoveRange(parcels);
            await _dbContext.SaveChangesAsync();
        }

        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }
    }
}
