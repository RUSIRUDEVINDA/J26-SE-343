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
public sealed class EnvironmentalSpatialConstraintEnrichmentIntegrationTests : IAsyncLifetime
{
    private const double OutsideCoverageLatitude = 6.9271;
    private const double OutsideCoverageLongitude = 79.8612;

    private readonly ITestOutputHelper _output;
    private ServiceProvider? _serviceProvider;
    private ILandParcelRepository? _repository;
    private IEnvironmentalSpatialConstraintEnrichmentService? _enrichmentService;
    private LandIntelligenceDbContext? _dbContext;
    private ConservationAreaSample _conservationSample = null!;
    private (double Latitude, double Longitude)? _outsideConservationPoint;
    private readonly List<Guid> _createdParcelIds = [];

    public EnvironmentalSpatialConstraintEnrichmentIntegrationTests(ITestOutputHelper output)
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
        _enrichmentService = _serviceProvider.GetRequiredService<IEnvironmentalSpatialConstraintEnrichmentService>();
        _dbContext = _serviceProvider.GetRequiredService<LandIntelligenceDbContext>();

        await LandIntelligenceH8GisTestData.CleanupSyntheticRecordsAsync(_dbContext);

        var importService = _serviceProvider.GetRequiredService<IGisReferenceDataImportService>();
        await importService.ImportHambantotaPilotAsync(GisReferenceDataPaths.ResolveDataRoot(null));

        _conservationSample = await ReadConservationAreaSampleAsync();
    }

    [Fact]
    public async Task EnrichAsync_detects_intersection_when_parcel_is_inside_conservation_area()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);

        var parcel = await PersistParcelAsync(
            _conservationSample.Latitude,
            _conservationSample.Longitude,
            boundary: CreateSmallBoundaryAround(_conservationSample.Latitude, _conservationSample.Longitude, delta: 0.001));

        var result = await _enrichmentService.EnrichAsync(parcel.Id);
        WriteResult(result);

        Assert.Equal(EnvironmentalSpatialConstraintEnrichmentStatus.Available, result.Status);
        Assert.True(result.IntersectsSoilConservationArea);
        Assert.Contains(
            result.ConservationAreas,
            area => area.Id == _conservationSample.ConservationAreaId);
        Assert.NotNull(result.ConservationAreaSourceProvenance);
        Assert.NotNull(result.DerivedConservationProvenance);
    }

    [Fact]
    public async Task EnrichAsync_returns_available_without_conservation_intersection_when_outside_conservation_polygons()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);

        var outsidePoint = await GetPointOutsideConservationAsync();

        var parcel = await PersistParcelAsync(
            outsidePoint.Latitude,
            outsidePoint.Longitude,
            boundary: CreateSmallBoundaryAround(
                outsidePoint.Latitude,
                outsidePoint.Longitude,
                delta: 0.001));

        var result = await _enrichmentService.EnrichAsync(parcel.Id);

        Assert.Equal(EnvironmentalSpatialConstraintEnrichmentStatus.Available, result.Status);
        Assert.False(result.IntersectsSoilConservationArea);
        Assert.Empty(result.ConservationAreas);
    }

    [Fact]
    public async Task EnrichAsync_returns_all_intersecting_conservation_polygons()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);
        Assert.NotNull(_dbContext);

        var parcel = await PersistParcelAsync(
            _conservationSample.Latitude,
            _conservationSample.Longitude,
            boundary: CreateSmallBoundaryAround(_conservationSample.Latitude, _conservationSample.Longitude, delta: 0.02));

        var expectedCount = await CountExpectedConservationOverlapsAsync(
            _conservationSample.Latitude,
            _conservationSample.Longitude,
            delta: 0.02);

        var result = await _enrichmentService.EnrichAsync(parcel.Id);

        Assert.Equal(EnvironmentalSpatialConstraintEnrichmentStatus.Available, result.Status);
        Assert.True(result.IntersectsSoilConservationArea);
        Assert.Equal(expectedCount, result.ConservationAreas.Count);
    }

    [Fact]
    public async Task EnrichAsync_calculates_overlap_percentage_for_boundary_geometry()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);

        const double delta = 0.001;
        var parcel = await PersistParcelAsync(
            _conservationSample.Latitude,
            _conservationSample.Longitude,
            boundary: CreateSmallBoundaryAround(_conservationSample.Latitude, _conservationSample.Longitude, delta));

        var expected = await ReadExpectedPrimaryConservationOverlapAsync(
            _conservationSample.Latitude,
            _conservationSample.Longitude,
            delta);

        var result = await _enrichmentService.EnrichAsync(parcel.Id);

        Assert.Equal(EnvironmentalSpatialConstraintEnrichmentStatus.Available, result.Status);
        Assert.NotEmpty(result.ConservationAreas);
        Assert.Equal(expected.OverlapPercentage, result.ConservationAreas[0].OverlapPercentage);
        Assert.True(result.ConservationAreas[0].OverlapAreaSquareMeters > 0);
    }

    [Fact]
    public async Task EnrichAsync_prefers_boundary_geometry_when_available()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);

        var parcel = await PersistParcelAsync(
            _conservationSample.Latitude,
            _conservationSample.Longitude,
            boundary: CreateSmallBoundaryAround(_conservationSample.Latitude, _conservationSample.Longitude, delta: 0.001));

        var result = await _enrichmentService.EnrichAsync(parcel.Id);

        Assert.Equal(AdministrativeLocationGeometryBasis.Boundary, result.GeometryBasis);
        Assert.NotNull(result.ConservationAreas[0].OverlapPercentage);
    }

    [Fact]
    public async Task EnrichAsync_uses_centroid_when_boundary_unavailable()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);

        var parcel = await PersistParcelAsync(
            _conservationSample.Latitude,
            _conservationSample.Longitude,
            boundary: null);

        var result = await _enrichmentService.EnrichAsync(parcel.Id);

        Assert.Equal(EnvironmentalSpatialConstraintEnrichmentStatus.Available, result.Status);
        Assert.Equal(AdministrativeLocationGeometryBasis.Centroid, result.GeometryBasis);
        Assert.True(result.IntersectsSoilConservationArea);
        Assert.All(result.ConservationAreas, area => Assert.Null(area.OverlapPercentage));
    }

    [Fact]
    public async Task EnrichAsync_returns_Unavailable_when_parcel_geometry_is_missing()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_dbContext);

        var parcel = await PersistParcelAsync(
            _conservationSample.Latitude,
            _conservationSample.Longitude,
            boundary: CreateSmallBoundaryAround(_conservationSample.Latitude, _conservationSample.Longitude, delta: 0.001));

        await ExecuteNonQueryAsync(
            """
            UPDATE land_intelligence.land_parcels
            SET "Centroid" = ST_GeomFromText('POINT EMPTY', 4326),
                "Boundary" = NULL
            WHERE "Id" = @parcelId
            """,
            ("parcelId", parcel.Id));

        var result = await _enrichmentService.EnrichAsync(parcel.Id);

        Assert.Equal(EnvironmentalSpatialConstraintEnrichmentStatus.Unavailable, result.Status);
        Assert.Null(result.GeometryBasis);
    }

    [Fact]
    public async Task EnrichAsync_returns_Unavailable_when_parcel_is_outside_pilot_coverage()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);

        var parcel = await PersistParcelAsync(OutsideCoverageLatitude, OutsideCoverageLongitude);

        var result = await _enrichmentService.EnrichAsync(parcel.Id);

        Assert.Equal(EnvironmentalSpatialConstraintEnrichmentStatus.Unavailable, result.Status);
        Assert.False(result.IntersectsSoilConservationArea);
    }

    [Fact]
    public async Task EnrichAsync_returns_erosion_data_status_unavailable_for_real_hambantota_dataset()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);
        Assert.NotNull(_dbContext);

        var erosionCount = await LandIntelligenceH8GisTestData.ReadPilotErosionObservationCountAsync(_dbContext);
        Assert.Equal(0, erosionCount);

        var parcel = await PersistParcelAsync(
            _conservationSample.Latitude,
            _conservationSample.Longitude,
            boundary: CreateSmallBoundaryAround(_conservationSample.Latitude, _conservationSample.Longitude, delta: 0.001));

        var result = await _enrichmentService.EnrichAsync(parcel.Id);

        Assert.Equal(ErosionDataStatus.Unavailable, result.ErosionDataStatus);
        Assert.Empty(result.ErosionObservations);
        Assert.Null(result.ErosionObservationSourceProvenance);
        Assert.Contains(
            result.Evidence,
            item => item.Contains("must not be interpreted", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EnrichAsync_returns_erosion_available_when_synthetic_pilot_observation_is_present()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);
        Assert.NotNull(_dbContext);

        await LandIntelligenceH8GisTestData.InsertSyntheticErosionObservationAsync(
            _dbContext,
            _conservationSample.Latitude,
            _conservationSample.Longitude);

        var parcel = await PersistParcelAsync(
            _conservationSample.Latitude,
            _conservationSample.Longitude,
            boundary: CreateSmallBoundaryAround(_conservationSample.Latitude, _conservationSample.Longitude, delta: 0.001));

        var result = await _enrichmentService.EnrichAsync(parcel.Id);

        Assert.Equal(ErosionDataStatus.Available, result.ErosionDataStatus);
        Assert.NotEmpty(result.ErosionObservations);
        Assert.NotNull(result.ErosionObservationSourceProvenance);
    }

    [Fact]
    public async Task EnrichAsync_does_not_interpret_missing_erosion_data_as_environmental_safety()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);

        var outsidePoint = await GetPointOutsideConservationAsync();

        var parcel = await PersistParcelAsync(
            outsidePoint.Latitude,
            outsidePoint.Longitude);

        var result = await _enrichmentService.EnrichAsync(parcel.Id);

        Assert.Equal(ErosionDataStatus.Unavailable, result.ErosionDataStatus);
        Assert.DoesNotContain(
            result.Evidence,
            item => item.Contains("low risk", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            result.Evidence,
            item => item.Contains("no erosion", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            result.Evidence,
            item => item.Contains("must not be interpreted", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EnrichAsync_returns_gis_source_and_provenance()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);

        var parcel = await PersistParcelAsync(
            _conservationSample.Latitude,
            _conservationSample.Longitude,
            boundary: CreateSmallBoundaryAround(_conservationSample.Latitude, _conservationSample.Longitude, delta: 0.001));

        var result = await _enrichmentService.EnrichAsync(parcel.Id);

        Assert.Equal(GisReferenceDataPaths.SourceName, result.SourceName);
        Assert.Contains(GisReferenceDataPaths.SoilConservationAreasLayer, result.SourceLayer, StringComparison.Ordinal);
        Assert.Contains(GisReferenceDataPaths.SoilErosionLayer, result.SourceLayer, StringComparison.Ordinal);
        Assert.Equal(
            AttributeProvenanceSourceType.ExternalAuthoritative,
            result.ConservationAreaSourceProvenance!.SourceType);
        Assert.Equal(AttributeProvenanceSourceType.Derived, result.DerivedConservationProvenance!.SourceType);
    }

    [Fact]
    public async Task EnrichAsync_is_deterministic_for_repeated_calls()
    {
        Assert.NotNull(_enrichmentService);
        Assert.NotNull(_repository);

        var parcel = await PersistParcelAsync(
            _conservationSample.Latitude,
            _conservationSample.Longitude,
            boundary: CreateSmallBoundaryAround(_conservationSample.Latitude, _conservationSample.Longitude, delta: 0.001));

        var first = await _enrichmentService.EnrichAsync(parcel.Id);
        var second = await _enrichmentService.EnrichAsync(parcel.Id);

        Assert.Equal(first.Status, second.Status);
        Assert.Equal(first.IntersectsSoilConservationArea, second.IntersectsSoilConservationArea);
        Assert.Equal(first.ErosionDataStatus, second.ErosionDataStatus);
        Assert.Equal(first.ConservationAreas.Count, second.ConservationAreas.Count);
        Assert.Equal(
            first.ConservationAreas.Select(area => area.Id),
            second.ConservationAreas.Select(area => area.Id));
        Assert.Equal(first.Evidence.Count, second.Evidence.Count);
    }

    private async Task<LandParcel> PersistParcelAsync(
        double latitude,
        double longitude,
        GeoBoundary? boundary = null)
    {
        Assert.NotNull(_repository);

        var cadastralNumber = $"H8-ENV-{Guid.NewGuid():N}"[..24];
        var parcel = new LandParcel(
            new ParcelIdentifier(cadastralNumber, "H8-ENV-PLAN"),
            new LandCategory(LandCategoryType.StateLand, "[SYNTHETIC] Environmental enrichment test parcel"),
            new LandArea(1m, AreaUnit.Hectares),
            new AdministrativeLocation("Southern Province", "Hambantota", "Hambantota DS"),
            new SpatialReference(latitude, longitude, boundary: boundary));

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

    private async Task<ConservationAreaSample> ReadConservationAreaSampleAsync()
    {
        Assert.NotNull(_dbContext);

        const string sql = """
            SELECT
                c."Id" AS "ConservationAreaId",
                c."Name" AS "Name",
                ST_Y(ST_PointOnSurface(c."Boundary")) AS "Latitude",
                ST_X(ST_PointOnSurface(c."Boundary")) AS "Longitude"
            FROM land_intelligence.gis_soil_conservation_areas c
            INNER JOIN land_intelligence.gis_administrative_boundaries b
                ON b."Name" = @districtName
               AND b."BoundaryType" = @districtType
               AND ST_Intersects(c."Boundary", b."Boundary")
            WHERE c."Boundary" IS NOT NULL
            LIMIT 1
            """;

        return await ReadConservationSampleAsync(sql);
    }

    private async Task<(double Latitude, double Longitude)> GetPointOutsideConservationAsync()
    {
        if (_outsideConservationPoint is not null)
        {
            return _outsideConservationPoint.Value;
        }

        _outsideConservationPoint = await ReadPointOutsideConservationAsync();
        return _outsideConservationPoint.Value;
    }

    private async Task<(double Latitude, double Longitude)> ReadPointOutsideConservationAsync()
    {
        Assert.NotNull(_dbContext);

        const string sql = """
            WITH district AS (
                SELECT "Boundary" AS geom
                FROM land_intelligence.gis_administrative_boundaries
                WHERE "Name" = @districtName
                  AND "BoundaryType" = @districtType
                LIMIT 1
            ),
            conservation_union AS (
                SELECT ST_Union(c."Boundary") AS geom
                FROM land_intelligence.gis_soil_conservation_areas c
                INNER JOIN district d
                    ON c."Boundary" IS NOT NULL
                   AND ST_Intersects(c."Boundary", d.geom)
            ),
            outside_geom AS (
                SELECT ST_Difference(
                    d.geom,
                    COALESCE(cu.geom, ST_GeomFromText('POLYGON EMPTY', 4326))
                ) AS geom
                FROM district d
                LEFT JOIN conservation_union cu ON TRUE
            )
            SELECT
                ST_Y(ST_PointOnSurface(geom)) AS "Latitude",
                ST_X(ST_PointOnSurface(geom)) AS "Longitude"
            FROM outside_geom
            WHERE geom IS NOT NULL
              AND NOT ST_IsEmpty(geom)
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
            throw new InvalidOperationException(
                "Expected a Hambantota interior point outside conservation areas was not found.");
        }

        return (
            reader.GetDouble(reader.GetOrdinal("Latitude")),
            reader.GetDouble(reader.GetOrdinal("Longitude")));
    }

    private async Task<int> CountExpectedConservationOverlapsAsync(
        double latitude,
        double longitude,
        double delta)
    {
        var boundary = CreateSmallBoundaryAround(latitude, longitude, delta);
        var polygonWkt = ToPolygonWkt(boundary);

        const string sql = """
            WITH parcel_geom AS (
                SELECT ST_SetSRID(ST_GeomFromText(@geometryWkt, 4326), 4326) AS geom
            )
            SELECT COUNT(*)::int AS "Count"
            FROM parcel_geom p
            INNER JOIN land_intelligence.gis_soil_conservation_areas c
                ON c."Boundary" IS NOT NULL
               AND ST_Intersects(p.geom, c."Boundary")
            """;

        return await ReadScalarIntAsync(sql, ("geometryWkt", polygonWkt));
    }

    private async Task<ExpectedConservationOverlap> ReadExpectedPrimaryConservationOverlapAsync(
        double latitude,
        double longitude,
        double delta)
    {
        var boundary = CreateSmallBoundaryAround(latitude, longitude, delta);
        var polygonWkt = ToPolygonWkt(boundary);

        const string sql = """
            WITH parcel_geom AS (
                SELECT ST_SetSRID(ST_GeomFromText(@geometryWkt, 4326), 4326) AS geom
            ),
            parcel_area AS (
                SELECT ST_Area(geom::geography) AS area_m2 FROM parcel_geom
            ),
            intersections AS (
                SELECT
                    c."Id" AS "ConservationAreaId",
                    ST_Area(ST_Intersection(p.geom, c."Boundary")::geography) AS overlap_area_m2
                FROM parcel_geom p
                INNER JOIN land_intelligence.gis_soil_conservation_areas c
                    ON c."Boundary" IS NOT NULL
                   AND ST_Intersects(p.geom, c."Boundary")
            )
            SELECT
                i."ConservationAreaId",
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

        Assert.NotNull(_dbContext);

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
            throw new InvalidOperationException("Expected primary conservation overlap was not found.");
        }

        return new ExpectedConservationOverlap(
            reader.GetGuid(reader.GetOrdinal("ConservationAreaId")),
            Convert.ToDecimal(reader.GetDouble(reader.GetOrdinal("OverlapPercentage"))));
    }

    private async Task<ConservationAreaSample> ReadConservationSampleAsync(string sql)
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
            throw new InvalidOperationException("Expected conservation area was not found for integration tests.");
        }

        return new ConservationAreaSample(
            reader.GetGuid(reader.GetOrdinal("ConservationAreaId")),
            reader.GetString(reader.GetOrdinal("Name")),
            reader.GetDouble(reader.GetOrdinal("Latitude")),
            reader.GetDouble(reader.GetOrdinal("Longitude")));
    }

    private async Task<int> ReadScalarIntAsync(
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

        var scalar = await command.ExecuteScalarAsync();
        return Convert.ToInt32(scalar);
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

    private static string ToPolygonWkt(GeoBoundary boundary)
    {
        var ring = boundary.ExteriorRing
            .Select(coordinate => $"{coordinate.Longitude} {coordinate.Latitude}")
            .ToList();
        ring.Add(ring[0]);
        return $"POLYGON(({string.Join(", ", ring)}))";
    }

    private void WriteResult(EnvironmentalSpatialConstraintEnrichmentResult result)
    {
        _output.WriteLine(
            $"Status={result.Status}, intersects={result.IntersectsSoilConservationArea}, " +
            $"conservationAreas={result.ConservationAreas.Count}, erosion={result.ErosionDataStatus}, " +
            $"basis={result.GeometryBasis}");
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

    private sealed record ConservationAreaSample(
        Guid ConservationAreaId,
        string Name,
        double Latitude,
        double Longitude);

    private sealed record ExpectedConservationOverlap(
        Guid ConservationAreaId,
        decimal OverlapPercentage);
}
