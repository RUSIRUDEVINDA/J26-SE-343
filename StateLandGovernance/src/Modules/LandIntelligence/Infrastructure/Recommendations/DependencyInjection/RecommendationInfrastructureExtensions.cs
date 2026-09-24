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
        var mlServiceUrl = Environment.GetEnvironmentVariable("ML_SUITABILITY_SERVICE_URL");
        services.AddHttpClient<IMlSuitabilityClient, HttpMlSuitabilityClient>(client =>
        {
            client.BaseAddress = new Uri(mlServiceUrl ?? "http://localhost:8500");
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        services.AddHttpClient<IExperimentalColomboMlClient, HttpExperimentalColomboMlClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ExperimentalColomboMlOptions>>().Value;
            client.BaseAddress = new Uri(options.ServiceBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
        });

        services.AddScoped<IExperimentalColomboMlEvidenceService, ExperimentalColomboMlEvidenceService>();

        services.AddScoped<IRecommendationCriterionEvaluator, PurposeAlignmentCriterionEvaluator>();
        services.AddScoped<IRecommendationCriterionEvaluator, RequiredAreaCriterionEvaluator>();
        services.AddScoped<IRecommendationCriterionEvaluator, LandCategoryCriterionEvaluator>();
        services.AddScoped<IRecommendationCriterionEvaluator, LandUseCriterionEvaluator>();
        services.AddScoped<IRecommendationCriterionEvaluator, LocationPreferenceCriterionEvaluator>();
        services.AddScoped<IRecommendationCriterionEvaluator, AccessibilityCriterionEvaluator>();
        services.AddScoped<IRecommendationCriterionEvaluator, EnvironmentalCriterionEvaluator>();
        services.AddScoped<IRecommendationCriterionEvaluator, RegulatoryCriterionEvaluator>();
        services.AddScoped<IRecommendationCriterionEvaluator, SpatialConstraintCriterionEvaluator>();
        services.AddScoped<IRecommendationCriterionEvaluator, CustomCriteriaEvaluator>();

        services.AddScoped<ILandRecommendationEngine, RuleBasedLandRecommendationEngine>();

        return services;
    }
}
