using System.Data;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.Mappings;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enrichment;

public sealed class RoadAccessibilityEnrichmentService : IRoadAccessibilityEnrichmentService
{
    private const int DistrictBoundaryType = 2;
    private const string DerivedDistanceSource = "GIS road nearest-neighbor spatial query";

    private readonly LandIntelligenceDbContext _dbContext;

    public RoadAccessibilityEnrichmentService(LandIntelligenceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RoadAccessibilityEnrichmentResult> EnrichAsync(
        Guid parcelId,
        CancellationToken cancellationToken = default)
    {
        var parcel = await _dbContext.LandParcels
            .AsNoTracking()
            .Where(entity => entity.Id == parcelId)
            .Select(entity => new ParcelRoadEnrichmentSnapshot(entity.Id, entity.Centroid, entity.Boundary))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Land parcel '{parcelId}' was not found.");

        var measuredAt = DateTimeOffset.UtcNow;
        var evidence = new List<string>
        {
            $"Road accessibility evidence sourced exclusively from {QualifiedRoadsTable()} (GIS reference roads)."
        };

        var geometrySelection = ParcelEnrichmentGeometrySelector.Select(parcel.Boundary, parcel.Centroid);
        if (geometrySelection is null)
        {
            evidence.Add("Parcel geometry unavailable: no boundary or centroid could be used for road distance.");

            return BuildUnavailableResult(
                parcel.Id,
                evidence,
                geometryBasis: null,
                sourceName: GisReferenceDataPaths.SourceName,
                sourceLayer: GisReferenceDataPaths.ExpresswaysLayer);
        }

        var (parcelGeometry, geometryBasis) = geometrySelection.Value;
        evidence.Add(
            geometryBasis == AdministrativeLocationGeometryBasis.Boundary
                ? "Distance geometry basis: parcel boundary (preferred)."
                : "Distance geometry basis: parcel centroid (boundary unavailable).");

        var withinPilotCoverage = await IsWithinHambantotaPilotCoverageAsync(
            parcelGeometry,
            geometryBasis,
            cancellationToken);

        if (!withinPilotCoverage)
        {
            evidence.Add(
                "Parcel is outside the imported Hambantota GIS pilot coverage; nearest-road distance was not calculated.");

            return BuildUnavailableResult(
                parcel.Id,
                evidence,
                geometryBasis,
                sourceName: GisReferenceDataPaths.SourceName,
                sourceLayer: GisReferenceDataPaths.ExpresswaysLayer);
        }

        var nearestRoad = await FindNearestRoadAsync(parcelGeometry, cancellationToken);
        if (nearestRoad is null)
        {
            evidence.Add("No GIS road geometry was available within the imported pilot dataset.");

            return BuildUnavailableResult(
                parcel.Id,
                evidence,
                geometryBasis,
                sourceName: GisReferenceDataPaths.SourceName,
                sourceLayer: GisReferenceDataPaths.ExpresswaysLayer);
        }

        var roadSourceProvenance = new AttributeProvenanceDto(
            AttributeProvenanceSourceType.ExternalAuthoritative,
            nearestRoad.SourceName,
            Confidence: 1m,
            CollectedAt: measuredAt,
            Verified: true);

        var distanceProvenance = AttributeProvenanceMapper.ToDto(
            AttributeProvenance.Derived(DerivedDistanceSource, confidence: 1m, collectedAt: measuredAt));

        evidence.Add(
            $"Nearest road '{nearestRoad.RoadName ?? "(unnamed)"}' ({nearestRoad.RoadType}) selected from layer '{nearestRoad.SourceLayer}'.");
        evidence.Add(
            $"Geographic distance calculated with ST_Distance(parcel geometry::geography, road geometry::geography) = {nearestRoad.DistanceMeters:F2} m.");
        evidence.Add($"Road accessibility enrichment status: {RoadAccessibilityEnrichmentStatus.Available}.");

        return new RoadAccessibilityEnrichmentResult
        {
            ParcelId = parcel.Id,
            RoadId = nearestRoad.RoadId,
            RoadName = nearestRoad.RoadName,
            RoadType = MapRoadType(nearestRoad.RoadType),
            DistanceMeters = nearestRoad.DistanceMeters,
            GeometryBasis = geometryBasis,
            Status = RoadAccessibilityEnrichmentStatus.Available,
            Evidence = evidence,
            SourceName = nearestRoad.SourceName,
            SourceLayer = nearestRoad.SourceLayer,
            RoadSourceProvenance = roadSourceProvenance,
            DistanceProvenance = distanceProvenance
        };
    }

    internal static string BuildNearestRoadSql() =>
        $"""
         WITH parcel_geom AS (
             SELECT ST_SetSRID(ST_GeomFromText(@geometryWkt, 4326), 4326) AS geom
         )
         SELECT
             r."Id" AS "RoadId",
             r."Name" AS "RoadName",
             r."RoadType" AS "RoadType",
             r."SourceName" AS "SourceName",
             r."SourceLayer" AS "SourceLayer",
             ST_Distance(p.geom::geography, r."Geometry"::geography) AS "DistanceMeters"
         FROM parcel_geom p
         INNER JOIN {QualifiedRoadsTable()} r ON r."Geometry" IS NOT NULL
         ORDER BY r."Geometry"::geography <-> p.geom::geography
         LIMIT 1
         """;

    private async Task<bool> IsWithinHambantotaPilotCoverageAsync(
        Geometry parcelGeometry,
        AdministrativeLocationGeometryBasis geometryBasis,
        CancellationToken cancellationToken)
    {
        var geometryWkt = parcelGeometry.AsText();
        var spatialPredicate = geometryBasis == AdministrativeLocationGeometryBasis.Centroid
            ? "ST_Covers(b.\"Boundary\", ST_SetSRID(ST_GeomFromText(@geometryWkt, 4326), 4326))"
            : "ST_Intersects(b.\"Boundary\", ST_SetSRID(ST_GeomFromText(@geometryWkt, 4326), 4326))";

        var sql = $"""
                   SELECT EXISTS (
                       SELECT 1
                       FROM {QualifiedAdministrativeBoundariesTable()} b
                       WHERE b."Name" = @districtName
                         AND b."BoundaryType" = @districtType
                         AND {spatialPredicate}
                   )
                   """;

        var result = await ExecuteScalarAsync(
            sql,
            cancellationToken,
            ("districtName", GisReferenceDataPaths.HambantotaDistrictName),
            ("districtType", DistrictBoundaryType),
            ("geometryWkt", geometryWkt));

        return result is bool withinCoverage && withinCoverage;
    }

    private async Task<NearestRoadRow?> FindNearestRoadAsync(
        Geometry parcelGeometry,
        CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = BuildNearestRoadSql();
        AddParameter(command, "geometryWkt", parcelGeometry.AsText());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new NearestRoadRow(
            reader.GetGuid(reader.GetOrdinal("RoadId")),
            reader.IsDBNull(reader.GetOrdinal("RoadName"))
                ? null
                : reader.GetString(reader.GetOrdinal("RoadName")),
            reader.GetInt32(reader.GetOrdinal("RoadType")),
            reader.GetString(reader.GetOrdinal("SourceName")),
            reader.GetString(reader.GetOrdinal("SourceLayer")),
            reader.GetDouble(reader.GetOrdinal("DistanceMeters")));
    }

    private static RoadAccessibilityEnrichmentResult BuildUnavailableResult(
        Guid parcelId,
        IReadOnlyList<string> evidence,
        AdministrativeLocationGeometryBasis? geometryBasis,
        string sourceName,
        string sourceLayer) =>
        new()
        {
            ParcelId = parcelId,
            RoadId = null,
            RoadName = null,
            RoadType = null,
            DistanceMeters = null,
            GeometryBasis = geometryBasis,
            Status = RoadAccessibilityEnrichmentStatus.Unavailable,
            Evidence = evidence,
            SourceName = sourceName,
            SourceLayer = sourceLayer,
            RoadSourceProvenance = null,
            DistanceProvenance = null
        };

    private static GisReferenceRoadType MapRoadType(int roadType) =>
        Enum.IsDefined(typeof(GisReferenceRoadType), roadType)
            ? (GisReferenceRoadType)roadType
            : GisReferenceRoadType.Unspecified;

    private async Task<object?> ExecuteScalarAsync(
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        var connection = _dbContext.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        foreach (var (name, value) in parameters)
        {
            AddParameter(command, name, value);
        }

        return await command.ExecuteScalarAsync(cancellationToken);
    }

    private static string QualifiedRoadsTable() =>
        $"{LandIntelligenceDbContext.SchemaName}.gis_roads";

    private static string QualifiedAdministrativeBoundariesTable() =>
        $"{LandIntelligenceDbContext.SchemaName}.gis_administrative_boundaries";

    private static async Task EnsureConnectionOpenAsync(
        System.Data.Common.DbConnection connection,
        CancellationToken cancellationToken)
    {
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record ParcelRoadEnrichmentSnapshot(
        Guid Id,
        Point Centroid,
        MultiPolygon? Boundary);

    private sealed record NearestRoadRow(
        Guid RoadId,
        string? RoadName,
        int RoadType,
        string SourceName,
        string SourceLayer,
        double DistanceMeters);
}
