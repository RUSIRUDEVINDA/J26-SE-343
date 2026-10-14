using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using StateLandGovernance.Shared.Infrastructure.Configuration;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;

/// <summary>
/// Design-time DbContext factory for GovernanceIntelligenceDbContext, allowing EF Core CLI tools
/// to execute migrations independently of the ASP.NET Core web host execution.
/// </summary>
public sealed class GovernanceIntelligenceDbContextFactory : IDesignTimeDbContextFactory<GovernanceIntelligenceDbContext>
{
    public GovernanceIntelligenceDbContext CreateDbContext(string[] args)
    {
        EnvFileLoader.LoadFromRepositoryRoot();

        var connectionString = Environment.GetEnvironmentVariable("GOVERNANCE_INTELLIGENCE_CONNECTION")
            ?? throw new InvalidOperationException(
                "Set GOVERNANCE_INTELLIGENCE_CONNECTION environment variable or in .env file before running design-time EF Core CLI commands.");

        var optionsBuilder = new DbContextOptionsBuilder<GovernanceIntelligenceDbContext>();
        optionsBuilder.UseNpgsql(
            connectionString,
            options =>
            {
                options.MigrationsHistoryTable("__ef_migrations_history", GovernanceIntelligenceDbContext.SchemaName);
            });

        return new GovernanceIntelligenceDbContext(optionsBuilder.Options);
    }
}
