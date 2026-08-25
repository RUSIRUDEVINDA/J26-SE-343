using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Infrastructure.Neo4j;
using StateLandGovernance.LandIntelligence.Infrastructure.Neo4j.Configuration;
using StateLandGovernance.LandIntelligence.Infrastructure.PostGIS;
using StateLandGovernance.LandIntelligence.Infrastructure.Recommendations.DependencyInjection;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enrichment;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Validation;
using StateLandGovernance.LandIntelligence.Infrastructure.Repositories;

namespace StateLandGovernance.LandIntelligence.Infrastructure.DependencyInjection;

public static class LandIntelligenceInfrastructureExtensions
{
    public static IServiceCollection AddLandIntelligenceInfrastructure(
        this IServiceCollection services,
        IConfiguration _)
    {
        var connectionString = Environment.GetEnvironmentVariable("LAND_INTELLIGENCE_CONNECTION")
            ?? throw new InvalidOperationException(
                "Set LAND_INTELLIGENCE_CONNECTION in .env at the repository root.");

        services.AddLandIntelligencePostGis(connectionString);

        services.AddScoped<ILandParcelRepository, LandParcelRepository>();
        services.AddScoped<ISpatialConstraintRepository, SpatialConstraintRepository>();
        services.AddScoped<ILandRecommendationRepository, LandRecommendationRepository>();
        services.AddScoped<ISpatialAnalysisService, PostGisSpatialAnalysisService>();
        services.AddLandIntelligenceRecommendations();

        if (Neo4jSettings.IsConfigured())
        {
            var neo4jSettings = Neo4jSettings.FromEnvironment();
            services.AddLandIntelligenceNeo4j(neo4jSettings);
            services.AddScoped<IKnowledgeGraphService, Neo4jKnowledgeGraphService>();
        }
        else
        {
            services.AddScoped<IKnowledgeGraphService, UnconfiguredKnowledgeGraphService>();
        }

        services.AddScoped<ILandParcelGraphSynchronizer, LandParcelGraphSynchronizer>();
        services.AddScoped<IGisReferenceDataImportService, GisReferenceDataImportService>();
        services.AddScoped<IGisReferenceDataValidationService, GisReferenceDataValidationService>();
        services.AddScoped<IAdministrativeLocationVerificationService, AdministrativeLocationVerificationService>();
        services.AddScoped<IRoadAccessibilityEnrichmentService, RoadAccessibilityEnrichmentService>();
        services.AddScoped<IWaterProximityEnrichmentService, WaterProximityEnrichmentService>();
        services.AddScoped<ISoilGroupEnrichmentService, SoilGroupEnrichmentService>();

        return services;
    }
}
