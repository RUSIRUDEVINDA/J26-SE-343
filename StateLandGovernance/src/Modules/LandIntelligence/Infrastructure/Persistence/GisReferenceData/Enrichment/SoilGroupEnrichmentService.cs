using System.Data;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.Mappings;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Mappings;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enrichment;

public sealed class SoilGroupEnrichmentService : ISoilGroupEnrichmentService
{
    private const int DistrictBoundaryType = 2;
    private const string DerivedSoilGroupSource = "GIS soil group spatial intersection query";

    private readonly LandIntelligenceDbContext _dbContext;

    public SoilGroupEnrichmentService(LandIntelligenceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SoilGroupEnrichmentResult> EnrichAsync(
        Guid parcelId,
        CancellationToken cancellationToken = default)
    {
        var parcel = await _dbContext.LandParcels
            .AsNoTracking()
            .Where(entity => entity.Id == parcelId)
            .Select(entity => new ParcelSoilEnrichmentSnapshot(
                entity.Id,
                entity.Centroid,
                entity.Boundary,
                entity.SoilType,
                entity.CharacteristicsProvenanceJson))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Land parcel '{parcelId}' was not found.");

        var measuredAt = DateTimeOffset.UtcNow;
        var (storedSoilProvenance, _, _) =
            AttributeProvenancePersistenceMapper.DeserializeCharacteristicsProvenance(parcel.CharacteristicsProvenanceJson);
        var storedSoilProvenanceDto = AttributeProvenanceMapper.ToDto(storedSoilProvenance);
        var officialPreserved = SoilGroupEnrichmentEvaluator.ShouldPreserveOfficialSoilType(
            storedSoilProvenance?.SourceType);

        var evidence = new List<string>
        {
            $"Soil group evidence sourced exclusively from {QualifiedSoilGroupsTable()} ({GisReferenceDataPaths.SoilGroupsLayer})."
        };

        if (!string.IsNullOrWhiteSpace(parcel.StoredSoilType))
        {
            evidence.Add(
                officialPreserved
                    ? $"Stored SoilType '{parcel.StoredSoilType}' is official and remains authoritative."
                    : $"Stored SoilType '{parcel.StoredSoilType}' is present with non-official provenance.");
        }

        var geometrySelection = ParcelEnrichmentGeometrySelector.Select(parcel.Boundary, parcel.Centroid);
        if (geometrySelection is null)
        {
            evidence.Add("Parcel geometry unavailable: no boundary or centroid could be used for soil enrichment.");

            return BuildUnavailableResult(
                parcel,
                evidence,
                geometryBasis: null,
                storedSoilProvenanceDto,
                officialPreserved);
        }

        var (parcelGeometry, geometryBasis) = geometrySelection.Value;
        evidence.Add(
            geometryBasis == AdministrativeLocationGeometryBasis.Boundary
                ? "Soil enrichment geometry basis: parcel boundary (preferred)."
                : "Soil enrichment geometry basis: parcel centroid (boundary unavailable).");

        var withinPilotCoverage = await IsWithinHambantotaPilotCoverageAsync(
            parcel.Id,
            geometryBasis,
            cancellationToken);

        if (!withinPilotCoverage)
        {
            evidence.Add(
                "Parcel is outside the imported Hambantota GIS pilot coverage; soil group enrichment was not calculated.");

            return BuildUnavailableResult(
                parcel,
                evidence,
                geometryBasis,
                storedSoilProvenanceDto,
                officialPreserved);
        }

        SoilGroupOverlapCandidate? primary = null;
        IReadOnlyList<SoilGroupOverlapCandidate> overlaps = [];

        if (geometryBasis == AdministrativeLocationGeometryBasis.Boundary)
        {
            overlaps = await FindBoundaryOverlapsAsync(parcel.Id, cancellationToken);
            primary = SoilGroupEnrichmentEvaluator.SelectPrimaryOverlap(overlaps);

            if (overlaps.Count > 0)
            {
                evidence.Add($"Detected {overlaps.Count} intersecting GIS soil group polygon(s).");
                foreach (var overlap in SoilGroupEnrichmentEvaluator.ToOverlapEvidence(overlaps))
                {
                    evidence.Add(
                        $"Overlap: '{overlap.SoilGroupName}' covers {overlap.OverlapPercentage:F2}% " +
                        $"({overlap.OverlapAreaSquareMeters:F2} m²).");
                }
            }
        }
        else
        {
            primary = await FindCentroidSoilGroupAsync(parcel.Id, cancellationToken);
            if (primary is not null)
            {
                evidence.Add($"Centroid point-in-polygon match: '{primary.SoilGroupName}'.");
            }
        }

        if (primary is null)
        {
            evidence.Add("No GIS soil group polygon covers the parcel geometry within the imported pilot dataset.");

            return BuildUnavailableResult(
                parcel,
                evidence,
                geometryBasis,
                storedSoilProvenanceDto,
                officialPreserved);
        }

        var soilGroupSourceProvenance = new AttributeProvenanceDto(
            AttributeProvenanceSourceType.ExternalAuthoritative,
            primary.SourceName,
            Confidence: 1m,
            CollectedAt: measuredAt,
            Verified: true);

        var derivedSoilGroupProvenance = AttributeProvenanceMapper.ToDto(
            AttributeProvenance.Derived(DerivedSoilGroupSource, confidence: 1m, collectedAt: measuredAt));

        evidence.Add(
            $"Primary GIS soil group selected: '{primary.SoilGroupName}' from layer '{primary.SourceLayer}'.");
        evidence.Add($"Soil group enrichment status: {SoilGroupEnrichmentStatus.Available}.");

        return new SoilGroupEnrichmentResult
        {
            ParcelId = parcel.Id,
            StoredSoilType = parcel.StoredSoilType,
            StoredSoilTypeProvenance = storedSoilProvenanceDto,
            OfficialSoilTypePreserved = officialPreserved,
            PrimarySoilGroup = primary.SoilGroupName,
            PrimarySoilGroupId = primary.SoilGroupId,
            OverlapPercentage = geometryBasis == AdministrativeLocationGeometryBasis.Boundary
                ? primary.OverlapPercentage
                : null,
            GeometryBasis = geometryBasis,
            Status = SoilGroupEnrichmentStatus.Available,
            Evidence = evidence,
            Overlaps = SoilGroupEnrichmentEvaluator.ToOverlapEvidence(overlaps),
            SourceName = primary.SourceName,
            SourceLayer = primary.SourceLayer,
            SoilGroupSourceProvenance = soilGroupSourceProvenance,
            DerivedSoilGroupProvenance = derivedSoilGroupProvenance
        };
    }

    internal static string BuildBoundaryOverlapSql() =>
        $"""
         WITH parcel_geom AS (
             SELECT ST_MakeValid("Boundary") AS geom
             FROM {QualifiedLandParcelsTable()}
             WHERE "Id" = @parcelId
               AND "Boundary" IS NOT NULL
         ),
         parcel_area AS (
             SELECT ST_Area(geom::geography) AS area_m2 FROM parcel_geom
         ),
         intersections AS (
             SELECT
                 s."Id" AS "SoilGroupId",
                 s."Name" AS "SoilGroupName",
                 s."SourceName" AS "SourceName",
                 s."SourceLayer" AS "SourceLayer",
                 ST_Area(ST_Intersection(p.geom, s."Boundary")::geography) AS overlap_area_m2
             FROM parcel_geom p
             INNER JOIN {QualifiedSoilGroupsTable()} s
                 ON s."Boundary" IS NOT NULL
                AND ST_Intersects(p.geom, s."Boundary")
         )
         SELECT
             i."SoilGroupId",
             i."SoilGroupName",
             i."SourceName",
             i."SourceLayer",
             i.overlap_area_m2 AS "OverlapAreaSquareMeters",
             CASE
                 WHEN pa.area_m2 > 0 THEN (i.overlap_area_m2 / pa.area_m2) * 100
                 ELSE 0
             END AS "OverlapPercentage"
         FROM intersections i
         CROSS JOIN parcel_area pa
         WHERE i.overlap_area_m2 > 0
         ORDER BY i.overlap_area_m2 DESC
         """;

    internal static string BuildCentroidSoilGroupSql() =>
        $"""
         SELECT
             s."Id" AS "SoilGroupId",
             s."Name" AS "SoilGroupName",
             s."SourceName" AS "SourceName",
             s."SourceLayer" AS "SourceLayer"
         FROM {QualifiedSoilGroupsTable()} s
         INNER JOIN {QualifiedLandParcelsTable()} p
             ON p."Id" = @parcelId
         WHERE s."Boundary" IS NOT NULL
           AND ST_Covers(s."Boundary", p."Centroid")
         ORDER BY ST_Area(s."Boundary"::geography) ASC
         LIMIT 1
         """;

    private async Task<IReadOnlyList<SoilGroupOverlapCandidate>> FindBoundaryOverlapsAsync(
        Guid parcelId,
        CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = BuildBoundaryOverlapSql();
        AddParameter(command, "parcelId", parcelId);

        var overlaps = new List<SoilGroupOverlapCandidate>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            overlaps.Add(new SoilGroupOverlapCandidate(
                reader.GetGuid(reader.GetOrdinal("SoilGroupId")),
                reader.GetString(reader.GetOrdinal("SoilGroupName")),
                reader.GetString(reader.GetOrdinal("SourceName")),
                reader.GetString(reader.GetOrdinal("SourceLayer")),
                reader.GetDouble(reader.GetOrdinal("OverlapAreaSquareMeters")),
                Convert.ToDecimal(reader.GetDouble(reader.GetOrdinal("OverlapPercentage")))));
        }

        return overlaps;
    }

    private async Task<SoilGroupOverlapCandidate?> FindCentroidSoilGroupAsync(
        Guid parcelId,
        CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = BuildCentroidSoilGroupSql();
        AddParameter(command, "parcelId", parcelId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new SoilGroupOverlapCandidate(
            reader.GetGuid(reader.GetOrdinal("SoilGroupId")),
            reader.GetString(reader.GetOrdinal("SoilGroupName")),
            reader.GetString(reader.GetOrdinal("SourceName")),
            reader.GetString(reader.GetOrdinal("SourceLayer")),
            OverlapAreaSquareMeters: 0,
            OverlapPercentage: 0);
    }

    private async Task<bool> IsWithinHambantotaPilotCoverageAsync(
        Guid parcelId,
        AdministrativeLocationGeometryBasis geometryBasis,
        CancellationToken cancellationToken)
    {
        var spatialPredicate = geometryBasis == AdministrativeLocationGeometryBasis.Centroid
            ? "ST_Covers(b.\"Boundary\", p.\"Centroid\")"
            : "ST_Intersects(b.\"Boundary\", ST_MakeValid(p.\"Boundary\"))";

        var sql = $"""
                   SELECT EXISTS (
                       SELECT 1
                       FROM {QualifiedAdministrativeBoundariesTable()} b
                       INNER JOIN {QualifiedLandParcelsTable()} p
                           ON p."Id" = @parcelId
                       WHERE b."Name" = @districtName
                         AND b."BoundaryType" = @districtType
                         AND {spatialPredicate}
                   )
                   """;

        var result = await ExecuteScalarAsync(
            sql,
            cancellationToken,
            ("parcelId", parcelId),
            ("districtName", GisReferenceDataPaths.HambantotaDistrictName),
            ("districtType", DistrictBoundaryType));

        return result is bool withinCoverage && withinCoverage;
    }

    private static SoilGroupEnrichmentResult BuildUnavailableResult(
        ParcelSoilEnrichmentSnapshot parcel,
        IReadOnlyList<string> evidence,
        AdministrativeLocationGeometryBasis? geometryBasis,
        AttributeProvenanceDto? storedSoilProvenanceDto,
        bool officialPreserved) =>
        new()
        {
            ParcelId = parcel.Id,
            StoredSoilType = parcel.StoredSoilType,
            StoredSoilTypeProvenance = storedSoilProvenanceDto,
            OfficialSoilTypePreserved = officialPreserved,
            PrimarySoilGroup = null,
            PrimarySoilGroupId = null,
            OverlapPercentage = null,
            GeometryBasis = geometryBasis,
            Status = SoilGroupEnrichmentStatus.Unavailable,
            Evidence = evidence,
            Overlaps = [],
            SourceName = GisReferenceDataPaths.SourceName,
            SourceLayer = GisReferenceDataPaths.SoilGroupsLayer,
            SoilGroupSourceProvenance = null,
            DerivedSoilGroupProvenance = null
        };

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

    private static string QualifiedSoilGroupsTable() =>
        $"{LandIntelligenceDbContext.SchemaName}.gis_soil_groups";

    private static string QualifiedLandParcelsTable() =>
        $"{LandIntelligenceDbContext.SchemaName}.land_parcels";

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

    private sealed record ParcelSoilEnrichmentSnapshot(
        Guid Id,
        Point Centroid,
        MultiPolygon? Boundary,
        string? StoredSoilType,
        string? CharacteristicsProvenanceJson);
}
