using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Infrastructure.DependencyInjection;
using StateLandGovernance.LandIntelligence.Infrastructure.Neo4j.Configuration;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.Shared.Infrastructure.Configuration;

try
{
    var options = ParseArgs(args);
    EnvFileLoader.LoadFromRepositoryRoot();

    if (!Neo4jSettings.IsConfigured())
    {
        throw new InvalidOperationException(
            "Neo4j is not configured. Set NEO4J_CONNECTION, NEO4J_USERNAME, and NEO4J_PASSWORD in the repository-root .env.");
    }

    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LAND_INTELLIGENCE_CONNECTION")))
    {
        throw new InvalidOperationException(
            "Set LAND_INTELLIGENCE_CONNECTION in the repository-root .env to the PostgreSQL database that holds your parcels.");
    }

    var configuration = new ConfigurationBuilder()
        .AddEnvironmentVariables()
        .Build();

    var services = new ServiceCollection();
    services.AddLogging(builder =>
    {
        builder.AddSimpleConsole(o => o.SingleLine = true);
        builder.SetMinimumLevel(options.Quiet ? LogLevel.Warning : LogLevel.Information);
        builder.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
    });
    services.AddLandIntelligenceInfrastructure(configuration);

    await using var provider = services.BuildServiceProvider();
    await using var scope = provider.CreateAsyncScope();
    var sp = scope.ServiceProvider;

    if (options.ClearGraph && !options.DryRun)
    {
        await ClearNeo4jGraphAsync(sp);
    }

    var db = sp.GetRequiredService<LandIntelligenceDbContext>();
    var parcelRepository = sp.GetRequiredService<ILandParcelRepository>();
    var knowledgeGraph = sp.GetRequiredService<INeo4jKnowledgeGraphService>();
    var gisGraphSync = sp.GetService<ILandParcelGisKnowledgeGraphSyncService>();

    var parcelIds = await QueryParcelIdsAsync(db, options, CancellationToken.None);
    var report = new BackfillReport(
        options.DryRun,
        options.ClearGraph,
        options.IncludeGis,
        options.District,
        options.Limit,
        parcelIds.Count,
        0,
        0,
        []);

    if (options.DryRun)
    {
        WriteReport(report);
        return 0;
    }

    var failures = new List<BackfillFailure>();
    var synced = 0;

    foreach (var parcelId in parcelIds)
    {
        try
        {
            var parcel = await parcelRepository.GetByIdAsync(parcelId, CancellationToken.None);
            if (parcel is null)
            {
                failures.Add(new BackfillFailure(parcelId, null, "Parcel not found in PostgreSQL."));
                continue;
            }

            await knowledgeGraph.SyncLandParcelGraphAsync(parcel, CancellationToken.None);

            if (options.IncludeGis && gisGraphSync is not null)
            {
                var syncRequest = await sp.GetRequiredService<IGisGraphSyncRequestBuilder>()
                    .BuildSyncRequestAsync(parcelId, CancellationToken.None);
                if (syncRequest is not null)
                {
                    await knowledgeGraph.SyncGisDerivedParcelIntelligenceAsync(syncRequest, CancellationToken.None);
                }
            }

            synced++;
            if (!options.Quiet && synced % 25 == 0)
            {
                Console.WriteLine($"Synced {synced}/{parcelIds.Count} parcels…");
            }
        }
        catch (Exception ex)
        {
            failures.Add(new BackfillFailure(parcelId, null, ex.Message));
        }
    }

    report = report with
    {
        Synced = synced,
        Failed = failures.Count,
        Failures = failures.Take(20).ToList(),
    };

    WriteReport(report);
    return failures.Count > 0 ? 2 : 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    Console.Error.WriteLine(
        "Usage: dotnet run --project tools/KnowledgeGraphBackfill -- " +
        "[--dry-run] [--clear-graph] [--include-gis] [--district <name>] [--limit <n>] [--parcel-id <guid>] [--quiet]");
    return 1;
}

static async Task<List<Guid>> QueryParcelIdsAsync(
    LandIntelligenceDbContext db,
    BackfillOptions options,
    CancellationToken cancellationToken)
{
    IQueryable<LandParcelEntity> query = db.LandParcels.AsNoTracking();

    if (!string.IsNullOrWhiteSpace(options.District))
    {
        var district = options.District.Trim();
        query = query.Where(parcel => parcel.District == district);
    }

    if (options.ParcelId is Guid singleId)
    {
        query = query.Where(parcel => parcel.Id == singleId);
    }

    query = query.OrderBy(parcel => parcel.CadastralNumber);

    if (options.Limit > 0)
    {
        query = query.Take(options.Limit);
    }

    return await query.Select(parcel => parcel.Id).ToListAsync(cancellationToken);
}

static async Task ClearNeo4jGraphAsync(IServiceProvider serviceProvider)
{
    var driver = serviceProvider.GetRequiredService<IDriver>();
    await using var session = driver.AsyncSession();
    await session.ExecuteWriteAsync(async tx =>
    {
        await tx.RunAsync("MATCH (n) DETACH DELETE n");
    });
    Console.WriteLine("Cleared all nodes and relationships in Neo4j.");
}

static void WriteReport(BackfillReport report)
{
    Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
}

static BackfillOptions ParseArgs(string[] args)
{
    var dryRun = false;
    var clearGraph = false;
    var includeGis = false;
    var quiet = false;
    string? district = null;
    var limit = 0;
    Guid? parcelId = null;

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i].ToLowerInvariant())
        {
            case "--dry-run":
                dryRun = true;
                break;
            case "--clear-graph":
                clearGraph = true;
                break;
            case "--include-gis":
                includeGis = true;
                break;
            case "--quiet":
                quiet = true;
                break;
            case "--district":
                district = RequireValue(args, ref i, "--district");
                break;
            case "--limit":
                limit = int.Parse(RequireValue(args, ref i, "--limit"), System.Globalization.CultureInfo.InvariantCulture);
                break;
            case "--parcel-id":
                parcelId = Guid.Parse(RequireValue(args, ref i, "--parcel-id"));
                break;
            default:
                throw new ArgumentException($"Unexpected argument: {args[i]}");
        }
    }

    return new BackfillOptions(dryRun, clearGraph, includeGis, quiet, district, limit, parcelId);
}

static string RequireValue(string[] args, ref int index, string flag)
{
    if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
    {
        throw new ArgumentException($"{flag} requires a value.");
    }

    return args[++index];
}

internal sealed record BackfillOptions(
    bool DryRun,
    bool ClearGraph,
    bool IncludeGis,
    bool Quiet,
    string? District,
    int Limit,
    Guid? ParcelId);

internal sealed record BackfillFailure(Guid ParcelId, string? CadastralNumber, string Error);

internal sealed record BackfillReport(
    bool DryRun,
    bool ClearedGraph,
    bool IncludedGis,
    string? DistrictFilter,
    int Limit,
    int CandidateCount,
    int Synced,
    int Failed,
    IReadOnlyList<BackfillFailure> Failures);
