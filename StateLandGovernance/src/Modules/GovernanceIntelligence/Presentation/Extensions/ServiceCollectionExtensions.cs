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

    /// <summary>
    /// Registers Governance Intelligence Phase 4 risk and corruption intelligence services.
    /// </summary>
    public static IServiceCollection AddGovernanceRiskIntelligence(this IServiceCollection services)
    {
        services.TryAddSingleton<IGovernanceRiskEngine, GovernanceRiskEngine>();
        services.TryAddTransient<EvaluateGovernanceRiskCommandHandler>();
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);

        return services;
    }

    /// <summary>
    /// Registers Governance Intelligence Phase 5 explainable governance engine services.
    /// </summary>
    public static IServiceCollection AddExplainableGovernanceEngine(this IServiceCollection services)
    {
        services.TryAddSingleton<IExplainableGovernanceEngine, ExplainableGovernanceEngine>();
        services.TryAddTransient<GenerateGovernanceExplanationCommandHandler>();
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);

        return services;
    }

    /// <summary>
    /// Registers Governance Intelligence Phase 6 multi-institution governance consensus services.
    /// </summary>
    public static IServiceCollection AddGovernanceConsensusEngine(this IServiceCollection services)
    {
        services.TryAddSingleton<IGovernanceConsensusEngine, GovernanceConsensusEngine>();
        services.TryAddTransient<EvaluateGovernanceConsensusCommandHandler>();
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);

        return services;
    }

    /// <summary>
    /// Registers Governance Intelligence Phase 7 conditional governance verification services.
    /// </summary>
    public static IServiceCollection AddConditionalGovernanceVerification(this IServiceCollection services)
    {
        services.TryAddSingleton<IConditionalGovernanceVerificationEngine, ConditionalGovernanceVerificationEngine>();
        services.TryAddTransient<EvaluateConditionalVerificationCommandHandler>();
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);

        return services;
    }
}
