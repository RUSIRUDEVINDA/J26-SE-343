using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Infrastructure.PostGIS;
using StateLandGovernance.LandIntelligence.Infrastructure.Repositories;

namespace StateLandGovernance.LandIntelligence.Infrastructure.DependencyInjection;

public static class LandIntelligenceInfrastructureExtensions
{
    public const string ConnectionStringKey = "LandIntelligence:Database";

    public static IServiceCollection AddLandIntelligenceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = Environment.GetEnvironmentVariable("LAND_INTELLIGENCE_CONNECTION")
            ?? configuration.GetConnectionString("LandIntelligence")
            ?? configuration[ConnectionStringKey]
            ?? throw new InvalidOperationException(
                $"Set LAND_INTELLIGENCE_CONNECTION, 'ConnectionStrings:LandIntelligence', or '{ConnectionStringKey}'.");

        services.AddLandIntelligencePostGis(connectionString);

        services.AddScoped<ILandParcelRepository, LandParcelRepository>();
        services.AddScoped<ISpatialConstraintRepository, SpatialConstraintRepository>();

        return services;
    }
}
