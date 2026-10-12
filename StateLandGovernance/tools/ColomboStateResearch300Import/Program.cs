using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.RealParcels;

try
{
    var values = Parse(args);
    var apply = values.ContainsKey("apply");
    var connection = ResolveConnection(values.GetValueOrDefault("connection"));
    if (apply && values.ContainsKey("create-database"))
    {
        await EnsureDatabaseAndSchemaAsync(connection!);
    }

    var datasetRoot = values.GetValueOrDefault("dataset-root")
        ?? Path.Combine("data", "gis", "experiments", "colombo", "state_research_300");
    var parcelsCsv = values.GetValueOrDefault("parcels-csv")
        ?? Path.Combine(datasetRoot, "colombo_state_parcels_300_real.csv");
    var boundaries = values.GetValueOrDefault("boundaries-geojson")
        ?? Path.Combine(datasetRoot, "colombo_state_parcels_300_boundaries.geojson");
    var simulated = values.GetValueOrDefault("simulated-lease-csv")
        ?? Path.Combine(datasetRoot, "lease_workflow_SIMULATED_300.csv");

    using var loggerFactory = LoggerFactory.Create(builder => builder.AddSimpleConsole(options => options.SingleLine = true));
    var service = new ColomboStateResearch300ImportService(
        loggerFactory.CreateLogger<ColomboStateResearch300ImportService>());
    var report = await service.RunAsync(new ColomboStateResearch300ImportOptions(
        parcelsCsv,
        boundaries,
        simulated,
        apply,
        connection,
        values.GetValueOrDefault("report"),
        values.ContainsKey("run-gis-enrichment"),
        values.GetValueOrDefault("gis-data-root")));
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
    {
        WriteIndented = true,
    }));
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    Console.Error.WriteLine(
        "Usage: dotnet run --project tools/ColomboStateResearch300Import -- " +
        "[--dataset-root <path>] [--parcels-csv <path>] [--boundaries-geojson <path>] [--simulated-lease-csv <path>] " +
        "[--report <path>] [--dry-run | --apply --connection <connection string or env var name> [--create-database]] " +
        "[--run-gis-enrichment --gis-data-root <path>]");
    return 1;
}

static Dictionary<string, string?> Parse(string[] args)
{
    var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].StartsWith("--"))
        {
            throw new ArgumentException($"Unexpected argument: {args[i]}");
        }

        var key = args[i][2..];
        if (key is "apply" or "dry-run" or "create-database" or "run-gis-enrichment")
        {
            result[key] = null;
        }
        else if (i + 1 >= args.Length || args[i + 1].StartsWith("--"))
        {
            throw new ArgumentException($"--{key} requires a value.");
        }
        else
        {
            result[key] = args[++i];
        }
    }

    if (result.ContainsKey("apply") && result.ContainsKey("dry-run"))
    {
        throw new ArgumentException("Choose --dry-run or --apply, not both.");
    }

    return result;
}

static string? ResolveConnection(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return null;
    }

    return value.Contains('=')
        ? value
        : Environment.GetEnvironmentVariable(value)
            ?? throw new ArgumentException($"Environment variable '{value}' is not set.");
}

static async Task EnsureDatabaseAndSchemaAsync(string connectionString)
{
    ColomboStateResearch300ImportService.AssertResearchDatabase(connectionString);
    var target = new NpgsqlConnectionStringBuilder(connectionString);
    var databaseName = target.Database;
    if (string.IsNullOrWhiteSpace(databaseName))
    {
        throw new ArgumentException("The target database name is missing.");
    }

    var admin = new NpgsqlConnectionStringBuilder(connectionString) { Database = "postgres" };
    await using (var connection = new NpgsqlConnection(admin.ConnectionString))
    {
        await connection.OpenAsync();
        await using var exists = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @name", connection);
        exists.Parameters.AddWithValue("name", databaseName);
        if (await exists.ExecuteScalarAsync() is null)
        {
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
            await create.ExecuteNonQueryAsync();
        }
    }

    var options = new DbContextOptionsBuilder<LandIntelligenceDbContext>()
        .UseNpgsql(connectionString, npgsql =>
        {
            npgsql.UseNetTopologySuite();
            npgsql.MigrationsHistoryTable("__ef_migrations_history", LandIntelligenceDbContext.SchemaName);
        })
        .Options;
    await using var dbContext = new LandIntelligenceDbContext(options);
    await dbContext.Database.MigrateAsync();
}
