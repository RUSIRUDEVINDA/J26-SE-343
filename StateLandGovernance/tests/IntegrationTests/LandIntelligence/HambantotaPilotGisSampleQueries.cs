using System.Data;
using Microsoft.EntityFrameworkCore;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;

namespace StateLandGovernance.IntegrationTests.LandIntelligence;

/// <summary>
/// Read-only GIS sample point discovery for H13 Hambantota pilot validation scenarios.
/// Uses imported LandIntelligence_GIS reference data only.
/// </summary>
internal static class HambantotaPilotGisSampleQueries
{
    public const double OutsideCoverageLatitude = 6.9271;
    public const double OutsideCoverageLongitude = 79.8612;
    private const int DistrictBoundaryType = 2;
    private const int CanalFeatureType = 1;

    public static async Task<(double Latitude, double Longitude)> ReadInteriorPointAsync(
        LandIntelligenceDbContext dbContext)
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

        return await ReadPointAsync(dbContext, sql);
    }

    public static async Task<(double Latitude, double Longitude)> ReadRoadStartPointAsync(
        LandIntelligenceDbContext dbContext)
    {
        const string sql = """
            SELECT
                ST_Y(ST_StartPoint(r."Geometry")) AS "Latitude",
                ST_X(ST_StartPoint(r."Geometry")) AS "Longitude"
            FROM land_intelligence.gis_roads r
            INNER JOIN land_intelligence.gis_administrative_boundaries b
                ON b."Name" = @districtName
               AND b."BoundaryType" = @districtType
               AND ST_Intersects(r."Geometry", b."Boundary")
            WHERE r."Geometry" IS NOT NULL
            LIMIT 1
            """;

        return await ReadPointAsync(dbContext, sql);
    }

    public static async Task<(double Latitude, double Longitude)> ReadWeakestRoadAccessibilityPointAsync(
        LandIntelligenceDbContext dbContext)
    {
        const string sql = """
            WITH district AS (
                SELECT "Boundary"
                FROM land_intelligence.gis_administrative_boundaries
                WHERE "Name" = @districtName
                  AND "BoundaryType" = @districtType
                LIMIT 1
            ),
            sample_points AS (
                SELECT ST_PointOnSurface(ST_Intersection(s."Boundary", d."Boundary")) AS geom
                FROM land_intelligence.gis_soil_groups s
                CROSS JOIN district d
                WHERE s."Boundary" IS NOT NULL
                  AND ST_Intersects(s."Boundary", d."Boundary")
                LIMIT 40
            ),
            distances AS (
                SELECT
                    sp.geom,
                    MIN(ST_Distance(sp.geom::geography, r."Geometry"::geography)) AS road_distance
                FROM sample_points sp
                CROSS JOIN land_intelligence.gis_roads r
                WHERE r."Geometry" IS NOT NULL
                GROUP BY sp.geom
            )
            SELECT
                ST_Y(geom) AS "Latitude",
                ST_X(geom) AS "Longitude",
                road_distance AS "RoadDistanceMeters"
            FROM distances
            ORDER BY road_distance DESC
            LIMIT 1
            """;

        var connection = await OpenConnectionAsync(dbContext);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddDistrictParameters(command);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Expected weakest road accessibility point was not found.");
        }

        return (
            reader.GetDouble(reader.GetOrdinal("Latitude")),
            reader.GetDouble(reader.GetOrdinal("Longitude")));
    }

    public static async Task<(double Latitude, double Longitude, Guid ConservationAreaId, string Name)> ReadConservationSampleAsync(
        LandIntelligenceDbContext dbContext)
    {
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

        var connection = await OpenConnectionAsync(dbContext);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddDistrictParameters(command);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Expected conservation area sample was not found.");
        }

        return (
            reader.GetDouble(reader.GetOrdinal("Latitude")),
            reader.GetDouble(reader.GetOrdinal("Longitude")),
            reader.GetGuid(reader.GetOrdinal("ConservationAreaId")),
            reader.GetString(reader.GetOrdinal("Name")));
    }

    public static async Task<(double Latitude, double Longitude, Guid SoilGroupId, string SoilGroupName)> ReadSoilGroupSampleAsync(
        LandIntelligenceDbContext dbContext)
    {
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

        var connection = await OpenConnectionAsync(dbContext);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddDistrictParameters(command);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Expected soil group sample was not found.");
        }

        return (
            reader.GetDouble(reader.GetOrdinal("Latitude")),
            reader.GetDouble(reader.GetOrdinal("Longitude")),
            reader.GetGuid(reader.GetOrdinal("SoilGroupId")),
            reader.GetString(reader.GetOrdinal("SoilGroupName")));
    }

    public static async Task<(double Latitude, double Longitude)> ReadNaturalWaterSampleAsync(
        LandIntelligenceDbContext dbContext)
    {
        const string sql = """
            SELECT
                ST_Y(ST_StartPoint(w."Geometry")) AS "Latitude",
                ST_X(ST_StartPoint(w."Geometry")) AS "Longitude"
            FROM land_intelligence.gis_water_features w
            INNER JOIN land_intelligence.gis_administrative_boundaries b
                ON b."Name" = @districtName
               AND b."BoundaryType" = @districtType
               AND ST_Intersects(w."Geometry", b."Boundary")
            WHERE w."Geometry" IS NOT NULL
              AND w."FeatureType" = @featureType
            LIMIT 1
            """;

        var connection = await OpenConnectionAsync(dbContext);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddDistrictParameters(command);

        var featureTypeParameter = command.CreateParameter();
        featureTypeParameter.ParameterName = "featureType";
        featureTypeParameter.Value = CanalFeatureType;
        command.Parameters.Add(featureTypeParameter);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Expected natural water feature sample was not found.");
        }

        return (
            reader.GetDouble(reader.GetOrdinal("Latitude")),
            reader.GetDouble(reader.GetOrdinal("Longitude")));
    }

    private static async Task<(double Latitude, double Longitude)> ReadPointAsync(
        LandIntelligenceDbContext dbContext,
        string sql)
    {
        var connection = await OpenConnectionAsync(dbContext);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddDistrictParameters(command);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Expected Hambantota GIS sample point was not found.");
        }

        return (
            reader.GetDouble(reader.GetOrdinal("Latitude")),
            reader.GetDouble(reader.GetOrdinal("Longitude")));
    }

    private static async Task<System.Data.Common.DbConnection> OpenConnectionAsync(LandIntelligenceDbContext dbContext)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        return connection;
    }

    private static void AddDistrictParameters(IDbCommand command)
    {
        var districtNameParameter = command.CreateParameter();
        districtNameParameter.ParameterName = "districtName";
        districtNameParameter.Value = GisReferenceDataPaths.HambantotaDistrictName;
        command.Parameters.Add(districtNameParameter);

        var districtTypeParameter = command.CreateParameter();
        districtTypeParameter.ParameterName = "districtType";
        districtTypeParameter.Value = DistrictBoundaryType;
        command.Parameters.Add(districtTypeParameter);
    }
}
