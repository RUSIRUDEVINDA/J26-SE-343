using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.LandIntelligence.Application.Commands;
using StateLandGovernance.LandIntelligence.Application.Queries;
using StateLandGovernance.LandIntelligence.Application.Validators;

namespace StateLandGovernance.LandIntelligence.Application.DependencyInjection;

public static class LandIntelligenceApplicationExtensions
{
    public static IServiceCollection AddLandIntelligenceApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateLandParcelCommandHandler>();
        services.AddScoped<UpdateLandParcelCommandHandler>();
        services.AddScoped<GenerateLandRecommendationCommandHandler>();

        services.AddScoped<GetLandParcelByIdQueryHandler>();
        services.AddScoped<SearchLandParcelsQueryHandler>();
        services.AddScoped<GetSpatialConstraintsByParcelIdQueryHandler>();
        services.AddScoped<GetLandRelationshipsQueryHandler>();
        services.AddScoped<SearchLandRecommendationsQueryHandler>();
        services.AddScoped<GetLandRecommendationByIdQueryHandler>();

        services.AddScoped<CreateLandParcelCommandValidator>();
        services.AddScoped<UpdateLandParcelCommandValidator>();
        services.AddScoped<LandSearchRequestValidator>();
        services.AddScoped<LandRecommendationRequestValidator>();
        services.AddScoped<LandRecommendationSearchRequestValidator>();

        return services;
    }
}
