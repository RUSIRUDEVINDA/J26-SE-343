using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.DependencyInjection;

/// <summary>
/// Infrastructure dependency injection extensions for Component 4 (GovernanceIntelligence).
/// Configures PostgreSQL DbContext, IGovernanceAuditRepository, and IGovernanceEvaluationStore persistence with environment-safe fallbacks.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGovernanceIntelligenceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        if (environment is null)
        {
            throw new ArgumentNullException(nameof(environment));
        }

        var connectionString = configuration.GetConnectionString("GovernanceIntelligenceConnection")
            ?? Environment.GetEnvironmentVariable("GOVERNANCE_INTELLIGENCE_CONNECTION");

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<GovernanceIntelligenceDbContext>(options =>
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsHistoryTable("__ef_migrations_history", GovernanceIntelligenceDbContext.SchemaName);
                }));

            services.AddScoped<IGovernanceAuditRepository, PostgresGovernanceAuditRepository>();
            services.AddScoped<IGovernanceEvaluationStore, PostgresGovernanceEvaluationStore>();
        }
        else if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
        {
            services.AddSingleton<IGovernanceAuditRepository, InMemoryGovernanceAuditRepository>();
            services.AddSingleton<IGovernanceEvaluationStore, InMemoryGovernanceEvaluationStore>();
        }
        else
        {
            throw new InvalidOperationException(
                "Governance Intelligence PostgreSQL connection string 'GOVERNANCE_INTELLIGENCE_CONNECTION' is missing in production environment.");
        }

        return services;
    }
}
