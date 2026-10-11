namespace StateLandGovernance.WorkflowGovernance.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Repositories;

public static class DependencyInjection
{
    /// <summary>
    /// Registers WorkflowGovernance infrastructure.
    /// In-memory lease-case persistence is allowed only for Development and Testing —
    /// it is not durable and must not be used as a silent production substitute.
    /// Document and analysis repositories require PostgreSQL via EF Core.
    /// </summary>
    public static IServiceCollection AddWorkflowGovernanceInfrastructure(
        this IServiceCollection services,
        IHostEnvironment environment,
        IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return services.AddWorkflowGovernanceInfrastructure(
            allowInMemoryLeasePersistence: environment.IsDevelopment()
                || environment.IsEnvironment("Testing"),
            configuration);
    }

    /// <summary>
    /// Explicit overload for unit tests and hosts that already decided whether
    /// volatile lease-case storage is acceptable.
    /// </summary>
    public static IServiceCollection AddWorkflowGovernanceInfrastructure(
        this IServiceCollection services,
        bool allowInMemoryLeasePersistence,
        IConfiguration? configuration = null)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IWorkflowInstanceRepository, WorkflowInstanceRepository>();

        var connectionString = WorkflowGovernanceConnectionStringResolver.Resolve(configuration);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "WorkflowGovernance document persistence requires a PostgreSQL connection. "
                + "Set WORKFLOW_GOVERNANCE_CONNECTION or LAND_INTELLIGENCE_CONNECTION.");
        }

        services.AddDbContext<WorkflowGovernanceDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable(
                    "__ef_migrations_history",
                    WorkflowGovernanceDbContext.SchemaName);
            }));

        services.AddScoped<WorkflowGovernancePersistenceSession>();
        services.AddScoped<IGovernedDocumentRepository, GovernedDocumentRepository>();
        services.AddScoped<IDocumentAnalysisRepository, DocumentAnalysisRepository>();
        services.AddScoped<IWorkflowGovernanceUnitOfWork, EfWorkflowGovernanceUnitOfWork>();

        if (!allowInMemoryLeasePersistence)
        {
            throw new InvalidOperationException(
                "WorkflowGovernance lease-case persistence is not configured for this environment. "
                + "In-memory ILeaseCaseRepository / IWorkflowGovernanceUnitOfWork registrations are "
                + "Development and Testing only (process-local, non-durable). "
                + "Provide durable EF (or equivalent) persistence before hosting in Production.");
        }

        services.AddSingleton<ILeaseCaseRepository, InMemoryLeaseCaseRepository>();
        return services;
    }
}
