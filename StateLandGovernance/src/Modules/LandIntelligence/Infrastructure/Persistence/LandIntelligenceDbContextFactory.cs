using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using StateLandGovernance.LandIntelligence.Infrastructure.PostGIS;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence;

public sealed class LandIntelligenceDbContextFactory : IDesignTimeDbContextFactory<LandIntelligenceDbContext>
{
    public LandIntelligenceDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("LAND_INTELLIGENCE_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=state_land_governance;Username=postgres;Password=postgres";

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
