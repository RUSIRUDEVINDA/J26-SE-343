namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;

using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using StateLandGovernance.Shared.Infrastructure.Configuration;

/// <summary>
/// Design-time DbContext factory for WorkflowGovernanceDbContext, allowing EF Core CLI tools
/// to execute migrations independently of the ASP.NET Core web host execution.
/// </summary>
public sealed class WorkflowGovernanceDbContextFactory : IDesignTimeDbContextFactory<WorkflowGovernanceDbContext>
{
    public WorkflowGovernanceDbContext CreateDbContext(string[] args)
    {
        EnvFileLoader.LoadFromRepositoryRoot();

        var connectionString = Environment.GetEnvironmentVariable("WORKFLOW_GOVERNANCE_CONNECTION")
            ?? Environment.GetEnvironmentVariable("LAND_INTELLIGENCE_CONNECTION")
            ?? throw new InvalidOperationException(
                "Set WORKFLOW_GOVERNANCE_CONNECTION or LAND_INTELLIGENCE_CONNECTION in .env before running design-time EF Core CLI commands.");

        var optionsBuilder = new DbContextOptionsBuilder<WorkflowGovernanceDbContext>();
        optionsBuilder.UseNpgsql(
            connectionString,
            options =>
            {
                options.MigrationsHistoryTable("__ef_migrations_history", WorkflowGovernanceDbContext.SchemaName);
            });

        return new WorkflowGovernanceDbContext(optionsBuilder.Options);
    }
}
