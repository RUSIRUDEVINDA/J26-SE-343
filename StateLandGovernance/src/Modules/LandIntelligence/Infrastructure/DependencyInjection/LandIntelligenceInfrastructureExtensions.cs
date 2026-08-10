using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Infrastructure.PostGIS;
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

        return services;
    }
}
