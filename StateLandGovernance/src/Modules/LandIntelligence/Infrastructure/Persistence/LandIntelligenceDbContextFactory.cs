using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using StateLandGovernance.LandIntelligence.Infrastructure.PostGIS;
using StateLandGovernance.Shared.Infrastructure.Configuration;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence;

public sealed class LandIntelligenceDbContextFactory : IDesignTimeDbContextFactory<LandIntelligenceDbContext>
{
    public LandIntelligenceDbContext CreateDbContext(string[] args)
    {
        EnvFileLoader.LoadFromRepositoryRoot();

        var connectionString = Environment.GetEnvironmentVariable("LAND_INTELLIGENCE_CONNECTION")
            ?? throw new InvalidOperationException(
                "Set LAND_INTELLIGENCE_CONNECTION in .env before running EF Core design-time commands.");

        var optionsBuilder = new DbContextOptionsBuilder<LandIntelligenceDbContext>();
        optionsBuilder.UseNpgsql(
            connectionString,
            options =>
            {
                options.MigrationsHistoryTable("__ef_migrations_history", LandIntelligenceDbContext.SchemaName);
                options.UseNetTopologySuite();
            });

        return new LandIntelligenceDbContext(optionsBuilder.Options);
    }
}
