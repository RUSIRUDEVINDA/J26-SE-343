namespace StateLandGovernance.WorkflowGovernance.Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Repositories;

public static class DependencyInjection
{
    public static IServiceCollection AddWorkflowGovernanceInfrastructure(this IServiceCollection services)
    {
        // Foundation established: Register Persistence, external adapters
        services.AddScoped<IWorkflowInstanceRepository, WorkflowInstanceRepository>();
        return services;
    }
}
