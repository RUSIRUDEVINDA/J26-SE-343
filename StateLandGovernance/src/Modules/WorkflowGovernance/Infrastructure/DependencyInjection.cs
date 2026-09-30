namespace StateLandGovernance.WorkflowGovernance.Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Repositories;

public static class DependencyInjection
{
    /// <summary>
    /// Registers WorkflowGovernance infrastructure.
    /// In-memory lease-case persistence is allowed only for Development and Testing —
    /// it is not durable and must not be used as a silent production substitute.
    /// </summary>
    public static IServiceCollection AddWorkflowGovernanceInfrastructure(
        this IServiceCollection services,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return services.AddWorkflowGovernanceInfrastructure(
            allowInMemoryLeasePersistence: environment.IsDevelopment()
                || environment.IsEnvironment("Testing"));
    }

    /// <summary>
    /// Explicit overload for unit tests and hosts that already decided whether
    /// volatile lease-case storage is acceptable.
    /// </summary>
    public static IServiceCollection AddWorkflowGovernanceInfrastructure(
        this IServiceCollection services,
        bool allowInMemoryLeasePersistence)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IWorkflowInstanceRepository, WorkflowInstanceRepository>();

        if (!allowInMemoryLeasePersistence)
        {
            throw new InvalidOperationException(
                "WorkflowGovernance lease-case persistence is not configured for this environment. "
                + "In-memory ILeaseCaseRepository / IWorkflowGovernanceUnitOfWork registrations are "
                + "Development and Testing only (process-local, non-durable). "
                + "Provide durable EF (or equivalent) persistence before hosting in Production.");
        }

        services.AddSingleton<ILeaseCaseRepository, InMemoryLeaseCaseRepository>();
        services.AddScoped<IWorkflowGovernanceUnitOfWork, InMemoryWorkflowGovernanceUnitOfWork>();
        return services;
    }
}
