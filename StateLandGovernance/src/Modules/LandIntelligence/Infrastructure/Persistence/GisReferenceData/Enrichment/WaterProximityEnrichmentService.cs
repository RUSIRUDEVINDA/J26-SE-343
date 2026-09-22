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

public sealed class WaterProximityEnrichmentService : IWaterProximityEnrichmentService
{
    private const int CanalFeatureType = 1;
    private const int LakeFeatureType = 2;
    private const string DerivedDistanceSource = "GIS water feature nearest-neighbor spatial query";

    private readonly LandIntelligenceDbContext _dbContext;
    private readonly GisEnrichmentCoverageOptions _coverageOptions;

    public WaterProximityEnrichmentService(
        LandIntelligenceDbContext dbContext,
        IOptions<GisEnrichmentCoverageOptions> coverageOptions)
    {
        _dbContext = dbContext;
        _coverageOptions = coverageOptions.Value;
    }

    public async Task<WaterProximityEnrichmentResult> EnrichAsync(
        Guid parcelId,
        CancellationToken cancellationToken = default)
    {
        var parcel = await _dbContext.LandParcels
            .AsNoTracking()
            .Where(entity => entity.Id == parcelId)
            .Select(entity => new ParcelWaterEnrichmentSnapshot(entity.Id, entity.Centroid, entity.Boundary))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Land parcel '{parcelId}' was not found.");

        var measuredAt = DateTimeOffset.UtcNow;
        var evidence = new List<string>
        {
            $"Water proximity evidence sourced exclusively from {QualifiedWaterFeaturesTable()} " +
            $"(canals and lakes only; {GisReferenceDataPaths.CanalsLayer}, {GisReferenceDataPaths.LakesLayer})."
        };

        var geometrySelection = ParcelEnrichmentGeometrySelector.Select(parcel.Boundary, parcel.Centroid);
        if (geometrySelection is null)
        {
            evidence.Add("Parcel geometry unavailable: no boundary or centroid could be used for water proximity.");

            return BuildResult(
                parcel.Id,
                WaterProximityEnrichmentStatus.Unavailable,
                evidence,
                geometryBasis: null,
                sourceName: GisReferenceDataPaths.SourceName,
                sourceLayer: $"{GisReferenceDataPaths.CanalsLayer}, {GisReferenceDataPaths.LakesLayer}");
        }

        var (parcelGeometry, geometryBasis) = geometrySelection.Value;
        evidence.Add(
            geometryBasis == AdministrativeLocationGeometryBasis.Boundary
                ? "Proximity geometry basis: parcel boundary (preferred)."
                : "Proximity geometry basis: parcel centroid (boundary unavailable).");

        var coverage = await GisEnrichmentCoverageHelper.AssessAsync(
            _dbContext,
            parcelGeometry,
            geometryBasis,
            _coverageOptions,
            cancellationToken);
        evidence.Add(coverage.EvidenceMessage);

        if (!coverage.IsInsideCoverage)
        {
            return BuildResult(
                parcel.Id,
                WaterProximityEnrichmentStatus.OutsideCoverage,
                evidence,
                geometryBasis,
                sourceName: GisReferenceDataPaths.SourceName,
                sourceLayer: $"{GisReferenceDataPaths.CanalsLayer}, {GisReferenceDataPaths.LakesLayer}");
        }

        var nearestFeature = await FindNearestWaterFeatureAsync(parcelGeometry, cancellationToken);
        if (nearestFeature is null)
        {
            evidence.Add(
                "No GIS canal or lake geometry was available within coverage; distance was not fabricated. " +
                "Utility WaterSupply is never substituted for natural-water proximity.");

            return BuildResult(
                parcel.Id,
                WaterProximityEnrichmentStatus.Unavailable,
                evidence,
                geometryBasis,
                sourceName: GisReferenceDataPaths.SourceName,
                sourceLayer: $"{GisReferenceDataPaths.CanalsLayer}, {GisReferenceDataPaths.LakesLayer}");
        }

        var featureSourceProvenance = new AttributeProvenanceDto(
            AttributeProvenanceSourceType.ExternalAuthoritative,
            nearestFeature.SourceName,
            Confidence: 1m,
            CollectedAt: measuredAt,
            Verified: true);

        var distanceProvenance = AttributeProvenanceMapper.ToDto(
            AttributeProvenance.Derived(DerivedDistanceSource, confidence: 1m, collectedAt: measuredAt));

        evidence.Add(
            $"Nearest water feature '{nearestFeature.FeatureName ?? "(unnamed)"}' ({MapFeatureType(nearestFeature.FeatureType)}) " +
            $"selected from layer '{nearestFeature.SourceLayer}'.");
        evidence.Add(
            $"Geographic distance calculated with ST_Distance(parcel geometry::geography, water geometry::geography) = {nearestFeature.DistanceMeters:F2} m.");
        evidence.Add("Water proximity is factual spatial intelligence only; no suitability judgment is applied.");
        evidence.Add($"Water proximity enrichment status: {WaterProximityEnrichmentStatus.Available}.");

        return new WaterProximityEnrichmentResult
        {
            ParcelId = parcel.Id,
            FeatureId = nearestFeature.FeatureId,
            FeatureName = nearestFeature.FeatureName,
            FeatureType = MapFeatureType(nearestFeature.FeatureType),
            DistanceMeters = nearestFeature.DistanceMeters,
            GeometryBasis = geometryBasis,
            Status = WaterProximityEnrichmentStatus.Available,
            Evidence = evidence,
            SourceName = nearestFeature.SourceName,
            SourceLayer = nearestFeature.SourceLayer,
            FeatureSourceProvenance = featureSourceProvenance,
            DistanceProvenance = distanceProvenance
        };
    }

    internal static string BuildNearestWaterFeatureSql() =>
        $"""
         WITH parcel_geom AS (
             SELECT ST_SetSRID(ST_GeomFromText(@geometryWkt, 4326), 4326) AS geom
         )
         SELECT
             w."Id" AS "FeatureId",
             w."Name" AS "FeatureName",
             w."FeatureType" AS "FeatureType",
             w."SourceName" AS "SourceName",
             w."SourceLayer" AS "SourceLayer",
             ST_Distance(p.geom::geography, w."Geometry"::geography) AS "DistanceMeters"
         FROM parcel_geom p
         INNER JOIN {QualifiedWaterFeaturesTable()} w
             ON w."Geometry" IS NOT NULL
            AND w."FeatureType" IN ({CanalFeatureType}, {LakeFeatureType})
         ORDER BY w."Geometry"::geography <-> p.geom::geography
         LIMIT 1
         """;

    private async Task<NearestWaterFeatureRow?> FindNearestWaterFeatureAsync(
        Geometry parcelGeometry,
        CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = BuildNearestWaterFeatureSql();
        AddParameter(command, "geometryWkt", parcelGeometry.AsText());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new NearestWaterFeatureRow(
            reader.GetGuid(reader.GetOrdinal("FeatureId")),
            reader.IsDBNull(reader.GetOrdinal("FeatureName"))
                ? null
                : reader.GetString(reader.GetOrdinal("FeatureName")),
            reader.GetInt32(reader.GetOrdinal("FeatureType")),
            reader.GetString(reader.GetOrdinal("SourceName")),
            reader.GetString(reader.GetOrdinal("SourceLayer")),
            reader.GetDouble(reader.GetOrdinal("DistanceMeters")));
    }

    private static WaterProximityEnrichmentResult BuildResult(
        Guid parcelId,
        WaterProximityEnrichmentStatus status,
        IReadOnlyList<string> evidence,
        AdministrativeLocationGeometryBasis? geometryBasis,
        string sourceName,
        string sourceLayer) =>
        new()
        {
            ParcelId = parcelId,
            FeatureId = null,
            FeatureName = null,
            FeatureType = null,
            DistanceMeters = null,
            GeometryBasis = geometryBasis,
            Status = status,
            Evidence = evidence,
            SourceName = sourceName,
            SourceLayer = sourceLayer,
            FeatureSourceProvenance = null,
            DistanceProvenance = null
        };

    private static GisReferenceWaterFeatureType MapFeatureType(int featureType) =>
        Enum.IsDefined(typeof(GisReferenceWaterFeatureType), featureType)
            ? (GisReferenceWaterFeatureType)featureType
            : throw new InvalidOperationException($"Unsupported water feature type '{featureType}'.");

    private static string QualifiedWaterFeaturesTable() =>
        $"{LandIntelligenceDbContext.SchemaName}.gis_water_features";

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

    private sealed record ParcelWaterEnrichmentSnapshot(
        Guid Id,
        Point Centroid,
        MultiPolygon? Boundary);

    private sealed record NearestWaterFeatureRow(
        Guid FeatureId,
        string? FeatureName,
        int FeatureType,
        string SourceName,
        string SourceLayer,
        double DistanceMeters);
}
