using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;
using StateLandGovernance.LandIntelligence.Application.Services;
using StateLandGovernance.LandIntelligence.Application.Validators;
using StateLandGovernance.LandIntelligence.Infrastructure.Configuration;
using StateLandGovernance.LandIntelligence.Infrastructure.Neo4j;
using StateLandGovernance.LandIntelligence.Infrastructure.Neo4j.Configuration;
using StateLandGovernance.LandIntelligence.Infrastructure.PostGIS;
using StateLandGovernance.LandIntelligence.Infrastructure.Recommendations.DependencyInjection;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enrichment;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Validation;
using StateLandGovernance.LandIntelligence.Infrastructure.PilotValidation;
using StateLandGovernance.LandIntelligence.Infrastructure.LeaseInterest;
using StateLandGovernance.LandIntelligence.Infrastructure.GisCoverage;
using StateLandGovernance.LandIntelligence.Infrastructure.Repositories;

namespace StateLandGovernance.LandIntelligence.Infrastructure.DependencyInjection;

public static class LandIntelligenceInfrastructureExtensions
{
    public static IServiceCollection AddLandIntelligenceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = Environment.GetEnvironmentVariable("LAND_INTELLIGENCE_CONNECTION")
            ?? throw new InvalidOperationException(
                "Set LAND_INTELLIGENCE_CONNECTION in .env at the repository root.");

        services.Configure<GisEnrichmentCoverageOptions>(
            configuration.GetSection(GisEnrichmentCoverageOptions.SectionName));
        services.Configure<ExperimentalColomboMlOptions>(
            configuration.GetSection(ExperimentalColomboMlOptions.SectionName));
        services.Configure<ResearchDemoOptions>(
            configuration.GetSection(ResearchDemoOptions.SectionName));
        services.Configure<LeaseInterestOptions>(
            configuration.GetSection(LeaseInterestOptions.SectionName));
        services.Configure<LeaseInterestPersonalDataOptions>(
            configuration.GetSection(LeaseInterestPersonalDataOptions.SectionName));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<INicPersonalDataProtector, NicPersonalDataProtector>();
        services.AddSingleton<ILeaseInterestOfficerAccessGate, LeaseInterestOfficerAccessGate>();
        services.AddScoped<ILeaseInterestCostAssessmentService, LeaseInterestCostAssessmentService>();
        services.AddScoped<IParcelShortlistService, ParcelShortlistService>();
        services.AddScoped<ILeaseInterestRequestService, LeaseInterestRequestService>();
        services.AddScoped<ILeaseInterestOfficerLookupService, LeaseInterestOfficerLookupService>();
        services.AddScoped<ILeaseInterestOfficerReviewService, LeaseInterestOfficerReviewService>();
        services.AddScoped<ILeaseInterestParcelEligibilityService, LeaseInterestParcelEligibilityService>();
        services.AddScoped<LeaseInterestDraftValidator>(sp =>
            new LeaseInterestDraftValidator(
                sp.GetRequiredService<IOptions<LeaseInterestOptions>>().Value));

        services.AddLandIntelligencePostGis(connectionString);

        services.AddScoped<ILandParcelRepository, LandParcelRepository>();
        services.AddScoped<IRealParcelSourceReadRepository, RealParcelSourceReadRepository>();
        services.AddScoped<IResearchSimulatedLeaseReadRepository, ResearchSimulatedLeaseReadRepository>();
        services.AddScoped<IProjectLeaseReadyDesignationReadRepository, ProjectLeaseReadyDesignationReadRepository>();
        services.AddSingleton<IResearchDemoMode, ResearchDemoMode>();
        services.AddScoped<ISpatialConstraintRepository, SpatialConstraintRepository>();
        services.AddScoped<ILandRecommendationRepository, LandRecommendationRepository>();
        services.AddScoped<ISpatialAnalysisService, PostGisSpatialAnalysisService>();
        services.AddLandIntelligenceRecommendations();

        services.AddScoped<IGisGraphSyncRequestBuilder, GisGraphSyncRequestBuilder>();
        services.AddScoped<IPostGisKnowledgeGraphBaselineProvider, PostGisKnowledgeGraphBaselineProvider>();

        if (Neo4jSettings.IsConfigured())
        {
            var neo4jSettings = Neo4jSettings.FromEnvironment();
            services.AddLandIntelligenceNeo4j(neo4jSettings);
            services.AddScoped<INeo4jKnowledgeGraphService, Neo4jKnowledgeGraphService>();
            services.AddScoped<IKnowledgeGraphService, ResilientKnowledgeGraphService>();
        }
        else
        {
            services.AddScoped<IKnowledgeGraphService, UnconfiguredKnowledgeGraphService>();
        }

        services.AddScoped<ILandParcelGraphSynchronizer, LandParcelGraphSynchronizer>();
        services.AddScoped<IGisReferenceDataImportService, GisReferenceDataImportService>();
        services.AddScoped<IOsmMotorRoadImportService, OsmMotorRoadImportService>();
        services.AddScoped<ColomboExperimentGisImportService>();
        services.AddScoped<IExperimentalColomboMlFeatureAdapter, ExperimentalColomboMlFeatureAdapter>();
        services.AddScoped<IGisReferenceDataValidationService, GisReferenceDataValidationService>();
        services.AddScoped<IAdministrativeLocationVerificationService, AdministrativeLocationVerificationService>();
        services.AddScoped<IRoadAccessibilityEnrichmentService, RoadAccessibilityEnrichmentService>();
        services.AddScoped<IWaterProximityEnrichmentService, WaterProximityEnrichmentService>();
        services.AddScoped<ISoilGroupEnrichmentService, SoilGroupEnrichmentService>();
        services.AddScoped<IEnvironmentalSpatialConstraintEnrichmentService, EnvironmentalSpatialConstraintEnrichmentService>();
        services.AddScoped<ILandParcelGisEnrichmentService, LandParcelGisEnrichmentService>();
        services.AddScoped<ILandParcelGisEnrichmentPersistenceService, LandParcelGisEnrichmentPersistenceService>();
        services.AddScoped<ILandParcelGisKnowledgeGraphSyncService, LandParcelGisKnowledgeGraphSyncService>();
        services.AddScoped<IHambantotaPilotValidationService, HambantotaPilotValidationService>();
        services.AddScoped<ILandIntelligenceGisCoverageService, LandIntelligenceGisCoverageService>();

        return services;
    }
}
