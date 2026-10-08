using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Integrations.ComplaintClassification;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Integrations.EarlyGovernanceReferral;
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

        services.AddComplaintClassifier(configuration);
        services.TryAddSingleton<IEarlyGovernanceReferralHandoff, UnconfiguredEarlyGovernanceReferralHandoff>();

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
            services.AddScoped<IComplianceRuleCatalogue, PostgresComplianceRuleCatalogue>();
            services.AddScoped<IEarlyGovernanceScreeningStore, PostgresEarlyGovernanceScreeningStore>();
            services.AddScoped<IComplaintClassificationAssessmentStore, PostgresComplaintClassificationAssessmentStore>();
        }
        else if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
        {
            services.AddSingleton<IGovernanceAuditRepository, InMemoryGovernanceAuditRepository>();
            services.AddSingleton<IGovernanceEvaluationStore, InMemoryGovernanceEvaluationStore>();
            services.AddSingleton<IComplianceRuleCatalogue, InMemoryComplianceRuleCatalogue>();
            services.AddScoped<IEarlyGovernanceScreeningStore>(_ =>
                throw new InvalidOperationException(
                    "Missing PostgreSQL configuration: Connection string 'GovernanceIntelligenceConnection' or environment variable 'GOVERNANCE_INTELLIGENCE_CONNECTION' is required for early governance screening persistence. In-memory storage is not supported."));
            services.AddScoped<IComplaintClassificationAssessmentStore>(_ =>
                throw new InvalidOperationException(
                    "Missing PostgreSQL configuration: Connection string 'GovernanceIntelligenceConnection' or environment variable 'GOVERNANCE_INTELLIGENCE_CONNECTION' is required for complaint classification persistence. In-memory storage is not supported."));
        }
        else
        {
            throw new InvalidOperationException(
                "Governance Intelligence PostgreSQL connection string 'GOVERNANCE_INTELLIGENCE_CONNECTION' is missing in production environment.");
        }

        return services;
    }

    /// <summary>
    /// Registers the validated configuration and typed HTTP client for the internal complaint classifier.
    /// Registration does not contact the Python service.
    /// </summary>
    public static IServiceCollection AddComplaintClassifier(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IValidateOptions<ComplaintClassifierOptions>, ComplaintClassifierOptionsValidator>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddTransient<ClassifyComplaintCommandHandler>();
        services.AddOptions<ComplaintClassifierOptions>()
            .Bind(configuration.GetSection(ComplaintClassifierOptions.SectionName))
            .ValidateOnStart();

        services.AddHttpClient<IComplaintClassificationClient, HttpComplaintClassificationClient>(
            (serviceProvider, client) =>
            {
                var options = serviceProvider
                    .GetRequiredService<IOptions<ComplaintClassifierOptions>>()
                    .Value;
                if (!ComplaintClassifierOptions.TryCreateBaseUri(
                        options.BaseUrl,
                        out var baseUri,
                        out var validationError))
                {
                    throw new InvalidOperationException(validationError);
                }

                client.BaseAddress = baseUri;
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            });

        return services;
    }
}
