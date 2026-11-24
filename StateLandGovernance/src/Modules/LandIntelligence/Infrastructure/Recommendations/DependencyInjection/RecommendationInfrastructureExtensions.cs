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
        var researchServiceUrl = Environment.GetEnvironmentVariable("COLOMBO400_RESEARCH_ML_URL")
            ?? "http://localhost:8502";
        services.AddHttpClient<IMlSuitabilityClient, HttpColombo400ResearchMlClient>(client =>
        {
            client.BaseAddress = new Uri(researchServiceUrl);
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        services.AddScoped<IExperimentalColomboMlClient, NullExperimentalColomboMlClient>();

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
