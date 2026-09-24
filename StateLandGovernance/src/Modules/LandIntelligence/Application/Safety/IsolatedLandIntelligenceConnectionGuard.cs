using System.Data.Common;
using System.Text.RegularExpressions;
using StateLandGovernance.LandIntelligence.Application.Configuration;

namespace StateLandGovernance.LandIntelligence.Application.Safety;

/// <summary>
/// Prevents Colombo experimental GIS verification from targeting the normal application database.
/// </summary>
public static class IsolatedLandIntelligenceConnectionGuard
{
    public const string IsolatedConnectionEnvironmentVariable = "COLUMBO_EXP_ISOLATED_CONNECTION";

    private static readonly Regex ForbiddenDatabaseNames = new(
        @"^(state_land_governance|postgres|template0|template1)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string RequireIsolatedConnectionString(string? connectionString = null)
    {
        var resolved = connectionString
            ?? Environment.GetEnvironmentVariable(IsolatedConnectionEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(resolved))
        {
            throw new InvalidOperationException(
                $"Set {IsolatedConnectionEnvironmentVariable} to a disposable PostGIS connection string. " +
                $"Do not use LAND_INTELLIGENCE_CONNECTION. Database name must contain " +
                $"'{GisEnrichmentCoverageDefaults.IsolatedDatabaseNameToken}'.");
        }

        Validate(resolved);
        return resolved;
    }

    public static void Validate(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Isolated connection string is empty.");
        }

        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        if (!builder.TryGetValue("Database", out var databaseObj)
            && !builder.TryGetValue("database", out databaseObj))
        {
            throw new InvalidOperationException(
                "Isolated connection string must include a Database value.");
        }

        var database = Convert.ToString(databaseObj)?.Trim() ?? string.Empty;
        if (ForbiddenDatabaseNames.IsMatch(database))
        {
            throw new InvalidOperationException(
                $"Refusing to use protected database '{database}'. " +
                "Create a disposable database whose name contains " +
                $"'{GisEnrichmentCoverageDefaults.IsolatedDatabaseNameToken}'.");
        }

        if (!database.Contains(
                GisEnrichmentCoverageDefaults.IsolatedDatabaseNameToken,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Isolated database name '{database}' must contain " +
                $"'{GisEnrichmentCoverageDefaults.IsolatedDatabaseNameToken}' " +
                "to avoid accidentally targeting the application database.");
        }

        var landIntel = Environment.GetEnvironmentVariable("LAND_INTELLIGENCE_CONNECTION");
        if (!string.IsNullOrWhiteSpace(landIntel))
        {
            try
            {
                var landBuilder = new DbConnectionStringBuilder { ConnectionString = landIntel };
                if (landBuilder.TryGetValue("Database", out var landDbObj)
                    || landBuilder.TryGetValue("database", out landDbObj))
                {
                    var landDb = Convert.ToString(landDbObj)?.Trim() ?? string.Empty;
                    if (ForbiddenDatabaseNames.IsMatch(landDb)
                        && string.Equals(Normalize(landIntel), Normalize(connectionString), StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            "Refusing to run: COLUMBO_EXP_ISOLATED_CONNECTION equals LAND_INTELLIGENCE_CONNECTION " +
                            $"which targets protected database '{landDb}'.");
                    }
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch
            {
                // Ignore unparsable LAND_INTELLIGENCE_CONNECTION; isolated string was already validated.
            }
        }
    }

    private static string Normalize(string connectionString)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        return string.Join(';', builder.Keys.Cast<object>()
            .Select(key =>
            {
                var keyText = Convert.ToString(key) ?? string.Empty;
                return $"{keyText}={builder[keyText]}";
            })
            .OrderBy(part => part, StringComparer.OrdinalIgnoreCase));
    }
}
