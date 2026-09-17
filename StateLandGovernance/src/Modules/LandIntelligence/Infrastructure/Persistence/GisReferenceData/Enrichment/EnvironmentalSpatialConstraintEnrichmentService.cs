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

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enrichment;

public sealed class EnvironmentalSpatialConstraintEnrichmentService
    : IEnvironmentalSpatialConstraintEnrichmentService
{
    internal const double ErosionObservationProximityRadiusMeters = 1000d;

    private const int DistrictBoundaryType = 2;
    private const string DerivedConservationSource =
        "GIS soil conservation area spatial intersection query";
    private const string DerivedErosionSource =
        "GIS soil erosion observation proximity spatial query";

    private readonly LandIntelligenceDbContext _dbContext;

    public EnvironmentalSpatialConstraintEnrichmentService(LandIntelligenceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<EnvironmentalSpatialConstraintEnrichmentResult> EnrichAsync(
        Guid parcelId,
        CancellationToken cancellationToken = default)
    {
        var parcel = await _dbContext.LandParcels
            .AsNoTracking()
            .Where(entity => entity.Id == parcelId)
            .Select(entity => new ParcelEnvironmentalEnrichmentSnapshot(
                entity.Id,
                entity.Centroid,
                entity.Boundary))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Land parcel '{parcelId}' was not found.");

        var measuredAt = DateTimeOffset.UtcNow;
        var evidence = new List<string>
        {
            $"Environmental/spatial constraint evidence sourced from {QualifiedSoilConservationAreasTable()} " +
            $"({GisReferenceDataPaths.SoilConservationAreasLayer}) and {QualifiedSoilErosionObservationsTable()} " +
            $"({GisReferenceDataPaths.SoilErosionLayer})."
        };

        var geometrySelection = ParcelEnrichmentGeometrySelector.Select(parcel.Boundary, parcel.Centroid);
        if (geometrySelection is null)
        {
            evidence.Add(
                "Parcel geometry unavailable: no boundary or centroid could be used for environmental enrichment.");

            return BuildUnavailableResult(
                parcel.Id,
                evidence,
                geometryBasis: null,
                erosionDataStatus: ErosionDataStatus.Unavailable);
        }

        var (_, geometryBasis) = geometrySelection.Value;
        evidence.Add(
            geometryBasis == AdministrativeLocationGeometryBasis.Boundary
                ? "Environmental enrichment geometry basis: parcel boundary (preferred)."
                : "Environmental enrichment geometry basis: parcel centroid (boundary unavailable).");

        var withinPilotCoverage = await IsWithinHambantotaPilotCoverageAsync(
            parcel.Id,
            geometryBasis,
            cancellationToken);

        if (!withinPilotCoverage)
        {
            evidence.Add(
                "Parcel is outside the imported Hambantota GIS pilot coverage; environmental enrichment was not calculated.");

            return BuildUnavailableResult(
                parcel.Id,
                evidence,
                geometryBasis,
                erosionDataStatus: ErosionDataStatus.Unavailable);
        }

        IReadOnlyList<SoilConservationAreaEvidence> conservationAreas;
        AttributeProvenanceDto? conservationSourceProvenance = null;
        AttributeProvenanceDto? derivedConservationProvenance = null;

        if (geometryBasis == AdministrativeLocationGeometryBasis.Boundary)
        {
            var overlaps = await FindBoundaryConservationOverlapsAsync(parcel.Id, cancellationToken);
            conservationAreas = EnvironmentalSpatialConstraintEnrichmentEvaluator.ToConservationAreaEvidence(overlaps);

            if (overlaps.Count > 0)
            {
                evidence.Add($"Detected {overlaps.Count} intersecting GIS soil conservation area polygon(s).");
                foreach (var overlap in conservationAreas)
                {
                    evidence.Add(
                        $"Conservation overlap: '{overlap.Name}' covers {overlap.OverlapPercentage:F2}% " +
                        $"({overlap.OverlapAreaSquareMeters:F2} m²).");
                }

                var primary = overlaps[0];
                conservationSourceProvenance = new AttributeProvenanceDto(
                    AttributeProvenanceSourceType.ExternalAuthoritative,
                    primary.SourceName,
                    Confidence: 1m,
                    CollectedAt: measuredAt,
                    Verified: true);
                derivedConservationProvenance = AttributeProvenanceMapper.ToDto(
                    AttributeProvenance.Derived(DerivedConservationSource, confidence: 1m, collectedAt: measuredAt));
            }
            else
            {
                evidence.Add(
                    "No GIS soil conservation area polygon intersects the parcel boundary within the imported pilot dataset.");
            }
        }
        else
        {
            var matches = await FindCentroidConservationAreasAsync(parcel.Id, cancellationToken);
            conservationAreas = EnvironmentalSpatialConstraintEnrichmentEvaluator.ToConservationAreaEvidence(matches);

            if (matches.Count > 0)
            {
                evidence.Add($"Detected {matches.Count} GIS soil conservation area polygon(s) covering parcel centroid.");
                foreach (var match in conservationAreas)
                {
                    evidence.Add($"Centroid point-in-polygon match: '{match.Name}'.");
                }

                var primary = matches[0];
                conservationSourceProvenance = new AttributeProvenanceDto(
                    AttributeProvenanceSourceType.ExternalAuthoritative,
                    primary.SourceName,
                    Confidence: 1m,
                    CollectedAt: measuredAt,
                    Verified: true);
                derivedConservationProvenance = AttributeProvenanceMapper.ToDto(
                    AttributeProvenance.Derived(DerivedConservationSource, confidence: 1m, collectedAt: measuredAt));
            }
            else
            {
                evidence.Add(
                    "No GIS soil conservation area polygon covers the parcel centroid within the imported pilot dataset.");
            }
        }

        var erosionDataAvailableInPilot = await HasErosionObservationsInPilotAsync(cancellationToken);
        IReadOnlyList<SoilErosionObservationEvidence> erosionObservations = [];
        AttributeProvenanceDto? erosionSourceProvenance = null;
        AttributeProvenanceDto? derivedErosionProvenance = null;
        ErosionDataStatus erosionDataStatus;

        if (!erosionDataAvailableInPilot)
        {
            erosionDataStatus = ErosionDataStatus.Unavailable;
            evidence.Add(
                "Soil erosion GIS observations are unavailable in the imported pilot dataset. " +
                "Absence of GIS observations must not be interpreted as evidence that the parcel is environmentally safe.");
        }
        else
        {
            erosionDataStatus = ErosionDataStatus.Available;

            var observations = geometryBasis == AdministrativeLocationGeometryBasis.Boundary
                ? await FindBoundaryErosionObservationsAsync(parcel.Id, cancellationToken)
                : await FindCentroidErosionObservationsAsync(parcel.Id, cancellationToken);

            erosionObservations =
                EnvironmentalSpatialConstraintEnrichmentEvaluator.ToErosionObservationEvidence(observations);

            if (observations.Count > 0)
            {
                evidence.Add(
                    $"Detected {observations.Count} relevant GIS soil erosion observation(s) within " +
                    $"intersection or {ErosionObservationProximityRadiusMeters:F0} m proximity.");
                foreach (var observation in erosionObservations)
                {
                    evidence.Add(
                        $"Erosion observation '{observation.ObservationClass ?? "(unclassified)"}' " +
                        $"at {observation.DistanceMeters:F2} m.");
                }

                var primary = observations[0];
                erosionSourceProvenance = new AttributeProvenanceDto(
                    AttributeProvenanceSourceType.ExternalAuthoritative,
                    primary.SourceName,
                    Confidence: 1m,
                    CollectedAt: measuredAt,
                    Verified: true);
                derivedErosionProvenance = AttributeProvenanceMapper.ToDto(
                    AttributeProvenance.Derived(DerivedErosionSource, confidence: 1m, collectedAt: measuredAt));
            }
            else
            {
                evidence.Add(
                    "No GIS soil erosion observations intersect or fall within proximity of the parcel geometry. " +
                    "This is a factual spatial result only; it does not indicate low erosion risk or environmental safety.");
            }
        }

        evidence.Add(
            $"Environmental/spatial constraint enrichment status: {EnvironmentalSpatialConstraintEnrichmentStatus.Available}.");
        evidence.Add($"Erosion data status: {erosionDataStatus}.");

        return new EnvironmentalSpatialConstraintEnrichmentResult
        {
            ParcelId = parcel.Id,
            Status = EnvironmentalSpatialConstraintEnrichmentStatus.Available,
            GeometryBasis = geometryBasis,
            IntersectsSoilConservationArea = conservationAreas.Count > 0,
            ConservationAreas = conservationAreas,
            ErosionDataStatus = erosionDataStatus,
            ErosionObservations = erosionObservations,
            Evidence = evidence,
            SourceName = GisReferenceDataPaths.SourceName,
            SourceLayer =
                $"{GisReferenceDataPaths.SoilConservationAreasLayer}, {GisReferenceDataPaths.SoilErosionLayer}",
            ConservationAreaSourceProvenance = conservationSourceProvenance,
            DerivedConservationProvenance = derivedConservationProvenance,
            ErosionObservationSourceProvenance = erosionSourceProvenance,
            DerivedErosionProvenance = derivedErosionProvenance
        };
    }

    internal static string BuildBoundaryConservationOverlapSql() =>
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
                 c."Id" AS "ConservationAreaId",
                 c."Name" AS "Name",
                 c."Description" AS "Description",
                 c."SourceName" AS "SourceName",
                 c."SourceLayer" AS "SourceLayer",
                 ST_Area(ST_Intersection(p.geom, c."Boundary")::geography) AS overlap_area_m2
             FROM parcel_geom p
             INNER JOIN {QualifiedSoilConservationAreasTable()} c
                 ON c."Boundary" IS NOT NULL
                AND ST_Intersects(p.geom, c."Boundary")
         )
         SELECT
             i."ConservationAreaId",
             i."Name",
             i."Description",
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
         ORDER BY i.overlap_area_m2 DESC, i."Name" ASC
         """;

    internal static string BuildCentroidConservationAreasSql() =>
        $"""
         SELECT
             c."Id" AS "ConservationAreaId",
             c."Name" AS "Name",
             c."Description" AS "Description",
             c."SourceName" AS "SourceName",
             c."SourceLayer" AS "SourceLayer"
         FROM {QualifiedSoilConservationAreasTable()} c
         INNER JOIN {QualifiedLandParcelsTable()} p
             ON p."Id" = @parcelId
         WHERE c."Boundary" IS NOT NULL
           AND ST_Covers(c."Boundary", p."Centroid")
         ORDER BY c."Name" ASC
         """;

    internal static string BuildErosionObservationsInPilotSql() =>
        $"""
         SELECT EXISTS (
             SELECT 1
             FROM {QualifiedSoilErosionObservationsTable()} e
             INNER JOIN {QualifiedAdministrativeBoundariesTable()} b
                 ON b."Name" = @districtName
                AND b."BoundaryType" = @districtType
             WHERE e."Location" IS NOT NULL
               AND ST_Intersects(e."Location", b."Boundary")
         )
         """;

    internal static string BuildBoundaryErosionObservationsSql() =>
        $"""
         WITH parcel_geom AS (
             SELECT ST_MakeValid("Boundary") AS geom
             FROM {QualifiedLandParcelsTable()}
             WHERE "Id" = @parcelId
               AND "Boundary" IS NOT NULL
         )
         SELECT
             e."Id" AS "ObservationId",
             e."ObservationClass" AS "ObservationClass",
             e."Description" AS "Description",
             e."ErosionRate" AS "ErosionRate",
             e."SourceName" AS "SourceName",
             e."SourceLayer" AS "SourceLayer",
             ST_Distance(p.geom::geography, e."Location"::geography) AS "DistanceMeters"
         FROM parcel_geom p
         INNER JOIN {QualifiedSoilErosionObservationsTable()} e
             ON e."Location" IS NOT NULL
            AND (
                ST_Intersects(p.geom, e."Location")
                OR ST_DWithin(e."Location"::geography, p.geom::geography, @proximityRadiusMeters)
            )
         ORDER BY "DistanceMeters" ASC, e."Id" ASC
         """;

    internal static string BuildCentroidErosionObservationsSql() =>
        $"""
         SELECT
             e."Id" AS "ObservationId",
             e."ObservationClass" AS "ObservationClass",
             e."Description" AS "Description",
             e."ErosionRate" AS "ErosionRate",
             e."SourceName" AS "SourceName",
             e."SourceLayer" AS "SourceLayer",
             ST_Distance(p."Centroid"::geography, e."Location"::geography) AS "DistanceMeters"
         FROM {QualifiedSoilErosionObservationsTable()} e
         INNER JOIN {QualifiedLandParcelsTable()} p
             ON p."Id" = @parcelId
         WHERE e."Location" IS NOT NULL
           AND ST_DWithin(e."Location"::geography, p."Centroid"::geography, @proximityRadiusMeters)
         ORDER BY "DistanceMeters" ASC, e."Id" ASC
         """;

    private async Task<IReadOnlyList<SoilConservationOverlapCandidate>> FindBoundaryConservationOverlapsAsync(
        Guid parcelId,
        CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = BuildBoundaryConservationOverlapSql();
        AddParameter(command, "parcelId", parcelId);

        var overlaps = new List<SoilConservationOverlapCandidate>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            overlaps.Add(new SoilConservationOverlapCandidate(
                reader.GetGuid(reader.GetOrdinal("ConservationAreaId")),
                reader.GetString(reader.GetOrdinal("Name")),
                reader.IsDBNull(reader.GetOrdinal("Description"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("Description")),
                reader.GetString(reader.GetOrdinal("SourceName")),
                reader.GetString(reader.GetOrdinal("SourceLayer")),
                reader.GetDouble(reader.GetOrdinal("OverlapAreaSquareMeters")),
                Convert.ToDecimal(reader.GetDouble(reader.GetOrdinal("OverlapPercentage")))));
        }

        return overlaps;
    }

    private async Task<IReadOnlyList<SoilConservationCentroidCandidate>> FindCentroidConservationAreasAsync(
        Guid parcelId,
        CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = BuildCentroidConservationAreasSql();
        AddParameter(command, "parcelId", parcelId);

        var matches = new List<SoilConservationCentroidCandidate>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            matches.Add(new SoilConservationCentroidCandidate(
                reader.GetGuid(reader.GetOrdinal("ConservationAreaId")),
                reader.GetString(reader.GetOrdinal("Name")),
                reader.IsDBNull(reader.GetOrdinal("Description"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("Description")),
                reader.GetString(reader.GetOrdinal("SourceName")),
                reader.GetString(reader.GetOrdinal("SourceLayer"))));
        }

        return matches;
    }

    private async Task<bool> HasErosionObservationsInPilotAsync(CancellationToken cancellationToken)
    {
        var result = await ExecuteScalarAsync(
            BuildErosionObservationsInPilotSql(),
            cancellationToken,
            ("districtName", GisReferenceDataPaths.HambantotaDistrictName),
            ("districtType", DistrictBoundaryType));

        return result is bool hasObservations && hasObservations;
    }

    private async Task<IReadOnlyList<SoilErosionObservationCandidate>> FindBoundaryErosionObservationsAsync(
        Guid parcelId,
        CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = BuildBoundaryErosionObservationsSql();
        AddParameter(command, "parcelId", parcelId);
        AddParameter(command, "proximityRadiusMeters", ErosionObservationProximityRadiusMeters);

        return await ReadErosionObservationsAsync(command, cancellationToken);
    }

    private async Task<IReadOnlyList<SoilErosionObservationCandidate>> FindCentroidErosionObservationsAsync(
        Guid parcelId,
        CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = BuildCentroidErosionObservationsSql();
        AddParameter(command, "parcelId", parcelId);
        AddParameter(command, "proximityRadiusMeters", ErosionObservationProximityRadiusMeters);

        return await ReadErosionObservationsAsync(command, cancellationToken);
    }

    private static async Task<IReadOnlyList<SoilErosionObservationCandidate>> ReadErosionObservationsAsync(
        System.Data.Common.DbCommand command,
        CancellationToken cancellationToken)
    {
        var observations = new List<SoilErosionObservationCandidate>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            observations.Add(new SoilErosionObservationCandidate(
                reader.GetGuid(reader.GetOrdinal("ObservationId")),
                reader.IsDBNull(reader.GetOrdinal("ObservationClass"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("ObservationClass")),
                reader.IsDBNull(reader.GetOrdinal("Description"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("Description")),
                reader.IsDBNull(reader.GetOrdinal("ErosionRate"))
                    ? null
                    : reader.GetDecimal(reader.GetOrdinal("ErosionRate")),
                reader.GetString(reader.GetOrdinal("SourceName")),
                reader.GetString(reader.GetOrdinal("SourceLayer")),
                reader.GetDouble(reader.GetOrdinal("DistanceMeters"))));
        }

        return observations;
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

    private static EnvironmentalSpatialConstraintEnrichmentResult BuildUnavailableResult(
        Guid parcelId,
        IReadOnlyList<string> evidence,
        AdministrativeLocationGeometryBasis? geometryBasis,
        ErosionDataStatus erosionDataStatus) =>
        new()
        {
            ParcelId = parcelId,
            Status = EnvironmentalSpatialConstraintEnrichmentStatus.Unavailable,
            GeometryBasis = geometryBasis,
            IntersectsSoilConservationArea = false,
            ConservationAreas = [],
            ErosionDataStatus = erosionDataStatus,
            ErosionObservations = [],
            Evidence = evidence,
            SourceName = GisReferenceDataPaths.SourceName,
            SourceLayer =
                $"{GisReferenceDataPaths.SoilConservationAreasLayer}, {GisReferenceDataPaths.SoilErosionLayer}",
            ConservationAreaSourceProvenance = null,
            DerivedConservationProvenance = null,
            ErosionObservationSourceProvenance = null,
            DerivedErosionProvenance = null
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

    private static string QualifiedSoilConservationAreasTable() =>
        $"{LandIntelligenceDbContext.SchemaName}.gis_soil_conservation_areas";

    private static string QualifiedSoilErosionObservationsTable() =>
        $"{LandIntelligenceDbContext.SchemaName}.gis_soil_erosion_observations";

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

    private sealed record ParcelEnvironmentalEnrichmentSnapshot(
        Guid Id,
        Point Centroid,
        MultiPolygon? Boundary);
}
