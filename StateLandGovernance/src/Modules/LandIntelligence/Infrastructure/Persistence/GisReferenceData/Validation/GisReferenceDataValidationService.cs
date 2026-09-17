using System.Data;
using Microsoft.EntityFrameworkCore;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;
using StateLandGovernance.LandIntelligence.Infrastructure.PostGIS;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Validation;

public sealed class GisReferenceDataValidationService : IGisReferenceDataValidationService
{
    private const int ExpresswayRoadType = 5;
    private const int ProvinceBoundaryType = 1;
    private const int DistrictBoundaryType = 2;

    private static readonly IReadOnlyList<GisReferenceTableSpec> TableSpecs =
    [
        new("gis_administrative_boundaries", "Boundary", ["ST_MultiPolygon"]),
        new("gis_roads", "Geometry", ["ST_MultiLineString"]),
        new("gis_water_features", "Geometry",
        [
            "ST_Polygon",
            "ST_MultiPolygon",
            "ST_LineString",
            "ST_MultiLineString"
        ]),
        new("gis_soil_groups", "Boundary", ["ST_MultiPolygon"]),
        new("gis_soil_conservation_areas", "Boundary", ["ST_MultiPolygon"]),
        new("gis_soil_erosion_observations", "Location", ["ST_Point"])
    ];

    private readonly LandIntelligenceDbContext _dbContext;

    public GisReferenceDataValidationService(LandIntelligenceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GisReferenceDataValidationResult> ValidateHambantotaPilotAsync(
        CancellationToken cancellationToken = default)
    {
        var issues = new List<string>();
        var tableSummaries = new Dictionary<string, GisReferenceTableValidationSummary>();
        var tableCounts = new Dictionary<string, int>();

        foreach (var spec in TableSpecs)
        {
            var summary = await ValidateTableGeometryAsync(spec, cancellationToken);
            tableSummaries[spec.TableName] = summary;
            tableCounts[spec.TableName] = summary.TotalRows;

            AppendGeometryIssues(spec.TableName, summary, issues);
        }

        var hambantotaValidation = await ValidateHambantotaSpatialRelationshipsAsync(cancellationToken);
        AppendSpatialIssues(hambantotaValidation, issues);

        var nearestRoadDistance = await ValidateNearestRoadDistanceAsync(cancellationToken);
        if (!nearestRoadDistance.RoadFound)
        {
            issues.Add("No nearest GIS road could be found for the Hambantota test point.");
        }
        else if (nearestRoadDistance.DistanceMeters is null or < 0)
        {
            issues.Add("Nearest GIS road distance must be a non-negative meter value.");
        }

        return new GisReferenceDataValidationResult
        {
            IsValid = issues.Count == 0,
            TableCounts = tableCounts,
            TableSummaries = tableSummaries,
            HambantotaSpatialValidation = hambantotaValidation,
            NearestRoadDistance = nearestRoadDistance,
            Issues = issues
        };
    }

    private async Task<GisReferenceTableValidationSummary> ValidateTableGeometryAsync(
        GisReferenceTableSpec spec,
        CancellationToken cancellationToken)
    {
        var totalRows = await ExecuteScalarIntAsync(
            $"""SELECT COUNT(*) FROM {QualifiedTable(spec.TableName)}""",
            cancellationToken);

        var nullGeometryCount = await ExecuteScalarIntAsync(
            $"""SELECT COUNT(*) FROM {QualifiedTable(spec.TableName)} WHERE "{spec.GeometryColumn}" IS NULL""",
            cancellationToken);

        var wrongSridCount = await ExecuteScalarIntAsync(
            $"""
            SELECT COUNT(*)
            FROM {QualifiedTable(spec.TableName)}
            WHERE "{spec.GeometryColumn}" IS NOT NULL
              AND ST_SRID("{spec.GeometryColumn}") <> {PostGisConfiguration.DefaultSpatialReferenceSystemId}
            """,
            cancellationToken);

        var invalidGeometryCount = await ExecuteScalarIntAsync(
            $"""
            SELECT COUNT(*)
            FROM {QualifiedTable(spec.TableName)}
            WHERE "{spec.GeometryColumn}" IS NOT NULL
              AND NOT ST_IsValid("{spec.GeometryColumn}")
            """,
            cancellationToken);

        var allowedTypesSql = string.Join(
            ", ",
            spec.AllowedGeometryTypes.Select(type => $"'{type}'"));

        var unexpectedGeometryTypeCount = await ExecuteScalarIntAsync(
            $"""
            SELECT COUNT(*)
            FROM {QualifiedTable(spec.TableName)}
            WHERE "{spec.GeometryColumn}" IS NOT NULL
              AND ST_GeometryType("{spec.GeometryColumn}") NOT IN ({allowedTypesSql})
            """,
            cancellationToken);

        var dominantGeometryType = await ExecuteScalarStringAsync(
            $"""
            SELECT ST_GeometryType("{spec.GeometryColumn}")
            FROM {QualifiedTable(spec.TableName)}
            WHERE "{spec.GeometryColumn}" IS NOT NULL
            GROUP BY 1
            ORDER BY COUNT(*) DESC
            LIMIT 1
            """,
            cancellationToken) ?? "none";

        var gistIndexExists = await ExecuteScalarIntAsync(
            """
            SELECT COUNT(*)
            FROM pg_indexes
            WHERE schemaname = @schemaName
              AND tablename = @tableName
              AND indexdef ILIKE '%USING gist%'
              AND indexdef ILIKE @geometryColumnPattern
            """,
            cancellationToken,
            ("schemaName", LandIntelligenceDbContext.SchemaName),
            ("tableName", spec.TableName),
            ("geometryColumnPattern", $"%\"{spec.GeometryColumn}\"%")) > 0;

        return new GisReferenceTableValidationSummary
        {
            GeometryColumn = spec.GeometryColumn,
            DominantGeometryType = dominantGeometryType,
            TotalRows = totalRows,
            NullGeometryCount = nullGeometryCount,
            WrongSridCount = wrongSridCount,
            InvalidGeometryCount = invalidGeometryCount,
            UnexpectedGeometryTypeCount = unexpectedGeometryTypeCount,
            GistIndexExists = gistIndexExists
        };
    }

    private async Task<GisReferenceHambantotaSpatialValidation> ValidateHambantotaSpatialRelationshipsAsync(
        CancellationToken cancellationToken)
    {
        var hambantotaExists = await ExecuteScalarIntAsync(
            """
            SELECT COUNT(*)
            FROM land_intelligence.gis_administrative_boundaries
            WHERE "Name" = @districtName
              AND "BoundaryType" = @districtType
              AND "Boundary" IS NOT NULL
            """,
            cancellationToken,
            ("districtName", GisReferenceDataPaths.HambantotaDistrictName),
            ("districtType", DistrictBoundaryType)) > 0;

        var provinceExists = await ExecuteScalarIntAsync(
            """
            SELECT COUNT(*)
            FROM land_intelligence.gis_administrative_boundaries
            WHERE "Name" = @provinceName
              AND "BoundaryType" = @provinceType
              AND "Boundary" IS NOT NULL
            """,
            cancellationToken,
            ("provinceName", "Southern"),
            ("provinceType", ProvinceBoundaryType)) > 0;

        var districtIntersectsProvince = await ExecuteScalarIntAsync(
            """
            SELECT COUNT(*)
            FROM land_intelligence.gis_administrative_boundaries district
            INNER JOIN land_intelligence.gis_administrative_boundaries province
                ON province."Name" = @provinceName
               AND province."BoundaryType" = @provinceType
            WHERE district."Name" = @districtName
              AND district."BoundaryType" = @districtType
              AND ST_Intersects(district."Boundary", province."Boundary")
            """,
            cancellationToken,
            ("districtName", GisReferenceDataPaths.HambantotaDistrictName),
            ("districtType", DistrictBoundaryType),
            ("provinceName", "Southern"),
            ("provinceType", ProvinceBoundaryType)) > 0;

        var roadsOutside = await CountFeaturesOutsideHambantotaAsync("gis_roads", "Geometry", cancellationToken);
        var waterOutside = await CountFeaturesOutsideHambantotaAsync("gis_water_features", "Geometry", cancellationToken);
        var soilGroupsOutside = await CountFeaturesOutsideHambantotaAsync("gis_soil_groups", "Boundary", cancellationToken);
        var conservationOutside = await CountFeaturesOutsideHambantotaAsync(
            "gis_soil_conservation_areas",
            "Boundary",
            cancellationToken);
        var erosionOutside = await CountFeaturesOutsideHambantotaAsync(
            "gis_soil_erosion_observations",
            "Location",
            cancellationToken);

        var expresswaysIntersecting = await CountIntersectingFeaturesAsync(
            "gis_roads",
            "Geometry",
            """AND "RoadType" = @roadType""",
            cancellationToken,
            ("roadType", ExpresswayRoadType));

        var waterIntersecting = await CountIntersectingFeaturesAsync("gis_water_features", "Geometry", cancellationToken: cancellationToken);
        var soilGroupsIntersecting = await CountIntersectingFeaturesAsync("gis_soil_groups", "Boundary", cancellationToken: cancellationToken);
        var conservationIntersecting = await CountIntersectingFeaturesAsync(
            "gis_soil_conservation_areas",
            "Boundary",
            cancellationToken: cancellationToken);
        var erosionIntersecting = await CountIntersectingFeaturesAsync(
            "gis_soil_erosion_observations",
            "Location",
            cancellationToken: cancellationToken);

        return new GisReferenceHambantotaSpatialValidation
        {
            HambantotaDistrictExists = hambantotaExists,
            SouthernProvinceExists = provinceExists,
            DistrictIntersectsProvince = districtIntersectsProvince,
            RoadsOutsideHambantotaCount = roadsOutside,
            WaterFeaturesOutsideHambantotaCount = waterOutside,
            SoilGroupsOutsideHambantotaCount = soilGroupsOutside,
            SoilConservationAreasOutsideHambantotaCount = conservationOutside,
            ErosionObservationsOutsideHambantotaCount = erosionOutside,
            ExpresswaysIntersectingHambantotaCount = expresswaysIntersecting,
            WaterFeaturesIntersectingHambantotaCount = waterIntersecting,
            SoilGroupsIntersectingHambantotaCount = soilGroupsIntersecting,
            SoilConservationAreasIntersectingHambantotaCount = conservationIntersecting,
            ErosionObservationsIntersectingHambantotaCount = erosionIntersecting
        };
    }

    private async Task<GisReferenceNearestRoadDistanceResult> ValidateNearestRoadDistanceAsync(
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH hambantota AS (
                SELECT ST_PointOnSurface("Boundary") AS test_point
                FROM land_intelligence.gis_administrative_boundaries
                WHERE "Name" = @districtName
                  AND "BoundaryType" = @districtType
                LIMIT 1
            ),
            nearest AS (
                SELECT
                    r."Name",
                    ST_Distance(h.test_point::geography, r."Geometry"::geography) AS distance_meters
                FROM hambantota h
                INNER JOIN land_intelligence.gis_roads r ON r."Geometry" IS NOT NULL
                ORDER BY r."Geometry"::geography <-> h.test_point::geography
                LIMIT 1
            )
            SELECT
                ST_Y((SELECT test_point FROM hambantota)) AS "Latitude",
                ST_X((SELECT test_point FROM hambantota)) AS "Longitude",
                nearest."Name" AS "RoadName",
                nearest.distance_meters AS "DistanceMeters"
            FROM nearest
            """;

        var connection = _dbContext.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParameter(command, "districtName", GisReferenceDataPaths.HambantotaDistrictName);
        AddParameter(command, "districtType", DistrictBoundaryType);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new GisReferenceNearestRoadDistanceResult
            {
                TestLatitude = 0,
                TestLongitude = 0,
                TestPointSource = "ST_PointOnSurface(Hambantota district boundary)",
                RoadFound = false,
                DistanceMeters = null,
                NearestRoadName = null
            };
        }

        return new GisReferenceNearestRoadDistanceResult
        {
            TestLatitude = reader.GetDouble(reader.GetOrdinal("Latitude")),
            TestLongitude = reader.GetDouble(reader.GetOrdinal("Longitude")),
            TestPointSource = "ST_PointOnSurface(Hambantota district boundary)",
            RoadFound = true,
            DistanceMeters = reader.GetDouble(reader.GetOrdinal("DistanceMeters")),
            NearestRoadName = reader.IsDBNull(reader.GetOrdinal("RoadName"))
                ? null
                : reader.GetString(reader.GetOrdinal("RoadName"))
        };
    }

    private async Task<int> CountFeaturesOutsideHambantotaAsync(
        string tableName,
        string geometryColumn,
        CancellationToken cancellationToken)
    {
        return await ExecuteScalarIntAsync(
            $"""
            WITH hambantota AS (
                SELECT "Boundary"
                FROM land_intelligence.gis_administrative_boundaries
                WHERE "Name" = @districtName
                  AND "BoundaryType" = @districtType
                LIMIT 1
            )
            SELECT COUNT(*)
            FROM {QualifiedTable(tableName)} feature
            CROSS JOIN hambantota
            WHERE feature."{geometryColumn}" IS NOT NULL
              AND NOT ST_Intersects(feature."{geometryColumn}", hambantota."Boundary")
            """,
            cancellationToken,
            ("districtName", GisReferenceDataPaths.HambantotaDistrictName),
            ("districtType", DistrictBoundaryType));
    }

    private async Task<int> CountIntersectingFeaturesAsync(
        string tableName,
        string geometryColumn,
        string? additionalFilterSql = null,
        CancellationToken cancellationToken = default,
        params (string Name, object Value)[] parameters)
    {
        additionalFilterSql ??= string.Empty;

        var allParameters = new List<(string Name, object Value)>
        {
            ("districtName", GisReferenceDataPaths.HambantotaDistrictName),
            ("districtType", DistrictBoundaryType)
        };
        allParameters.AddRange(parameters);

        return await ExecuteScalarIntAsync(
            $"""
            WITH hambantota AS (
                SELECT "Boundary"
                FROM land_intelligence.gis_administrative_boundaries
                WHERE "Name" = @districtName
                  AND "BoundaryType" = @districtType
                LIMIT 1
            )
            SELECT COUNT(*)
            FROM {QualifiedTable(tableName)} feature
            CROSS JOIN hambantota
            WHERE feature."{geometryColumn}" IS NOT NULL
              AND ST_Intersects(feature."{geometryColumn}", hambantota."Boundary")
              {additionalFilterSql}
            """,
            cancellationToken,
            allParameters.ToArray());
    }

    private static void AppendGeometryIssues(
        string tableName,
        GisReferenceTableValidationSummary summary,
        ICollection<string> issues)
    {
        if (summary.NullGeometryCount > 0)
        {
            issues.Add($"{tableName}: {summary.NullGeometryCount} null geometry row(s).");
        }

        if (summary.WrongSridCount > 0)
        {
            issues.Add($"{tableName}: {summary.WrongSridCount} row(s) not in SRID 4326.");
        }

        if (summary.InvalidGeometryCount > 0)
        {
            issues.Add($"{tableName}: {summary.InvalidGeometryCount} invalid geometry row(s).");
        }

        if (summary.UnexpectedGeometryTypeCount > 0)
        {
            issues.Add($"{tableName}: {summary.UnexpectedGeometryTypeCount} unexpected geometry type row(s).");
        }

        if (!summary.GistIndexExists)
        {
            issues.Add($"{tableName}: missing GIST index on {summary.GeometryColumn}.");
        }
    }

    private static void AppendSpatialIssues(
        GisReferenceHambantotaSpatialValidation validation,
        ICollection<string> issues)
    {
        if (!validation.HambantotaDistrictExists)
        {
            issues.Add("Hambantota district boundary is missing.");
        }

        if (!validation.SouthernProvinceExists)
        {
            issues.Add("Southern Province boundary is missing.");
        }

        if (!validation.DistrictIntersectsProvince)
        {
            issues.Add("Hambantota district does not intersect Southern Province.");
        }

        if (validation.RoadsOutsideHambantotaCount > 0)
        {
            issues.Add($"{validation.RoadsOutsideHambantotaCount} road(s) fall outside Hambantota.");
        }

        if (validation.WaterFeaturesOutsideHambantotaCount > 0)
        {
            issues.Add($"{validation.WaterFeaturesOutsideHambantotaCount} water feature(s) fall outside Hambantota.");
        }

        if (validation.SoilGroupsOutsideHambantotaCount > 0)
        {
            issues.Add($"{validation.SoilGroupsOutsideHambantotaCount} soil group(s) fall outside Hambantota.");
        }

        if (validation.SoilConservationAreasOutsideHambantotaCount > 0)
        {
            issues.Add($"{validation.SoilConservationAreasOutsideHambantotaCount} soil conservation area(s) fall outside Hambantota.");
        }

        if (validation.ErosionObservationsOutsideHambantotaCount > 0)
        {
            issues.Add($"{validation.ErosionObservationsOutsideHambantotaCount} erosion observation(s) fall outside Hambantota.");
        }
    }

    private async Task<int> ExecuteScalarIntAsync(
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        var result = await ExecuteScalarAsync(sql, cancellationToken, parameters);
        return Convert.ToInt32(result ?? 0);
    }

    private async Task<string?> ExecuteScalarStringAsync(
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        var result = await ExecuteScalarAsync(sql, cancellationToken, parameters);
        return result?.ToString();
    }

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

    private static string QualifiedTable(string tableName) =>
        $"{LandIntelligenceDbContext.SchemaName}.{tableName}";

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

    private sealed record GisReferenceTableSpec(
        string TableName,
        string GeometryColumn,
        IReadOnlyList<string> AllowedGeometryTypes);
}
