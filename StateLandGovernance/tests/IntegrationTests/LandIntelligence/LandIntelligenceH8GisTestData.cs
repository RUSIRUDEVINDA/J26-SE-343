using System.Data;
using Microsoft.EntityFrameworkCore;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;

namespace StateLandGovernance.IntegrationTests.LandIntelligence;

/// <summary>
/// Lifecycle helpers for H8 integration tests that may insert synthetic GIS reference rows.
/// Cleanup is keyed on explicit synthetic identifiers so imported LandIntelligence_GIS data is never removed.
/// </summary>
internal static class LandIntelligenceH8GisTestData
{
    public const string SyntheticSourceName = "LandIntelligence_GIS_IntegrationTest";

    public const string SyntheticErosionSourceFeatureId = "H8-TEST-EROSION";

    public const string SyntheticErosionDescription =
        "[SYNTHETIC] H8 integration test erosion observation";

    public static async Task CleanupSyntheticRecordsAsync(LandIntelligenceDbContext dbContext)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            DELETE FROM {LandIntelligenceDbContext.SchemaName}.gis_soil_erosion_observations
            WHERE "SourceFeatureId" = @sourceFeatureId
               OR (
                   "SourceName" = @sourceName
                   AND "Description" LIKE @descriptionPrefix
               )
            """;

        AddParameter(command, "sourceFeatureId", SyntheticErosionSourceFeatureId);
        AddParameter(command, "sourceName", SyntheticSourceName);
        AddParameter(command, "descriptionPrefix", "[SYNTHETIC] H8 integration test%");

        await command.ExecuteNonQueryAsync();
    }

    public static async Task<Guid> InsertSyntheticErosionObservationAsync(
        LandIntelligenceDbContext dbContext,
        double latitude,
        double longitude)
    {
        var featureId = Guid.NewGuid();

        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO {LandIntelligenceDbContext.SchemaName}.gis_soil_erosion_observations
                ("Id", "ObservationClass", "Description", "ErosionRate", "Location",
                 "SourceName", "SourceLayer", "SourceFeatureId", "SourceFingerprint", "ImportedAt")
            VALUES
                (@id, @observationClass, @description, @erosionRate,
                 ST_SetSRID(ST_MakePoint(@longitude, @latitude), 4326),
                 @sourceName, @sourceLayer, @sourceFeatureId, NULL, NOW())
            """;

        AddParameter(command, "id", featureId);
        AddParameter(command, "observationClass", "H8-TEST-SITE");
        AddParameter(command, "description", SyntheticErosionDescription);
        AddParameter(command, "erosionRate", 0.5m);
        AddParameter(command, "longitude", longitude);
        AddParameter(command, "latitude", latitude);
        AddParameter(command, "sourceName", SyntheticSourceName);
        AddParameter(command, "sourceLayer", GisReferenceDataPaths.SoilErosionLayer);
        AddParameter(command, "sourceFeatureId", SyntheticErosionSourceFeatureId);

        await command.ExecuteNonQueryAsync();
        return featureId;
    }

    public static async Task<int> ReadPilotErosionObservationCountAsync(LandIntelligenceDbContext dbContext)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT COUNT(*)::int
            FROM {LandIntelligenceDbContext.SchemaName}.gis_soil_erosion_observations e
            INNER JOIN {LandIntelligenceDbContext.SchemaName}.gis_administrative_boundaries b
                ON b."Name" = @districtName
               AND b."BoundaryType" = @districtType
            WHERE e."Location" IS NOT NULL
              AND ST_Intersects(e."Location", b."Boundary")
            """;

        AddParameter(command, "districtName", GisReferenceDataPaths.HambantotaDistrictName);
        AddParameter(command, "districtType", 2);

        var scalar = await command.ExecuteScalarAsync();
        return Convert.ToInt32(scalar);
    }

    public static async Task<int> ReadSyntheticErosionObservationCountAsync(LandIntelligenceDbContext dbContext)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT COUNT(*)::int
            FROM {LandIntelligenceDbContext.SchemaName}.gis_soil_erosion_observations
            WHERE "SourceFeatureId" = @sourceFeatureId
               OR (
                   "SourceName" = @sourceName
                   AND "Description" LIKE @descriptionPrefix
               )
            """;

        AddParameter(command, "sourceFeatureId", SyntheticErosionSourceFeatureId);
        AddParameter(command, "sourceName", SyntheticSourceName);
        AddParameter(command, "descriptionPrefix", "[SYNTHETIC] H8 integration test%");

        var scalar = await command.ExecuteScalarAsync();
        return Convert.ToInt32(scalar);
    }

    private static void AddParameter(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
