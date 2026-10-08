using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Application.Configuration;
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
    private const string DerivedDistanceSource = "GIS road nearest-neighbor spatial query (geography metres)";

    private readonly LandIntelligenceDbContext _dbContext;
    private readonly GisEnrichmentCoverageOptions _coverageOptions;

    public RoadAccessibilityEnrichmentService(
        LandIntelligenceDbContext dbContext,
        IOptions<GisEnrichmentCoverageOptions> coverageOptions)
    {
        _dbContext = dbContext;
        _coverageOptions = coverageOptions.Value;
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
            $"Road accessibility evidence sourced exclusively from {QualifiedRoadsTable()}. " +
            "Railway proximity is never used for this road distance feature."
        };

        var geometrySelection = ParcelEnrichmentGeometrySelector.Select(parcel.Boundary, parcel.Centroid);
        if (geometrySelection is null)
        {
            evidence.Add("Parcel geometry unavailable: no boundary or centroid could be used for road distance.");

            return BuildResult(
                parcel.Id,
                RoadAccessibilityEnrichmentStatus.Unavailable,
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

        var coverage = await GisEnrichmentCoverageHelper.AssessAsync(
            _dbContext,
            parcelGeometry,
            geometryBasis,
            _coverageOptions,
            cancellationToken);
        evidence.Add(coverage.EvidenceMessage);

        var roadSourceLayer = ResolveRoadSourceLayer(coverage.MatchedDistrict);
        evidence.Add(
            $"Road SourceLayer filter for district '{coverage.MatchedDistrict ?? "(none)"}': " +
            $"'{roadSourceLayer ?? "(all layers)"}'.");

        if (!coverage.IsInsideCoverage)
        {
            return BuildResult(
                parcel.Id,
                RoadAccessibilityEnrichmentStatus.OutsideCoverage,
                evidence,
                geometryBasis,
                sourceName: GisReferenceDataPaths.SourceName,
                sourceLayer: roadSourceLayer ?? GisReferenceDataPaths.ExpresswaysLayer);
        }

        var nearestRoad = await FindNearestRoadAsync(parcelGeometry, roadSourceLayer, cancellationToken);
        if (nearestRoad is null)
        {
            evidence.Add(
                $"No GIS road geometry was available for SourceLayer '{roadSourceLayer ?? "(all layers)"}' " +
                "inside coverage; distance was not fabricated.");

            return BuildResult(
                parcel.Id,
                RoadAccessibilityEnrichmentStatus.Unavailable,
                evidence,
                geometryBasis,
                sourceName: GisReferenceDataPaths.SourceName,
                sourceLayer: roadSourceLayer ?? GisReferenceDataPaths.ExpresswaysLayer);
        }

        var highwayClass = string.Equals(
                nearestRoad.SourceLayer,
                GisEnrichmentCoverageDefaults.OsmMotorRoadsLayer,
                StringComparison.OrdinalIgnoreCase)
            ? OsmMotorRoadAttributeEncoding.TryDecodeHighway(nearestRoad.RoadName)
            : null;
        var displayName = string.Equals(
                nearestRoad.SourceLayer,
                GisEnrichmentCoverageDefaults.OsmMotorRoadsLayer,
                StringComparison.OrdinalIgnoreCase)
            ? OsmMotorRoadAttributeEncoding.TryDecodeDisplayName(nearestRoad.RoadName)
            : nearestRoad.RoadName;
        var filterPolicyVersion = string.Equals(
                nearestRoad.SourceLayer,
                GisEnrichmentCoverageDefaults.OsmMotorRoadsLayer,
                StringComparison.OrdinalIgnoreCase)
            ? _coverageOptions.OsmMotorRoadFilterPolicyVersion
            : null;

        var provenanceSourceName = string.Equals(
                nearestRoad.SourceLayer,
                GisEnrichmentCoverageDefaults.OsmMotorRoadsLayer,
                StringComparison.OrdinalIgnoreCase)
            ? OsmMotorRoadAttributeEncoding.BuildProvenanceSourceName(filterPolicyVersion)
            : nearestRoad.SourceName;

        var roadSourceProvenance = new AttributeProvenanceDto(
            AttributeProvenanceSourceType.ExternalAuthoritative,
            provenanceSourceName,
            Confidence: 1m,
            CollectedAt: measuredAt,
            Verified: true);

        var distanceProvenance = AttributeProvenanceMapper.ToDto(
            AttributeProvenance.Derived(DerivedDistanceSource, confidence: 1m, collectedAt: measuredAt));

        evidence.Add(
            $"Nearest road '{displayName ?? "(unnamed)"}' ({nearestRoad.RoadType}" +
            (highwayClass is null ? string.Empty : $", highway={highwayClass}") +
            $") selected from layer '{nearestRoad.SourceLayer}'" +
            (nearestRoad.SourceFeatureId is null ? "." : $" (SourceFeatureId={nearestRoad.SourceFeatureId})."));
        evidence.Add(
            $"Nearest-neighbour ordering and distance both use geography metres " +
            $"(ORDER BY geom::geography <-> parcel::geography; ST_Distance geography) = {nearestRoad.DistanceMeters:F2} m.");
        if (filterPolicyVersion is not null)
        {
            evidence.Add($"OSM motor-road filter policy version: {filterPolicyVersion}.");
        }

        evidence.Add($"Road accessibility enrichment status: {RoadAccessibilityEnrichmentStatus.Available}.");

        return new RoadAccessibilityEnrichmentResult
        {
            ParcelId = parcel.Id,
            RoadId = nearestRoad.RoadId,
            RoadName = displayName,
            RoadType = MapRoadType(nearestRoad.RoadType),
            DistanceMeters = nearestRoad.DistanceMeters,
            GeometryBasis = geometryBasis,
            Status = RoadAccessibilityEnrichmentStatus.Available,
            Evidence = evidence,
            SourceName = provenanceSourceName,
            SourceLayer = nearestRoad.SourceLayer,
            RoadSourceProvenance = roadSourceProvenance,
            DistanceProvenance = distanceProvenance,
            HighwayClass = highwayClass,
            OsmId = nearestRoad.SourceFeatureId,
            FilterPolicyVersion = filterPolicyVersion
        };
    }

    internal static string BuildNearestRoadSql(string? sourceLayerFilter = null)
    {
        var layerPredicate = string.IsNullOrWhiteSpace(sourceLayerFilter)
            ? string.Empty
            : """ AND r."SourceLayer" = @sourceLayer """;

        return $"""
                WITH parcel_geom AS (
                    SELECT ST_SetSRID(ST_GeomFromText(@geometryWkt, 4326), 4326) AS geom
                )
                SELECT
                    r."Id" AS "RoadId",
                    r."Name" AS "RoadName",
                    r."RoadType" AS "RoadType",
                    r."SourceName" AS "SourceName",
                    r."SourceLayer" AS "SourceLayer",
                    r."SourceFeatureId" AS "SourceFeatureId",
                    ST_Distance(p.geom::geography, r."Geometry"::geography) AS "DistanceMeters"
                FROM parcel_geom p
                INNER JOIN {QualifiedRoadsTable()} r ON r."Geometry" IS NOT NULL
                WHERE 1 = 1
                {layerPredicate}
                ORDER BY r."Geometry"::geography <-> p.geom::geography
                LIMIT 1
                """;
    }

    private string? ResolveRoadSourceLayer(string? matchedDistrict)
    {
        var resolved = _coverageOptions.ResolveRoadSourceLayerForDistrict(matchedDistrict);
        return string.IsNullOrWhiteSpace(resolved) ? null : resolved;
    }

    private async Task<NearestRoadRow?> FindNearestRoadAsync(
        Geometry parcelGeometry,
        string? sourceLayerFilter,
        CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = BuildNearestRoadSql(sourceLayerFilter);
        AddParameter(command, "geometryWkt", parcelGeometry.AsText());
        if (!string.IsNullOrWhiteSpace(sourceLayerFilter))
        {
            AddParameter(command, "sourceLayer", sourceLayerFilter);
        }

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
            reader.IsDBNull(reader.GetOrdinal("SourceFeatureId"))
                ? null
                : reader.GetString(reader.GetOrdinal("SourceFeatureId")),
            reader.GetDouble(reader.GetOrdinal("DistanceMeters")));
    }

    private static RoadAccessibilityEnrichmentResult BuildResult(
        Guid parcelId,
        RoadAccessibilityEnrichmentStatus status,
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
            Status = status,
            Evidence = evidence,
            SourceName = sourceName,
            SourceLayer = sourceLayer,
            RoadSourceProvenance = null,
            DistanceProvenance = null,
            HighwayClass = null,
            OsmId = null,
            FilterPolicyVersion = null
        };

    private static GisReferenceRoadType MapRoadType(int roadType) =>
        Enum.IsDefined(typeof(GisReferenceRoadType), roadType)
            ? (GisReferenceRoadType)roadType
            : GisReferenceRoadType.Unspecified;

    private static string QualifiedRoadsTable() =>
        $"{LandIntelligenceDbContext.SchemaName}.gis_roads";

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
        string? SourceFeatureId,
        double DistanceMeters);
}
