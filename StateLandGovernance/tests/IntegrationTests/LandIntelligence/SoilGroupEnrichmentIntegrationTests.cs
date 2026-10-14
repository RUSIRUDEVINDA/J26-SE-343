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
public sealed class SoilGroupEnrichmentIntegrationTests : IAsyncLifetime
{
    private const double OutsideCoverageLatitude = 6.9271;
    private const double OutsideCoverageLongitude = 79.8612;

    private readonly ITestOutputHelper _output;
    private ServiceProvider? _serviceProvider;
    private ILandParcelRepository? _repository;
    private ISoilGroupEnrichmentService? _enrichmentService;
    private LandIntelligenceDbContext? _dbContext;
    private double _hambantotaLatitude;
    private double _hambantotaLongitude;
    private SoilGroupSample _soilSample = null!;
    private readonly List<Guid> _createdParcelIds = [];

    public SoilGroupEnrichmentIntegrationTests(ITestOutputHelper output)
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
        _enrichmentService = _serviceProvider.GetRequiredService<ISoilGroupEnrichmentService>();
        _dbContext = _serviceProvider.GetRequiredService<LandIntelligenceDbContext>();

        var importService = _serviceProvider.GetRequiredService<IGisReferenceDataImportService>();
        await importService.ImportHambantotaPilotAsync(GisReferenceDataPaths.ResolveDataRoot(null));

        (_hambantotaLatitude, _hambantotaLongitude) = await ReadHambantotaInteriorPointAsync();
        _soilSample = await ReadSoilGroupSampleAsync();
    }

    [Fact]
    public async Task EnrichAsync_returns_primary_soil_group_for_parcel_inside_single_soil_polygon()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);

        var parcel = await PersistParcelAsync(
            _soilSample.Latitude,
            _soilSample.Longitude,
            boundary: CreateSmallBoundaryAround(_soilSample.Latitude, _soilSample.Longitude, delta: 0.001));

        var result = await _enrichmentService.EnrichAsync(parcel.Id);
        WriteResult(result);

        Assert.Equal(SoilGroupEnrichmentStatus.Available, result.Status);
        Assert.Equal(_soilSample.SoilGroupId, result.PrimarySoilGroupId);
        Assert.Equal(_soilSample.SoilGroupName, result.PrimarySoilGroup);
        Assert.NotNull(result.OverlapPercentage);
        Assert.True(result.OverlapPercentage > 0);
        Assert.Equal(AdministrativeLocationGeometryBasis.Boundary, result.GeometryBasis);
    }

    [Fact]
    public async Task EnrichAsync_selects_dominant_overlap_when_multiple_soil_polygons_intersect()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);
        Assert.NotNull(_dbContext);

        var parcel = await PersistParcelAsync(
            _soilSample.Latitude,
            _soilSample.Longitude,
            boundary: CreateSmallBoundaryAround(_soilSample.Latitude, _soilSample.Longitude, delta: 0.008));

        var expectedPrimary = await ReadExpectedPrimaryOverlapAsync(
            _soilSample.Latitude,
            _soilSample.Longitude,
            delta: 0.008);

        var result = await _enrichmentService.EnrichAsync(parcel.Id);
        WriteResult(result);

        Assert.Equal(SoilGroupEnrichmentStatus.Available, result.Status);
        Assert.True(result.Overlaps.Count >= 1);
        Assert.Equal(expectedPrimary.SoilGroupId, result.PrimarySoilGroupId);
        Assert.Equal(expectedPrimary.SoilGroupName, result.PrimarySoilGroup);
        Assert.Equal(expectedPrimary.OverlapPercentage, result.OverlapPercentage);
    }

    [Fact]
    public async Task EnrichAsync_uses_centroid_point_in_polygon_when_boundary_unavailable()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);

        var parcel = await PersistParcelAsync(_soilSample.Latitude, _soilSample.Longitude, boundary: null);

        var result = await _enrichmentService.EnrichAsync(parcel.Id);

        Assert.Equal(SoilGroupEnrichmentStatus.Available, result.Status);
        Assert.Equal(AdministrativeLocationGeometryBasis.Centroid, result.GeometryBasis);
        Assert.Null(result.OverlapPercentage);
        Assert.NotNull(result.PrimarySoilGroup);
    }

    [Fact]
    public async Task EnrichAsync_returns_Unavailable_when_parcel_has_no_soil_coverage()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);

        var parcel = await PersistParcelAsync(OutsideCoverageLatitude, OutsideCoverageLongitude);

        var result = await _enrichmentService.EnrichAsync(parcel.Id);

        Assert.Equal(SoilGroupEnrichmentStatus.Unavailable, result.Status);
        Assert.Null(result.PrimarySoilGroup);
        Assert.Null(result.OverlapPercentage);
    }

    [Fact]
    public async Task EnrichAsync_preserves_official_stored_soil_type_without_persisting_gis_override()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);

        const string officialSoilType = "Official Survey Loam";
        var parcel = await PersistParcelAsync(
            _soilSample.Latitude,
            _soilSample.Longitude,
            boundary: null,
            soilType: officialSoilType,
            soilTypeProvenance: AttributeProvenance.Official("Survey Department"));

        var result = await _enrichmentService.EnrichAsync(parcel.Id);

        Assert.Equal(SoilGroupEnrichmentStatus.Available, result.Status);
        Assert.True(result.OfficialSoilTypePreserved);
        Assert.Equal(officialSoilType, result.StoredSoilType);
        Assert.Equal(AttributeProvenanceSourceType.Official, result.StoredSoilTypeProvenance!.SourceType);
        Assert.NotNull(result.PrimarySoilGroup);

        var reloaded = await _repository!.GetByIdAsync(parcel.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(officialSoilType, reloaded!.Characteristics?.SoilType);
        Assert.Equal(AttributeProvenanceSourceType.Official, reloaded.Characteristics?.SoilTypeProvenance?.SourceType);
    }

    [Fact]
    public async Task EnrichAsync_returns_gis_source_and_provenance()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);

        var parcel = await PersistParcelAsync(_soilSample.Latitude, _soilSample.Longitude);

        var result = await _enrichmentService.EnrichAsync(parcel.Id);

        Assert.Equal(GisReferenceDataPaths.SourceName, result.SourceName);
        Assert.False(string.IsNullOrWhiteSpace(result.SourceLayer));
        Assert.Equal(AttributeProvenanceSourceType.ExternalAuthoritative, result.SoilGroupSourceProvenance!.SourceType);
        Assert.Equal(AttributeProvenanceSourceType.Derived, result.DerivedSoilGroupProvenance!.SourceType);
    }

    private async Task<LandParcel> PersistParcelAsync(
        double latitude,
        double longitude,
        GeoBoundary? boundary = null,
        string? soilType = null,
        AttributeProvenance? soilTypeProvenance = null)
    {
        Assert.NotNull(_repository);

        var cadastralNumber = $"H7-SOIL-{Guid.NewGuid():N}"[..24];
        LandCharacteristics? characteristics = soilType is null && soilTypeProvenance is null
            ? null
            : new LandCharacteristics(
                soilType,
                terrainDescription: null,
                elevationMeters: null,
                soilTypeProvenance: soilTypeProvenance);

        var parcel = new LandParcel(
            new ParcelIdentifier(cadastralNumber, "H7-SOIL-PLAN"),
            new LandCategory(LandCategoryType.StateLand, "[SYNTHETIC] Soil group enrichment test parcel"),
            new LandArea(1m, AreaUnit.Hectares),
            new AdministrativeLocation("Southern Province", "Hambantota", "Hambantota DS"),
            new SpatialReference(latitude, longitude, boundary: boundary),
            currentUse: null,
            characteristics);

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
            throw new InvalidOperationException("Expected soil group was not found for integration tests.");
        }

        return new SoilGroupSample(
            reader.GetGuid(reader.GetOrdinal("SoilGroupId")),
            reader.GetString(reader.GetOrdinal("SoilGroupName")),
            reader.GetDouble(reader.GetOrdinal("Latitude")),
            reader.GetDouble(reader.GetOrdinal("Longitude")));
    }

    private async Task<ExpectedPrimaryOverlap> ReadExpectedPrimaryOverlapAsync(
        double latitude,
        double longitude,
        double delta)
    {
        Assert.NotNull(_dbContext);

        var boundary = CreateSmallBoundaryAround(latitude, longitude, delta);
        var ring = boundary.ExteriorRing
            .Select(coordinate => $"{coordinate.Longitude} {coordinate.Latitude}")
            .ToList();
        ring.Add(ring[0]);
        var polygonWkt = $"POLYGON(({string.Join(", ", ring)}))";

        const string sql = """
            WITH parcel_geom AS (
                SELECT ST_SetSRID(ST_GeomFromText(@geometryWkt, 4326), 4326) AS geom
            ),
            parcel_area AS (
                SELECT ST_Area(geom::geography) AS area_m2 FROM parcel_geom
            ),
            intersections AS (
                SELECT
                    s."Id" AS "SoilGroupId",
                    s."Name" AS "SoilGroupName",
                    ST_Area(ST_Intersection(p.geom, s."Boundary")::geography) AS overlap_area_m2
                FROM parcel_geom p
                INNER JOIN land_intelligence.gis_soil_groups s
                    ON s."Boundary" IS NOT NULL
                   AND ST_Intersects(p.geom, s."Boundary")
            )
            SELECT
                i."SoilGroupId",
                i."SoilGroupName",
                CASE
                    WHEN pa.area_m2 > 0 THEN (i.overlap_area_m2 / pa.area_m2) * 100
                    ELSE 0
                END AS "OverlapPercentage"
            FROM intersections i
            CROSS JOIN parcel_area pa
            WHERE i.overlap_area_m2 > 0
            ORDER BY i.overlap_area_m2 DESC
            LIMIT 1
            """;

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var geometryParameter = command.CreateParameter();
        geometryParameter.ParameterName = "geometryWkt";
        geometryParameter.Value = polygonWkt;
        command.Parameters.Add(geometryParameter);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Expected primary soil overlap was not found.");
        }

        return new ExpectedPrimaryOverlap(
            reader.GetGuid(reader.GetOrdinal("SoilGroupId")),
            reader.GetString(reader.GetOrdinal("SoilGroupName")),
            Convert.ToDecimal(reader.GetDouble(reader.GetOrdinal("OverlapPercentage"))));
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

    private void WriteResult(SoilGroupEnrichmentResult result)
    {
        _output.WriteLine(
            $"Status={result.Status}, primary={result.PrimarySoilGroup}, overlap={result.OverlapPercentage}, overlaps={result.Overlaps.Count}, basis={result.GeometryBasis}");
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

    private sealed record SoilGroupSample(
        Guid SoilGroupId,
        string SoilGroupName,
        double Latitude,
        double Longitude);

    private sealed record ExpectedPrimaryOverlap(
        Guid SoilGroupId,
        string SoilGroupName,
        decimal OverlapPercentage);
}
