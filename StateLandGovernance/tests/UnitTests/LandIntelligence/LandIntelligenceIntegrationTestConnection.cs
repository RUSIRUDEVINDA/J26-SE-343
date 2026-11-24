using System.Data.Common;
using Npgsql;

namespace StateLandGovernance.UnitTests.LandIntelligence;

/// <summary>
/// Guards Land Intelligence integration tests so they only run against disposable test databases.
/// </summary>
internal static class LandIntelligenceIntegrationTestConnection
{
    public const string ConnectionEnvironmentVariable = "LAND_INTELLIGENCE_TEST_CONNECTION";

    private const string RequiredDatabasePrefix = "land_intelligence_test_";

    private static readonly string[] ForbiddenDatabaseNames =
    [
        "land_intel_colombo_state_research",
        "land_intel_colombo_real_demo",
        "land_intel_colombo_exp_iso",
        "state_land_governance",
        "postgres",
        "template0",
        "template1",
    ];

    /// <summary>
    /// Returns a validated test connection string, or null when the env var is unset (test should skip).
    /// </summary>
    public static string? TryGetValidatedConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        Validate(connectionString);
        return connectionString;
    }

    public static void Validate(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"{ConnectionEnvironmentVariable} is empty.");
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var database = builder.Database?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(database))
        {
            throw new InvalidOperationException(
                $"{ConnectionEnvironmentVariable} must include a Database value.");
        }

        foreach (var forbidden in ForbiddenDatabaseNames)
        {
            if (string.Equals(database, forbidden, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Refusing to run Land Intelligence integration tests against protected database '{database}'. " +
                    $"Use a disposable database whose name starts with '{RequiredDatabasePrefix}'.");
            }
        }

        if (!database.StartsWith(RequiredDatabasePrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Refusing to run Land Intelligence integration tests against '{database}'. " +
                $"Database name must start with '{RequiredDatabasePrefix}'.");
        }

        var applicationConnection = Environment.GetEnvironmentVariable("LAND_INTELLIGENCE_CONNECTION");
        if (!string.IsNullOrWhiteSpace(applicationConnection))
        {
            try
            {
                var appBuilder = new DbConnectionStringBuilder { ConnectionString = applicationConnection };
                if (appBuilder.TryGetValue("Database", out var appDbObj)
                    || appBuilder.TryGetValue("database", out appDbObj))
                {
                    var appDb = Convert.ToString(appDbObj)?.Trim() ?? string.Empty;
                    if (string.Equals(appDb, database, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            $"{ConnectionEnvironmentVariable} must not target the same database as " +
                            $"LAND_INTELLIGENCE_CONNECTION ('{appDb}'). Create an isolated test database.");
                    }
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch
            {
                // Ignore unparsable application connection string.
            }
        }
    }
}
