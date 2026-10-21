using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Application.Projections;
using StateLandGovernance.GovernanceIntelligence.Application.Queries;
using StateLandGovernance.GovernanceIntelligence.Application.Screening.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Application.Timing;
using StateLandGovernance.GovernanceIntelligence.Application.WorkflowAnomaly;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Integrations.Blockchain;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Integrations.ComplaintClassification;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Integrations.EarlyGovernanceReferral;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Integrations.Explainability;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Integrations.WorkflowAnomaly;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.BackgroundServices;
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
        services.AddWorkflowAnomalyClient(configuration);
        services.AddGroqExplainability(configuration);
        var enablePp1SyntheticIntegration = environment.IsDevelopment() &&
            configuration.GetValue<bool>("GovernanceIntelligence:Pp1:EnableSyntheticIntegration", false);
        if (enablePp1SyntheticIntegration)
        {
            services.AddSingleton<IEarlyGovernanceReferralHandoff, SyntheticPp1ReferralHandoff>();
            services.AddSingleton<IEarlyGovernanceReferralPolicy, Pp1EarlyGovernanceReferralPolicy>();
            services.AddTransient<ApplyPp1SeedScenarioCommandHandler>();
        }
        else
        {
            services.TryAddSingleton<IEarlyGovernanceReferralHandoff, UnconfiguredEarlyGovernanceReferralHandoff>();
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
            services.AddScoped<IGovernanceAuditCaseHistoryReader, PostgresGovernanceAuditRepository>();
            services.AddScoped<IGovernanceEvaluationStore, PostgresGovernanceEvaluationStore>();
            services.AddScoped<IComplianceRuleCatalogue, PostgresComplianceRuleCatalogue>();
            services.AddScoped<IConsolidatedGovernanceScreeningStore, PostgresConsolidatedGovernanceScreeningStore>();
            services.AddScoped<IEarlyGovernanceScreeningStore, PostgresEarlyGovernanceScreeningStore>();
            services.AddScoped<IComplaintClassificationAssessmentStore, PostgresComplaintClassificationAssessmentStore>();
            services.AddScoped<IComponent1GovernanceCaseProjectionStore, PostgresComponent1GovernanceCaseProjectionStore>();
            services.AddScoped<IComponent3WorkflowGovernanceProjectionStore, PostgresComponent3WorkflowGovernanceProjectionStore>();
            services.AddScoped<IWorkflowAnomalyAssessmentStore, PostgresWorkflowAnomalyAssessmentStore>();
            services.AddScoped<ILeaseApprovalTimingAssessmentStore, PostgresLeaseApprovalTimingAssessmentStore>();
            services.AddScoped<IGovernanceAuditAnchorStore, PostgresGovernanceAuditAnchorStore>();
            services.AddScoped<IGovernanceDashboardReader, GovernanceDashboardReader>();
        }
        else if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
        {
            services.AddSingleton<IGovernanceAuditRepository, InMemoryGovernanceAuditRepository>();
            services.AddSingleton<IGovernanceEvaluationStore, InMemoryGovernanceEvaluationStore>();
            services.AddSingleton<IComplianceRuleCatalogue, InMemoryComplianceRuleCatalogue>();
            services.AddSingleton<IGovernanceAuditAnchorStore, InMemoryGovernanceAuditAnchorStore>();
            services.AddSingleton<IGovernanceDashboardReader, InMemoryGovernanceDashboardReader>();
            services.AddScoped<IEarlyGovernanceScreeningStore>(_ =>
                throw new InvalidOperationException(
                    "Missing PostgreSQL configuration: Connection string 'GovernanceIntelligenceConnection' or environment variable 'GOVERNANCE_INTELLIGENCE_CONNECTION' is required for early governance screening persistence. In-memory storage is not supported."));
            services.AddScoped<IComplaintClassificationAssessmentStore>(_ =>
                throw new InvalidOperationException(
                    "Missing PostgreSQL configuration: Connection string 'GovernanceIntelligenceConnection' or environment variable 'GOVERNANCE_INTELLIGENCE_CONNECTION' is required for complaint classification persistence. In-memory storage is not supported."));
            services.AddScoped<IComponent1GovernanceCaseProjectionStore>(_ =>
                throw new InvalidOperationException(
                    "Missing PostgreSQL configuration: Connection string 'GovernanceIntelligenceConnection' or environment variable 'GOVERNANCE_INTELLIGENCE_CONNECTION' is required for Component 1 case projection persistence. In-memory storage is not supported."));
            services.AddScoped<IComponent3WorkflowGovernanceProjectionStore>(_ =>
                throw new InvalidOperationException(
                    "Missing PostgreSQL configuration: Connection string 'GovernanceIntelligenceConnection' or environment variable 'GOVERNANCE_INTELLIGENCE_CONNECTION' is required for Component 3 workflow projection persistence. In-memory storage is not supported."));
            services.AddScoped<IWorkflowAnomalyAssessmentStore>(_ =>
                throw new InvalidOperationException(
                    "Missing PostgreSQL configuration: Connection string 'GovernanceIntelligenceConnection' or environment variable 'GOVERNANCE_INTELLIGENCE_CONNECTION' is required for workflow anomaly assessment persistence. In-memory storage is not supported."));
            services.AddScoped<ILeaseApprovalTimingAssessmentStore>(_ =>
                throw new InvalidOperationException(
                    "Missing PostgreSQL configuration: Connection string 'GovernanceIntelligenceConnection' or environment variable 'GOVERNANCE_INTELLIGENCE_CONNECTION' is required for lease approval timing assessment persistence. In-memory storage is not supported."));
        }
        else
        {
            throw new InvalidOperationException(
                "Governance Intelligence PostgreSQL connection string 'GOVERNANCE_INTELLIGENCE_CONNECTION' is missing in production environment.");
        }

        services.TryAddSingleton<IConsolidatedGovernanceScreeningStore, InMemoryConsolidatedGovernanceScreeningStore>();
        services.AddScoped<IApprovedScreeningPolicySource>(sp => new ApprovedScreeningPolicySource(sp.GetRequiredService<IComplianceRuleCatalogue>(), configuration));

        services.AddTransient<GetAuditReceiptQueryHandler>();
        services.AddTransient<GetAuditHistoryByCaseQueryHandler>();
        services.AddTransient<VerifyAuditIntegrityQueryHandler>();
        services.AddTransient<GetGovernanceDashboardQueryHandler>();
        services.AddTransient<AnchorAuditRecordCommandHandler>();
        services.AddTransient<GetComplaintClassificationAssessmentQueryHandler>();
        services.AddTransient<GetComplaintClassificationHistoryQueryHandler>();

        services.AddHttpClient<IBlockchainAuditAnchorService, HttpBlockchainAuditAnchorService>(
            (serviceProvider, client) =>
            {
                var cfg = serviceProvider.GetRequiredService<IConfiguration>();
                var baseUrl = cfg.GetValue<string>("GovernanceIntelligence:Blockchain:BaseUrl") ?? "http://localhost:8550/";
                if (!baseUrl.EndsWith('/')) baseUrl += "/";
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            });

        // Dispatches persisted audit hashes asynchronously; evaluations remain independent of ledger availability.
        services.AddHostedService<GovernanceAuditAnchorOutboxProcessor>();

        return services;
    }

    public static IServiceCollection AddGroqExplainability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IValidateOptions<GroqExplainabilityOptions>, GroqExplainabilityOptionsValidator>();
        services.AddOptions<GroqExplainabilityOptions>()
            .Bind(configuration.GetSection(GroqExplainabilityOptions.SectionName))
            .PostConfigure(options =>
            {
                if (string.IsNullOrWhiteSpace(options.Token))
                    options.Token = Environment.GetEnvironmentVariable("GROQ_API_KEY") ?? string.Empty;
            })
            .ValidateOnStart();

        services.AddHttpClient<IGovernanceExplanationSimplifier, GroqGovernanceExplanationSimplifier>(
            (serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<GroqExplainabilityOptions>>().Value;
                if (!GroqExplainabilityOptions.TryCreateBaseUri(options.BaseUrl, out var baseUri))
                    throw new InvalidOperationException($"{GroqExplainabilityOptions.SectionName}:BaseUrl is invalid.");
                client.BaseAddress = baseUri;
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            });

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

    /// <summary>
    /// Registers the validated configuration and typed HTTP client for the workflow anomaly detector.
    /// Registration does not contact the Python service.
    /// </summary>
    public static IServiceCollection AddWorkflowAnomalyClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IValidateOptions<WorkflowAnomalyOptions>, WorkflowAnomalyOptionsValidator>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddTransient<AssessWorkflowAnomalyCommandHandler>();
        services.AddTransient<GetWorkflowAnomalyAssessmentQueryHandler>();
        services.AddTransient<GetWorkflowAnomalyHistoryByCaseQueryHandler>();
        services.AddTransient<GetWorkflowAnomalyHistoryByWorkflowRunQueryHandler>();
        services.AddTransient<GetWorkflowProjectionJourneyByCaseQueryHandler>();
        services.AddOptions<WorkflowAnomalyOptions>()
            .Bind(configuration.GetSection(WorkflowAnomalyOptions.SectionName))
            .ValidateOnStart();

        services.AddHttpClient<IWorkflowAnomalyClient, HttpWorkflowAnomalyClient>(
            (serviceProvider, client) =>
            {
                var options = serviceProvider
                    .GetRequiredService<IOptions<WorkflowAnomalyOptions>>()
                    .Value;
                if (!WorkflowAnomalyOptions.TryCreateBaseUri(
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
