using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace StateLandGovernance.LandIntelligence.Infrastructure.PostGIS;

public static class PostGisConfiguration
{
    public const int DefaultSpatialReferenceSystemId = 4326;

    public static IServiceCollection AddLandIntelligencePostGis(
        this IServiceCollection services,
        string connectionString)
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseNetTopologySuite();
        var dataSource = dataSourceBuilder.Build();

        services.AddSingleton(dataSource);
        services.AddDbContext<Persistence.LandIntelligenceDbContext>(options =>
            options.UseNpgsql(
                dataSource,
                npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsHistoryTable("__ef_migrations_history", Persistence.LandIntelligenceDbContext.SchemaName);
                    npgsqlOptions.UseNetTopologySuite();
                }));

        return services;
    }
}
