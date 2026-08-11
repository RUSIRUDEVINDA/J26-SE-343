using System.Data;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.Exceptions;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.PostGIS;

public sealed class PostGisSpatialAnalysisService : ISpatialAnalysisService
{
    private readonly LandIntelligenceDbContext _dbContext;

    public PostGisSpatialAnalysisService(LandIntelligenceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PointInPolygonResultDto> IsPointInPolygonAsync(
        PointInPolygonRequest request,
        CancellationToken cancellationToken = default)
    {
        var point = PostGisGeometryFactory.CreatePoint(request.Longitude, request.Latitude);
        MultiPolygon? constraintGeometry = null;

        if (request.SpatialConstraintId.HasValue)
        {
            constraintGeometry = await _dbContext.SpatialConstraints
                .AsNoTracking()
                .Where(constraint => constraint.Id == request.SpatialConstraintId.Value)
                .Select(constraint => constraint.ConstraintGeometry)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var polygon = PostGisGeometryFactory.ResolvePolygonGeometry(request.PolygonRing, constraintGeometry)
            ?? throw new Application.Interfaces.ValidationException(["A polygon ring or spatial constraint identifier is required."]);

        var isInside = await ExecuteContainsAsync(point, polygon, cancellationToken);

        return new PointInPolygonResultDto(
            request.Latitude,
            request.Longitude,
            isInside,
            request.SpatialConstraintId,
            request.SpatialConstraintId.HasValue ? "database_constraint" : "request_polygon");
    }

    public async Task<IReadOnlyList<ParcelIntersectionResultDto>> FindParcelsIntersectingConstraintAsync(
        Guid spatialConstraintId,
        CancellationToken cancellationToken = default)
    {
        var constraint = await _dbContext.SpatialConstraints
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == spatialConstraintId, cancellationToken)
            ?? throw new Application.Interfaces.ValidationException([$"Spatial constraint '{spatialConstraintId}' was not found."]);

        if (constraint.ConstraintGeometry is null)
        {
            return [];
        }

        return await QueryParcelIntersectionsAsync(constraint, cancellationToken);
    }

    public async Task<GeometryIntersectionResultDto> CheckGeometriesIntersectAsync(
        GeometryIntersectionRequest request,
        CancellationToken cancellationToken = default)
    {
        var parcel = await GetParcelGeometryAsync(request.LandParcelId, cancellationToken);

        if (!request.SpatialConstraintId.HasValue)
        {
            throw new Application.Interfaces.ValidationException(["Spatial constraint identifier is required."]);
        }

        var constraintGeometry = await _dbContext.SpatialConstraints
            .AsNoTracking()
            .Where(constraint => constraint.Id == request.SpatialConstraintId.Value)
            .Select(constraint => constraint.ConstraintGeometry)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new Application.Interfaces.ValidationException([$"Spatial constraint '{request.SpatialConstraintId}' was not found."]);

        if (constraintGeometry is null)
        {
            return new GeometryIntersectionResultDto(
                request.LandParcelId,
                request.SpatialConstraintId,
                Intersects: false,
                IntersectionType: "none");
        }

        var parcelGeometry = PostGisGeometryFactory.ResolveParcelGeometry(parcel.Centroid, parcel.Boundary);
        var intersects = await ExecuteIntersectsAsync(parcelGeometry, constraintGeometry, cancellationToken);

        return new GeometryIntersectionResultDto(
            request.LandParcelId,
            request.SpatialConstraintId,
            intersects,
            DetermineIntersectionType(parcel.Centroid, parcel.Boundary, constraintGeometry));
    }

    public async Task<IReadOnlyList<ProximityResultDto>> FindParcelsNearPointAsync(
        ProximitySearchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.RadiusMeters <= 0)
        {
            throw new Application.Interfaces.ValidationException(["Radius must be greater than zero meters."]);
        }

        const string sql = """
            SELECT
                p."Id",
                p."CadastralNumber",
                ST_Y(p."Centroid") AS "Latitude",
                ST_X(p."Centroid") AS "Longitude",
                ST_Distance(
                    p."Centroid"::geography,
                    ST_SetSRID(ST_MakePoint(@longitude, @latitude), 4326)::geography) AS "DistanceMeters"
            FROM land_intelligence.land_parcels p
            WHERE ST_DWithin(
                p."Centroid"::geography,
                ST_SetSRID(ST_MakePoint(@longitude, @latitude), 4326)::geography,
                @radiusMeters)
            ORDER BY "DistanceMeters"
            LIMIT @maxResults
            """;

        return await ExecuteProximityQueryAsync(
            sql,
            request.Longitude,
            request.Latitude,
            request.RadiusMeters,
            request.MaxResults,
            cancellationToken);
    }

    public async Task<DistanceResultDto> CalculateDistanceBetweenParcelAndInfrastructureAsync(
        Guid landParcelId,
        Guid infrastructureFeatureId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                p."Id" AS "ParcelId",
                i."Id" AS "InfrastructureId",
                i."Name",
                i."Type",
                i."DistanceMeters" AS "StoredDistanceMeters",
                ST_Distance(
                    p."Centroid"::geography,
                    i."Location"::geography) AS "CalculatedDistanceMeters"
            FROM land_intelligence.land_parcels p
            INNER JOIN land_intelligence.infrastructure_features i ON i."Id" = @infrastructureFeatureId
            WHERE p."Id" = @landParcelId
              AND i."Location" IS NOT NULL
            """;

        var connection = _dbContext.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParameter(command, "landParcelId", landParcelId);
        AddParameter(command, "infrastructureFeatureId", infrastructureFeatureId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new Application.Interfaces.ValidationException([
                "Parcel and infrastructure feature with a known location are required for distance analysis."
            ]);
        }

        var calculatedDistance = reader.GetDouble(reader.GetOrdinal("CalculatedDistanceMeters"));
        var storedDistance = reader.IsDBNull(reader.GetOrdinal("StoredDistanceMeters"))
            ? (decimal?)null
            : reader.GetDecimal(reader.GetOrdinal("StoredDistanceMeters"));

        return new DistanceResultDto(
            reader.GetGuid(reader.GetOrdinal("ParcelId")),
            reader.GetGuid(reader.GetOrdinal("InfrastructureId")),
            reader.GetString(reader.GetOrdinal("Name")),
            (InfrastructureFeatureType)reader.GetInt32(reader.GetOrdinal("Type")),
            calculatedDistance,
            storedDistance.HasValue && Math.Abs((double)storedDistance.Value - calculatedDistance) <= 1d);
    }

    public async Task<IReadOnlyList<SpatialFilterResultDto>> FilterParcelsAsync(
        SpatialFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var results = new List<SpatialFilterResultDto>();

        if (HasBoundingBox(request))
        {
            var envelopeResults = await FilterByBoundingBoxAsync(request, cancellationToken);
            results.AddRange(envelopeResults);
        }
        else if (request.IntersectingConstraintId.HasValue)
        {
            var intersectionResults = await FindParcelsIntersectingConstraintAsync(
                request.IntersectingConstraintId.Value,
                cancellationToken);

            var parcelIds = intersectionResults.Select(item => item.LandParcelId).ToList();
            var parcelDetails = await _dbContext.LandParcels
                .AsNoTracking()
                .Where(parcel => parcelIds.Contains(parcel.Id))
                .Select(parcel => new
                {
                    parcel.Id,
                    parcel.Province,
                    parcel.District,
                    Latitude = parcel.Centroid.Y,
                    Longitude = parcel.Centroid.X
                })
                .ToDictionaryAsync(item => item.Id, cancellationToken);

            results.AddRange(intersectionResults.Select(item =>
            {
                parcelDetails.TryGetValue(item.LandParcelId, out var details);
                return new SpatialFilterResultDto(
                    item.LandParcelId,
                    item.CadastralNumber,
                    details?.Province ?? string.Empty,
                    details?.District ?? string.Empty,
                    details?.Latitude ?? 0,
                    details?.Longitude ?? 0,
                    null);
            }));
        }
        else if (request.NearLatitude.HasValue
            && request.NearLongitude.HasValue
            && request.NearRadiusMeters.HasValue)
        {
            var proximityResults = await FindParcelsNearPointAsync(
                new ProximitySearchRequest
                {
                    Latitude = request.NearLatitude.Value,
                    Longitude = request.NearLongitude.Value,
                    RadiusMeters = request.NearRadiusMeters.Value,
                    MaxResults = request.MaxResults
                },
                cancellationToken);

            results.AddRange(proximityResults.Select(item => new SpatialFilterResultDto(
                item.LandParcelId,
                item.CadastralNumber,
                Province: string.Empty,
                District: string.Empty,
                item.CentroidLatitude,
                item.CentroidLongitude,
                item.DistanceMeters)));
        }
        else
        {
            throw new Application.Interfaces.ValidationException([
                "Provide a bounding box, intersecting constraint, or proximity filter."
            ]);
        }

        return results
            .Take(request.MaxResults)
            .ToList();
    }

    public async Task<AreaCalculationResultDto> CalculateParcelAreaAsync(
        Guid landParcelId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                p."Id",
                p."AreaValue",
                p."AreaUnit",
                CASE
                    WHEN p."Boundary" IS NOT NULL THEN ST_Area(p."Boundary"::geography)
                    ELSE NULL
                END AS "CalculatedAreaSquareMeters"
            FROM land_intelligence.land_parcels p
            WHERE p."Id" = @landParcelId
            """;

        var connection = _dbContext.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParameter(command, "landParcelId", landParcelId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new LandParcelNotFoundException(landParcelId);
        }

        var calculatedArea = reader.IsDBNull(reader.GetOrdinal("CalculatedAreaSquareMeters"))
            ? (double?)null
            : reader.GetDouble(reader.GetOrdinal("CalculatedAreaSquareMeters"));

        return new AreaCalculationResultDto(
            reader.GetGuid(reader.GetOrdinal("Id")),
            calculatedArea,
            reader.GetDecimal(reader.GetOrdinal("AreaValue")),
            (AreaUnit)reader.GetInt32(reader.GetOrdinal("AreaUnit")),
            calculatedArea.HasValue ? "postgis_boundary" : "stored_attribute_only");
    }

    public async Task<IReadOnlyList<SpatialConstraintDetectionDto>> DetectSpatialConstraintsForParcelAsync(
        Guid landParcelId,
        CancellationToken cancellationToken = default)
    {
        await GetParcelGeometryAsync(landParcelId, cancellationToken);

        const string sql = """
            SELECT
                c."Id",
                c."LandParcelId",
                c."Type",
                c."Description",
                c."Severity",
                CASE
                    WHEN p."Boundary" IS NOT NULL AND ST_Intersects(c."ConstraintGeometry", p."Boundary") THEN TRUE
                    ELSE FALSE
                END AS "IntersectsBoundary",
                CASE
                    WHEN ST_Intersects(c."ConstraintGeometry", p."Centroid") THEN TRUE
                    ELSE FALSE
                END AS "IntersectsCentroid"
            FROM land_intelligence.spatial_constraints c
            INNER JOIN land_intelligence.land_parcels p ON p."Id" = @landParcelId
            WHERE c."ConstraintGeometry" IS NOT NULL
              AND (
                    (p."Boundary" IS NOT NULL AND ST_Intersects(c."ConstraintGeometry", p."Boundary"))
                 OR ST_Intersects(c."ConstraintGeometry", p."Centroid")
              )
            ORDER BY c."Severity" DESC, c."Type"
            """;

        var connection = _dbContext.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParameter(command, "landParcelId", landParcelId);

        var detections = new List<SpatialConstraintDetectionDto>();
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var intersectsBoundary = reader.GetBoolean(reader.GetOrdinal("IntersectsBoundary"));
                var intersectsCentroid = reader.GetBoolean(reader.GetOrdinal("IntersectsCentroid"));

                detections.Add(new SpatialConstraintDetectionDto(
                    reader.GetGuid(reader.GetOrdinal("Id")),
                    reader.GetGuid(reader.GetOrdinal("LandParcelId")),
                    (SpatialConstraintType)reader.GetInt32(reader.GetOrdinal("Type")),
                    reader.GetString(reader.GetOrdinal("Description")),
                    (RestrictionSeverity)reader.GetInt32(reader.GetOrdinal("Severity")),
                    BuildDetectionReason(intersectsBoundary, intersectsCentroid),
                    intersectsBoundary,
                    intersectsCentroid));
            }
        }

        var linkedConstraints = await _dbContext.SpatialConstraints
            .AsNoTracking()
            .Where(constraint => constraint.LandParcelId == landParcelId)
            .ToListAsync(cancellationToken);

        foreach (var linkedConstraint in linkedConstraints)
        {
            if (detections.Any(detection => detection.ConstraintId == linkedConstraint.Id))
            {
                continue;
            }

            detections.Add(new SpatialConstraintDetectionDto(
                linkedConstraint.Id,
                linkedConstraint.LandParcelId,
                linkedConstraint.Type,
                linkedConstraint.Description,
                linkedConstraint.Severity,
                "linked_to_parcel",
                false,
                false));
        }

        return detections;
    }

    public async Task<IReadOnlyList<SpatialConstraintDto>> AnalyzeConstraintsAsync(
        Guid parcelId,
        CancellationToken cancellationToken = default)
    {
        var detections = await DetectSpatialConstraintsForParcelAsync(parcelId, cancellationToken);

        return detections
            .Select(detection => new SpatialConstraintDto(
                detection.ConstraintId,
                detection.LandParcelId,
                detection.Type,
                detection.Description,
                detection.Severity))
            .ToList();
    }

    private async Task<LandParcelEntity> GetParcelGeometryAsync(
        Guid landParcelId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.LandParcels
            .AsNoTracking()
            .Where(parcel => parcel.Id == landParcelId)
            .Select(parcel => new LandParcelEntity
            {
                Id = parcel.Id,
                Centroid = parcel.Centroid,
                Boundary = parcel.Boundary
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new LandParcelNotFoundException(landParcelId);
    }

    private async Task<IReadOnlyList<ParcelIntersectionResultDto>> QueryParcelIntersectionsAsync(
        SpatialConstraintEntity constraint,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                p."Id",
                p."CadastralNumber",
                CASE
                    WHEN p."Boundary" IS NOT NULL AND ST_Intersects(p."Boundary", c."ConstraintGeometry") THEN TRUE
                    ELSE FALSE
                END AS "IntersectsBoundary",
                CASE
                    WHEN ST_Intersects(p."Centroid", c."ConstraintGeometry") THEN TRUE
                    ELSE FALSE
                END AS "IntersectsCentroid"
            FROM land_intelligence.land_parcels p
            CROSS JOIN land_intelligence.spatial_constraints c
            WHERE c."Id" = @constraintId
              AND c."ConstraintGeometry" IS NOT NULL
              AND (
                    (p."Boundary" IS NOT NULL AND ST_Intersects(p."Boundary", c."ConstraintGeometry"))
                 OR ST_Intersects(p."Centroid", c."ConstraintGeometry")
              )
            ORDER BY p."CadastralNumber"
            """;

        var connection = _dbContext.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParameter(command, "constraintId", constraint.Id);

        var results = new List<ParcelIntersectionResultDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new ParcelIntersectionResultDto(
                reader.GetGuid(reader.GetOrdinal("Id")),
                reader.GetString(reader.GetOrdinal("CadastralNumber")),
                constraint.Id,
                reader.GetBoolean(reader.GetOrdinal("IntersectsBoundary")),
                reader.GetBoolean(reader.GetOrdinal("IntersectsCentroid"))));
        }

        return results;
    }

    private async Task<IReadOnlyList<SpatialFilterResultDto>> FilterByBoundingBoxAsync(
        SpatialFilterRequest request,
        CancellationToken cancellationToken)
    {
        var minLongitude = request.MinLongitude!.Value;
        var maxLongitude = request.MaxLongitude!.Value;
        var minLatitude = request.MinLatitude!.Value;
        var maxLatitude = request.MaxLatitude!.Value;

        var parcels = await _dbContext.LandParcels
            .AsNoTracking()
            .Where(parcel =>
                parcel.Centroid.X >= minLongitude
                && parcel.Centroid.X <= maxLongitude
                && parcel.Centroid.Y >= minLatitude
                && parcel.Centroid.Y <= maxLatitude)
            .OrderBy(parcel => parcel.CadastralNumber)
            .Take(request.MaxResults)
            .Select(parcel => new SpatialFilterResultDto(
                parcel.Id,
                parcel.CadastralNumber,
                parcel.Province,
                parcel.District,
                parcel.Centroid.Y,
                parcel.Centroid.X,
                null))
            .ToListAsync(cancellationToken);

        return parcels;
    }

    private async Task<IReadOnlyList<ProximityResultDto>> ExecuteProximityQueryAsync(
        string sql,
        double longitude,
        double latitude,
        double radiusMeters,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParameter(command, "longitude", longitude);
        AddParameter(command, "latitude", latitude);
        AddParameter(command, "radiusMeters", radiusMeters);
        AddParameter(command, "maxResults", maxResults);

        var results = new List<ProximityResultDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new ProximityResultDto(
                reader.GetGuid(reader.GetOrdinal("Id")),
                reader.GetString(reader.GetOrdinal("CadastralNumber")),
                reader.GetDouble(reader.GetOrdinal("Latitude")),
                reader.GetDouble(reader.GetOrdinal("Longitude")),
                reader.GetDouble(reader.GetOrdinal("DistanceMeters"))));
        }

        return results;
    }

    private async Task<bool> ExecuteContainsAsync(
        Point point,
        Geometry polygon,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT ST_Contains(
                ST_GeomFromText(@polygonWkt, 4326),
                ST_SetSRID(ST_MakePoint(@longitude, @latitude), 4326))
            """;

        var connection = _dbContext.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParameter(command, "longitude", point.X);
        AddParameter(command, "latitude", point.Y);
        AddParameter(command, "polygonWkt", polygon.AsText());

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is bool contains && contains;
    }

    private async Task<bool> ExecuteIntersectsAsync(
        Geometry first,
        Geometry second,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT ST_Intersects(
                ST_GeomFromText(@firstGeometryWkt, 4326),
                ST_GeomFromText(@secondGeometryWkt, 4326))
            """;

        var connection = _dbContext.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParameter(command, "firstGeometryWkt", first.AsText());
        AddParameter(command, "secondGeometryWkt", second.AsText());

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is bool intersects && intersects;
    }

    private static bool HasBoundingBox(SpatialFilterRequest request) =>
        request.MinLatitude.HasValue
        && request.MaxLatitude.HasValue
        && request.MinLongitude.HasValue
        && request.MaxLongitude.HasValue;

    private static string DetermineIntersectionType(
        Point centroid,
        MultiPolygon? boundary,
        Geometry constraintGeometry)
    {
        var analysis = PostGisSpatialOperations.AnalyzeIntersection(centroid, boundary, constraintGeometry);

        if (analysis.IntersectsBoundary && analysis.IntersectsCentroid)
        {
            return "boundary_and_centroid";
        }

        if (analysis.IntersectsBoundary)
        {
            return "boundary";
        }

        if (analysis.IntersectsCentroid)
        {
            return "centroid";
        }

        return "none";
    }

    private static string BuildDetectionReason(bool intersectsBoundary, bool intersectsCentroid)
    {
        if (intersectsBoundary && intersectsCentroid)
        {
            return "constraint_intersects_boundary_and_centroid";
        }

        if (intersectsBoundary)
        {
            return "constraint_intersects_boundary";
        }

        if (intersectsCentroid)
        {
            return "constraint_intersects_centroid";
        }

        return "linked_to_parcel";
    }

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
}
