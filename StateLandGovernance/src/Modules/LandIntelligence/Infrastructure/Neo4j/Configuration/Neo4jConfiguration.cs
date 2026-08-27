using Microsoft.Extensions.DependencyInjection;
using Neo4j.Driver;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Neo4j.Configuration;

public static class Neo4jConfiguration
{
    public static IServiceCollection AddLandIntelligenceNeo4j(
        this IServiceCollection services,
        Neo4jSettings settings)
    {
        services.AddSingleton(settings);
        services.AddSingleton<IDriver>(_ =>
            GraphDatabase.Driver(settings.Uri, AuthTokens.Basic(settings.Username, settings.Password)));

        return services;
    }
}
