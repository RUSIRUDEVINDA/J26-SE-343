using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Infrastructure.Recommendations;
using StateLandGovernance.LandIntelligence.Infrastructure.Recommendations.Criteria;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Recommendations.DependencyInjection;

public static class RecommendationInfrastructureExtensions
{
    public static IServiceCollection AddLandIntelligenceRecommendations(this IServiceCollection services)
    {
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
