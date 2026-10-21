using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.RealParcels;

try
{
    var values = Parse(args);
    if (!values.TryGetValue("source-zip", out var sourceZip)) throw new ArgumentException("--source-zip is required.");
    var apply = values.ContainsKey("apply");
    var connection = ResolveConnection(values.GetValueOrDefault("connection"));
    if (apply && values.ContainsKey("create-database"))
    {
        await EnsureDatabaseAndSchemaAsync(connection!);
    }
    int? limit = values.TryGetValue("limit", out var rawLimit) ? int.Parse(rawLimit!) : null;
    using var loggerFactory = LoggerFactory.Create(builder => builder.AddSimpleConsole(options => options.SingleLine = true));
    var service = new ColomboCadastreParcelImportService(loggerFactory.CreateLogger<ColomboCadastreParcelImportService>());
    var report = await service.RunAsync(new ColomboCadastreImportOptions(
        sourceZip!, values.GetValueOrDefault("district") ?? "Colombo", limit, values.GetValueOrDefault("gn-code"), apply, connection, values.GetValueOrDefault("report")));
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    Console.Error.WriteLine("Usage: dotnet run --project tools/ColomboCadastreImport -- --source-zip <path> [--district Colombo] [--gn-code <code>] [--limit <n>] [--report <path>] [--dry-run | --apply --connection <connection string or env var name> [--create-database]]");
    return 1;
}

static Dictionary<string, string?> Parse(string[] args)
{
    var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].StartsWith("--")) throw new ArgumentException($"Unexpected argument: {args[i]}");
        var key = args[i][2..];
        if (key is "apply" or "dry-run" or "create-database") result[key] = null;
        else if (i + 1 >= args.Length || args[i + 1].StartsWith("--")) throw new ArgumentException($"--{key} requires a value.");
        else result[key] = args[++i];
    }
    if (result.ContainsKey("apply") && result.ContainsKey("dry-run")) throw new ArgumentException("Choose --dry-run or --apply, not both.");
    return result;
}

static string? ResolveConnection(string? value)
{
    if (string.IsNullOrWhiteSpace(value)) return null;
    return value.Contains('=') ? value : Environment.GetEnvironmentVariable(value) ?? throw new ArgumentException($"Environment variable '{value}' is not set.");
}

static async Task EnsureDatabaseAndSchemaAsync(string connectionString)
{
    var target = new NpgsqlConnectionStringBuilder(connectionString);
    var databaseName = target.Database;
    if (string.IsNullOrWhiteSpace(databaseName) || !System.Text.RegularExpressions.Regex.IsMatch(databaseName, "^[A-Za-z0-9_]+$"))
        throw new ArgumentException("The target database name is missing or unsafe.");

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
