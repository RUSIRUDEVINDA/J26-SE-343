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
public sealed class WaterProximityEnrichmentIntegrationTests : IAsyncLifetime
{
    private const double OutsideCoverageLatitude = 6.9271;
    private const double OutsideCoverageLongitude = 79.8612;
    private const int CanalFeatureType = 1;
    private const int LakeFeatureType = 2;
    private const double DistanceToleranceMeters = 25d;

    private readonly ITestOutputHelper _output;
    private ServiceProvider? _serviceProvider;
    private ILandParcelRepository? _repository;
    private IWaterProximityEnrichmentService? _enrichmentService;
    private LandIntelligenceDbContext? _dbContext;
    private double _hambantotaLatitude;
    private double _hambantotaLongitude;
    private WaterFeatureSample _canalSample = null!;
    private WaterFeatureSample _lakeSample = null!;
    private readonly List<Guid> _createdParcelIds = [];

    public WaterProximityEnrichmentIntegrationTests(ITestOutputHelper output)
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
        _enrichmentService = _serviceProvider.GetRequiredService<IWaterProximityEnrichmentService>();
        _dbContext = _serviceProvider.GetRequiredService<LandIntelligenceDbContext>();

        var importService = _serviceProvider.GetRequiredService<IGisReferenceDataImportService>();
        await importService.ImportHambantotaPilotAsync(GisReferenceDataPaths.ResolveDataRoot(null));

        (_hambantotaLatitude, _hambantotaLongitude) = await ReadHambantotaInteriorPointAsync();
        _canalSample = await ReadWaterFeatureSampleAsync(CanalFeatureType);
        _lakeSample = await ReadWaterFeatureSampleAsync(LakeFeatureType);
    }

    [Fact]
    public async Task EnrichAsync_returns_nearest_canal_when_parcel_is_on_canal_geometry()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);

        var parcel = await PersistParcelAsync(_canalSample.Latitude, _canalSample.Longitude);

        var result = await _enrichmentService.EnrichAsync(parcel.Id);
        WriteResult(result);

        Assert.Equal(WaterProximityEnrichmentStatus.Available, result.Status);
        Assert.Equal(GisReferenceWaterFeatureType.Canal, result.FeatureType);
        Assert.Equal(_canalSample.FeatureId, result.FeatureId);
        Assert.NotNull(result.DistanceMeters);
        Assert.True(result.DistanceMeters <= DistanceToleranceMeters);
    }

    [Fact]
    public async Task EnrichAsync_returns_nearest_lake_when_parcel_is_on_lake_geometry()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);

        var parcel = await PersistParcelAsync(_lakeSample.Latitude, _lakeSample.Longitude);

        var result = await _enrichmentService.EnrichAsync(parcel.Id);

        Assert.Equal(WaterProximityEnrichmentStatus.Available, result.Status);
        Assert.Equal(GisReferenceWaterFeatureType.Lake, result.FeatureType);
        Assert.Equal(_lakeSample.FeatureId, result.FeatureId);
        Assert.NotNull(result.DistanceMeters);
        Assert.True(result.DistanceMeters <= DistanceToleranceMeters);
    }

    [Fact]
    public async Task EnrichAsync_selects_correct_nearest_feature_across_mixed_canal_and_lake_types()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);
        Assert.NotNull(_dbContext);

        var expected = await ReadExpectedNearestFeatureAsync(_hambantotaLatitude, _hambantotaLongitude);

        var parcel = await PersistParcelAsync(_hambantotaLatitude, _hambantotaLongitude);

        var result = await _enrichmentService.EnrichAsync(parcel.Id);
        WriteResult(result);

        Assert.Equal(WaterProximityEnrichmentStatus.Available, result.Status);
        Assert.Equal(expected.FeatureId, result.FeatureId);
        Assert.Equal((GisReferenceWaterFeatureType)expected.FeatureType, result.FeatureType);
    }

    [Fact]
    public async Task EnrichAsync_returns_distance_in_meters()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);

        var parcel = await PersistParcelAsync(_hambantotaLatitude, _hambantotaLongitude);

        var result = await _enrichmentService.EnrichAsync(parcel.Id);

        Assert.Equal(WaterProximityEnrichmentStatus.Available, result.Status);
        Assert.NotNull(result.DistanceMeters);
        Assert.InRange(result.DistanceMeters.Value, 0, 200_000);
    }

    [Fact]
    public async Task EnrichAsync_returns_Unavailable_without_zero_distance_when_outside_pilot_coverage()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);

        var parcel = await PersistParcelAsync(OutsideCoverageLatitude, OutsideCoverageLongitude);

        var result = await _enrichmentService.EnrichAsync(parcel.Id);

        Assert.Equal(WaterProximityEnrichmentStatus.Unavailable, result.Status);
        Assert.Null(result.FeatureId);
        Assert.Null(result.DistanceMeters);
    }

    [Fact]
    public async Task EnrichAsync_prefers_boundary_geometry_when_available()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);

        var boundary = CreateSmallBoundaryAround(_hambantotaLatitude, _hambantotaLongitude, delta: 0.002);
        var parcel = await PersistParcelAsync(_hambantotaLatitude, _hambantotaLongitude, boundary);

        var result = await _enrichmentService.EnrichAsync(parcel.Id);

        Assert.Equal(WaterProximityEnrichmentStatus.Available, result.Status);
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
    public async Task EnrichAsync_is_deterministic_for_repeated_calls()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);

        var parcel = await PersistParcelAsync(_hambantotaLatitude, _hambantotaLongitude);

        var first = await _enrichmentService.EnrichAsync(parcel.Id);
        var second = await _enrichmentService.EnrichAsync(parcel.Id);

        Assert.Equal(first.FeatureId, second.FeatureId);
        Assert.Equal(first.FeatureType, second.FeatureType);
        Assert.Equal(first.DistanceMeters, second.DistanceMeters);
    }

    [Fact]
    public async Task EnrichAsync_returns_gis_source_and_provenance()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);

        var parcel = await PersistParcelAsync(_hambantotaLatitude, _hambantotaLongitude);

        var result = await _enrichmentService.EnrichAsync(parcel.Id);

        Assert.Equal(GisReferenceDataPaths.SourceName, result.SourceName);
        Assert.False(string.IsNullOrWhiteSpace(result.SourceLayer));
        Assert.Equal(AttributeProvenanceSourceType.ExternalAuthoritative, result.FeatureSourceProvenance!.SourceType);
        Assert.Equal(AttributeProvenanceSourceType.Derived, result.DistanceProvenance!.SourceType);
    }

    private async Task<LandParcel> PersistParcelAsync(
        double latitude,
        double longitude,
        GeoBoundary? boundary = null)
    {
        Assert.NotNull(_repository);

        var cadastralNumber = $"H6-WATER-{Guid.NewGuid():N}"[..24];
        var parcel = new LandParcel(
            new ParcelIdentifier(cadastralNumber, "H6-WATER-PLAN"),
            new LandCategory(LandCategoryType.StateLand, "[SYNTHETIC] Water proximity enrichment test parcel"),
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

    private async Task<WaterFeatureSample> ReadWaterFeatureSampleAsync(int featureType)
    {
        Assert.NotNull(_dbContext);

        const string sql = """
            SELECT
                "Id" AS "FeatureId",
                "Name" AS "FeatureName",
                "FeatureType" AS "FeatureType",
                ST_Y(ST_PointOnSurface("Geometry")) AS "Latitude",
                ST_X(ST_PointOnSurface("Geometry")) AS "Longitude"
            FROM land_intelligence.gis_water_features
            WHERE "FeatureType" = @featureType
              AND "Geometry" IS NOT NULL
            LIMIT 1
            """;

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var featureTypeParameter = command.CreateParameter();
        featureTypeParameter.ParameterName = "featureType";
        featureTypeParameter.Value = featureType;
        command.Parameters.Add(featureTypeParameter);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException($"Expected water feature type {featureType} was not found.");
        }

        return new WaterFeatureSample(
            reader.GetGuid(reader.GetOrdinal("FeatureId")),
            reader.IsDBNull(reader.GetOrdinal("FeatureName"))
                ? null
                : reader.GetString(reader.GetOrdinal("FeatureName")),
            reader.GetInt32(reader.GetOrdinal("FeatureType")),
            reader.GetDouble(reader.GetOrdinal("Latitude")),
            reader.GetDouble(reader.GetOrdinal("Longitude")));
    }

    private async Task<WaterFeatureSample> ReadExpectedNearestFeatureAsync(double latitude, double longitude)
    {
        Assert.NotNull(_dbContext);

        const string sql = """
            WITH parcel_geom AS (
                SELECT ST_SetSRID(ST_MakePoint(@longitude, @latitude), 4326) AS geom
            )
            SELECT
                w."Id" AS "FeatureId",
                w."Name" AS "FeatureName",
                w."FeatureType" AS "FeatureType",
                ST_Distance(p.geom::geography, w."Geometry"::geography) AS "DistanceMeters"
            FROM parcel_geom p
            INNER JOIN land_intelligence.gis_water_features w
                ON w."Geometry" IS NOT NULL
               AND w."FeatureType" IN (1, 2)
            ORDER BY w."Geometry"::geography <-> p.geom::geography
            LIMIT 1
            """;

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var longitudeParameter = command.CreateParameter();
        longitudeParameter.ParameterName = "longitude";
        longitudeParameter.Value = longitude;
        command.Parameters.Add(longitudeParameter);

        var latitudeParameter = command.CreateParameter();
        latitudeParameter.ParameterName = "latitude";
        latitudeParameter.Value = latitude;
        command.Parameters.Add(latitudeParameter);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Expected nearest water feature was not found.");
        }

        return new WaterFeatureSample(
            reader.GetGuid(reader.GetOrdinal("FeatureId")),
            reader.IsDBNull(reader.GetOrdinal("FeatureName"))
                ? null
                : reader.GetString(reader.GetOrdinal("FeatureName")),
            reader.GetInt32(reader.GetOrdinal("FeatureType")),
            latitude,
            longitude);
    }

    private async Task<(double Latitude, double Longitude)> ReadPointAsync(
        string sql,
        string districtName,
        int districtType)
    {
        Assert.NotNull(_dbContext);

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var districtNameParameter = command.CreateParameter();
        districtNameParameter.ParameterName = "districtName";
        districtNameParameter.Value = districtName;
        command.Parameters.Add(districtNameParameter);

        var districtTypeParameter = command.CreateParameter();
        districtTypeParameter.ParameterName = "districtType";
        districtTypeParameter.Value = districtType;
        command.Parameters.Add(districtTypeParameter);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Expected geometry point was not found for integration tests.");
        }

        return (
            reader.GetDouble(reader.GetOrdinal("Latitude")),
            reader.GetDouble(reader.GetOrdinal("Longitude")));
    }

    private void WriteResult(WaterProximityEnrichmentResult result)
    {
        _output.WriteLine(
            $"Status={result.Status}, feature={result.FeatureName}, type={result.FeatureType}, distanceMeters={result.DistanceMeters}, basis={result.GeometryBasis}");
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

    private sealed record WaterFeatureSample(
        Guid FeatureId,
        string? FeatureName,
        int FeatureType,
        double Latitude,
        double Longitude);
}
