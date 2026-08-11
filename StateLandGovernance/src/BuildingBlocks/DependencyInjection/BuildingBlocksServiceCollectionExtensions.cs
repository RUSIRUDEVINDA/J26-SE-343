namespace StateLandGovernance.BuildingBlocks.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.BuildingBlocks.Events;

public static class BuildingBlocksServiceCollectionExtensions
{
    public static IServiceCollection AddBuildingBlocks(this IServiceCollection services)
    {
        services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
        services.AddSingleton<IQueryDispatcher, QueryDispatcher>();
        services.AddSingleton<IEventDispatcher, EventDispatcher>();

        return services;
    }
}
