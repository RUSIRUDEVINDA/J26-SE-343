using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Infrastructure.Integrations;
using StateLandGovernance.LandIntelligence.Infrastructure.Recommendations;
using StateLandGovernance.LandIntelligence.Infrastructure.Recommendations.Criteria;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Recommendations.DependencyInjection;

public static class RecommendationInfrastructureExtensions
{
    public static IServiceCollection AddLandIntelligenceRecommendations(this IServiceCollection services)
    {
        var useColomboResearchModel = string.Equals(
            Environment.GetEnvironmentVariable("COLOMBO400_RESEARCH_ML_ENABLED"),
            "true", StringComparison.OrdinalIgnoreCase);
        if (useColomboResearchModel)
        {
            var researchServiceUrl = Environment.GetEnvironmentVariable("COLOMBO400_RESEARCH_ML_URL")
                ?? "http://localhost:8502";
            services.AddHttpClient<IMlSuitabilityClient, HttpColombo400ResearchMlClient>(client =>
            {
                client.BaseAddress = new Uri(researchServiceUrl);
                client.Timeout = TimeSpan.FromSeconds(5);
            });
        }
        else
        {
            var mlServiceUrl = Environment.GetEnvironmentVariable("ML_SUITABILITY_SERVICE_URL");
            services.AddHttpClient<IMlSuitabilityClient, HttpMlSuitabilityClient>(client =>
            {
                client.BaseAddress = new Uri(mlServiceUrl ?? "http://localhost:8500");
                client.Timeout = TimeSpan.FromSeconds(5);
            });
        }

        services.AddHttpClient<IExperimentalColomboMlClient, HttpExperimentalColomboMlClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ExperimentalColomboMlOptions>>().Value;
            client.BaseAddress = new Uri(options.ServiceBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
        });

        services.AddScoped<IExperimentalColomboMlEvidenceService, ExperimentalColomboMlEvidenceService>();
        services.AddScoped<IRecommendationParcelContext, RecommendationParcelContext>();

        services.AddScoped<IRecommendationCriterionEvaluator, PurposeAlignmentCriterionEvaluator>();
        services.AddScoped<IRecommendationCriterionEvaluator, TargetAreaCriterionEvaluator>();
        services.AddScoped<IRecommendationCriterionEvaluator, LandCategoryCriterionEvaluator>();
        services.AddScoped<IRecommendationCriterionEvaluator, LandUseCriterionEvaluator>();
        services.AddScoped<IRecommendationCriterionEvaluator, LocationPreferenceCriterionEvaluator>();
        services.AddScoped<IRecommendationCriterionEvaluator, RegulatoryCriterionEvaluator>();
        services.AddScoped<IRecommendationCriterionEvaluator, SpatialConstraintCriterionEvaluator>();
        services.AddScoped<IRecommendationCriterionEvaluator, CustomCriteriaEvaluator>();

        services.AddScoped<ILandRecommendationEngine, RuleBasedLandRecommendationEngine>();

        return services;
    }
}
