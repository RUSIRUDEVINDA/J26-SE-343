namespace StateLandGovernance.WorkflowGovernance.Application;

using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddWorkflowGovernanceApplication(this IServiceCollection services)
    {
        // Foundation established: Register Application services, handlers, validators
        return services;
    }
}
