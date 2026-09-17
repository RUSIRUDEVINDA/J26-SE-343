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
public sealed class LandParcelGisEnrichmentIntegrationTests : IAsyncLifetime
{
    private const double OutsideCoverageLatitude = 6.9271;
    private const double OutsideCoverageLongitude = 79.8612;
    private const double DistanceToleranceMeters = 5d;

    private readonly ITestOutputHelper _output;
    private ServiceProvider? _serviceProvider;
    private ILandParcelRepository? _repository;
    private ILandParcelGisEnrichmentService? _enrichmentService;
    private LandIntelligenceDbContext? _dbContext;
    private double _hambantotaLatitude;
    private double _hambantotaLongitude;
    private SoilGroupSample _soilSample = null!;
    private readonly List<Guid> _createdParcelIds = [];

    public LandParcelGisEnrichmentIntegrationTests(ITestOutputHelper output)
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
        _enrichmentService = _serviceProvider.GetRequiredService<ILandParcelGisEnrichmentService>();
        _dbContext = _serviceProvider.GetRequiredService<LandIntelligenceDbContext>();

        await LandIntelligenceH8GisTestData.CleanupSyntheticRecordsAsync(_dbContext);

        var importService = _serviceProvider.GetRequiredService<IGisReferenceDataImportService>();
        await importService.ImportHambantotaPilotAsync(GisReferenceDataPaths.ResolveDataRoot(null));

        (_hambantotaLatitude, _hambantotaLongitude) = await ReadHambantotaInteriorPointAsync();
        _soilSample = await ReadSoilGroupSampleAsync();
    }

    [Fact]
    public async Task EnrichAsync_returns_unified_results_for_complete_hambantota_parcel()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);

        var parcel = await PersistParcelAsync(
            _soilSample.Latitude,
            _soilSample.Longitude,
            boundary: CreateSmallBoundaryAround(_soilSample.Latitude, _soilSample.Longitude, delta: 0.001));

        var result = await _enrichmentService.EnrichAsync(parcel.Id);
        WriteResult(result);

        Assert.Equal(parcel.Identifier.CadastralNumber, result.CadastralNumber);
        Assert.NotNull(result.Administrative);
        Assert.NotNull(result.RoadAccessibility);
        Assert.NotNull(result.WaterProximity);
        Assert.NotNull(result.Soil);
        Assert.NotNull(result.Environmental);

        Assert.Equal(AdministrativeLocationVerificationStatus.Verified, result.Administrative.Status);
        Assert.Equal("Hambantota", result.Administrative.DetectedDistrict);
        Assert.Equal("Southern", result.Administrative.DetectedProvince);

        Assert.Equal(RoadAccessibilityEnrichmentStatus.Available, result.RoadAccessibility.Status);
        Assert.Equal(WaterProximityEnrichmentStatus.Available, result.WaterProximity.Status);
        Assert.Equal(SoilGroupEnrichmentStatus.Available, result.Soil.Status);
        Assert.Equal(
            EnvironmentalSpatialConstraintEnrichmentStatus.Available,
            result.Environmental.Status);
        Assert.Equal(ErosionDataStatus.Unavailable, result.Environmental.ErosionDataStatus);

        Assert.Equal(LandParcelGisEnrichmentOverallStatus.Partial, result.OverallStatus);
        Assert.Contains(
            result.Warnings,
            warning => warning.Contains("Soil erosion GIS observations are unavailable", StringComparison.Ordinal));
        Assert.NotEmpty(result.Evidence);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public async Task EnrichAsync_returns_Partial_when_erosion_dataset_is_missing_but_other_sections_succeed()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);
        Assert.NotNull(_dbContext);

        var erosionCount = await LandIntelligenceH8GisTestData.ReadPilotErosionObservationCountAsync(_dbContext);
        Assert.Equal(0, erosionCount);

        var parcel = await PersistParcelAsync(_hambantotaLatitude, _hambantotaLongitude);

        var result = await _enrichmentService.EnrichAsync(parcel.Id);

        Assert.Equal(LandParcelGisEnrichmentOverallStatus.Partial, result.OverallStatus);
        Assert.Equal(AdministrativeLocationVerificationStatus.Verified, result.Administrative!.Status);
        Assert.Equal(RoadAccessibilityEnrichmentStatus.Available, result.RoadAccessibility!.Status);
        Assert.Equal(WaterProximityEnrichmentStatus.Available, result.WaterProximity!.Status);
        Assert.Equal(ErosionDataStatus.Unavailable, result.Environmental!.ErosionDataStatus);
        Assert.DoesNotContain(
            result.Evidence,
            item => item.Contains("no erosion risk", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EnrichAsync_returns_Unavailable_for_parcel_outside_imported_gis_coverage()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);

        var parcel = await PersistParcelAsync(OutsideCoverageLatitude, OutsideCoverageLongitude);

        var result = await _enrichmentService.EnrichAsync(parcel.Id);

        Assert.Equal(LandParcelGisEnrichmentOverallStatus.Unavailable, result.OverallStatus);
        Assert.Equal(AdministrativeLocationVerificationStatus.Unavailable, result.Administrative!.Status);
        Assert.Equal(RoadAccessibilityEnrichmentStatus.Unavailable, result.RoadAccessibility!.Status);
        Assert.Equal(WaterProximityEnrichmentStatus.Unavailable, result.WaterProximity!.Status);
        Assert.Null(result.RoadAccessibility.RoadId);
        Assert.Null(result.WaterProximity.FeatureId);
    }

    [Fact]
    public async Task EnrichAsync_returns_Unavailable_when_parcel_geometry_is_missing()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_dbContext);

        var parcel = await PersistParcelAsync(
            _hambantotaLatitude,
            _hambantotaLongitude,
            boundary: CreateSmallBoundaryAround(_hambantotaLatitude, _hambantotaLongitude, delta: 0.001));

        await ExecuteNonQueryAsync(
            """
            UPDATE land_intelligence.land_parcels
            SET "Centroid" = ST_GeomFromText('POINT EMPTY', 4326),
                "Boundary" = NULL
            WHERE "Id" = @parcelId
            """,
            ("parcelId", parcel.Id));

        var result = await _enrichmentService.EnrichAsync(parcel.Id);

        Assert.Equal(LandParcelGisEnrichmentOverallStatus.Unavailable, result.OverallStatus);
        Assert.Null(result.GeometryBasis);
        Assert.Equal(AdministrativeLocationVerificationStatus.Unavailable, result.Administrative!.Status);
    }

    [Fact]
    public async Task EnrichAsync_is_deterministic_for_repeated_calls()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);

        var parcel = await PersistParcelAsync(
            _soilSample.Latitude,
            _soilSample.Longitude,
            boundary: CreateSmallBoundaryAround(_soilSample.Latitude, _soilSample.Longitude, delta: 0.001));

        var first = await _enrichmentService.EnrichAsync(parcel.Id);
        var second = await _enrichmentService.EnrichAsync(parcel.Id);

        Assert.Equal(first.OverallStatus, second.OverallStatus);
        Assert.Equal(first.Administrative!.Status, second.Administrative!.Status);
        Assert.Equal(first.RoadAccessibility!.RoadId, second.RoadAccessibility!.RoadId);
        AssertDistanceMeters(first.RoadAccessibility.DistanceMeters, second.RoadAccessibility.DistanceMeters);
        Assert.Equal(first.WaterProximity!.FeatureId, second.WaterProximity!.FeatureId);
        Assert.Equal(first.Soil!.PrimarySoilGroupId, second.Soil!.PrimarySoilGroupId);
        Assert.Equal(
            first.Environmental!.IntersectsSoilConservationArea,
            second.Environmental!.IntersectsSoilConservationArea);
        Assert.Equal(first.Environmental.ErosionDataStatus, second.Environmental.ErosionDataStatus);
    }

    [Fact]
    public async Task EnrichAsync_does_not_modify_official_parcel_values()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);

        const string officialSoilType = "Official Survey Loam";
        var parcel = await PersistParcelAsync(
            _soilSample.Latitude,
            _soilSample.Longitude,
            boundary: null,
            soilType: officialSoilType);

        await _enrichmentService.EnrichAsync(parcel.Id);

        var reloaded = await _repository.GetByIdAsync(parcel.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("Southern Province", reloaded!.Location.Province);
        Assert.Equal("Hambantota", reloaded.Location.District);
        Assert.Equal(officialSoilType, reloaded.Characteristics?.SoilType);
    }

    private async Task<LandParcel> PersistParcelAsync(
        double latitude,
        double longitude,
        GeoBoundary? boundary = null,
        string? soilType = null)
    {
        Assert.NotNull(_repository);

        var cadastralNumber = $"H9-UNIFIED-{Guid.NewGuid():N}"[..24];
        LandCharacteristics? characteristics = soilType is null
            ? null
            : new LandCharacteristics(soilType, terrainDescription: null, elevationMeters: null);

        var parcel = new LandParcel(
            new ParcelIdentifier(cadastralNumber, "H9-UNIFIED-PLAN"),
            new LandCategory(LandCategoryType.StateLand, "[SYNTHETIC] Unified GIS enrichment test parcel"),
            new LandArea(1m, AreaUnit.Hectares),
            new AdministrativeLocation("Southern Province", "Hambantota", "Hambantota DS"),
            new SpatialReference(latitude, longitude, boundary: boundary),
            characteristics: characteristics);

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

        return await ReadPointAsync(sql);
    }

    private async Task<SoilGroupSample> ReadSoilGroupSampleAsync()
    {
        Assert.NotNull(_dbContext);

        const string sql = """
            SELECT
                s."Id" AS "SoilGroupId",
                s."Name" AS "SoilGroupName",
                ST_Y(ST_PointOnSurface(ST_Intersection(s."Boundary", b."Boundary"))) AS "Latitude",
                ST_X(ST_PointOnSurface(ST_Intersection(s."Boundary", b."Boundary"))) AS "Longitude"
            FROM land_intelligence.gis_soil_groups s
            INNER JOIN land_intelligence.gis_administrative_boundaries b
                ON b."Name" = @districtName
               AND b."BoundaryType" = @districtType
               AND ST_Intersects(s."Boundary", b."Boundary")
            WHERE s."Boundary" IS NOT NULL
            LIMIT 1
            """;

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var districtNameParameter = command.CreateParameter();
        districtNameParameter.ParameterName = "districtName";
        districtNameParameter.Value = GisReferenceDataPaths.HambantotaDistrictName;
        command.Parameters.Add(districtNameParameter);

        var districtTypeParameter = command.CreateParameter();
        districtTypeParameter.ParameterName = "districtType";
        districtTypeParameter.Value = 2;
        command.Parameters.Add(districtTypeParameter);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Expected soil group was not found for unified enrichment tests.");
        }

        return new SoilGroupSample(
            reader.GetGuid(reader.GetOrdinal("SoilGroupId")),
            reader.GetString(reader.GetOrdinal("SoilGroupName")),
            reader.GetDouble(reader.GetOrdinal("Latitude")),
            reader.GetDouble(reader.GetOrdinal("Longitude")));
    }

    private async Task<(double Latitude, double Longitude)> ReadPointAsync(string sql)
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
        districtNameParameter.Value = GisReferenceDataPaths.HambantotaDistrictName;
        command.Parameters.Add(districtNameParameter);

        var districtTypeParameter = command.CreateParameter();
        districtTypeParameter.ParameterName = "districtType";
        districtTypeParameter.Value = 2;
        command.Parameters.Add(districtTypeParameter);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Expected geometry point was not found for unified enrichment tests.");
        }

        return (
            reader.GetDouble(reader.GetOrdinal("Latitude")),
            reader.GetDouble(reader.GetOrdinal("Longitude")));
    }

    private async Task ExecuteNonQueryAsync(
        string sql,
        params (string Name, object Value)[] parameters)
    {
        Assert.NotNull(_dbContext);

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        await command.ExecuteNonQueryAsync();
    }

    private static void AssertDistanceMeters(double? actual, double? expected)
    {
        Assert.NotNull(actual);
        Assert.NotNull(expected);
        Assert.True(Math.Abs(actual.Value - expected.Value) <= DistanceToleranceMeters);
    }

    private void WriteResult(LandParcelGisEnrichmentResult result)
    {
        _output.WriteLine(
            $"Overall={result.OverallStatus}, admin={result.Administrative?.Status}, " +
            $"road={result.RoadAccessibility?.Status}, water={result.WaterProximity?.Status}, " +
            $"soil={result.Soil?.Status}, environmental={result.Environmental?.Status}, " +
            $"erosion={result.Environmental?.ErosionDataStatus}");
    }

    public async Task DisposeAsync()
    {
        if (_dbContext is not null)
        {
            if (_createdParcelIds.Count > 0)
            {
                var parcels = await _dbContext.LandParcels
                    .Where(parcel => _createdParcelIds.Contains(parcel.Id))
                    .ToListAsync();

                _dbContext.LandParcels.RemoveRange(parcels);
                await _dbContext.SaveChangesAsync();
            }

            await LandIntelligenceH8GisTestData.CleanupSyntheticRecordsAsync(_dbContext);
        }

        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }
    }

    private sealed record SoilGroupSample(
        Guid SoilGroupId,
        string SoilGroupName,
        double Latitude,
        double Longitude);
}
