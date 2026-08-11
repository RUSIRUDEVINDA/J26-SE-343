using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up Governance Intelligence dependencies in an IServiceCollection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Governance Intelligence Phase 3 conflict detection and time services.
    /// </summary>
    public static IServiceCollection AddGovernanceConflictDetection(this IServiceCollection services)
    {
        services.TryAddSingleton<IGovernanceConflictEngine, GovernanceConflictEngine>();
        services.TryAddTransient<DetectConflictsCommandHandler>();
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);

        return services;
    }
}
