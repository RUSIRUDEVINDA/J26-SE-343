namespace StateLandGovernance.WorkflowGovernance.Application;

using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Queries;
using StateLandGovernance.WorkflowGovernance.Application.Validators;

public static class DependencyInjection
{
    public static IServiceCollection AddWorkflowGovernanceApplication(this IServiceCollection services)
    {
        // Handlers
        services.AddScoped<ICommandHandler<RegisterLeaseCaseCommand, LeaseCaseDto>, RegisterLeaseCaseCommandHandler>();
        services.AddScoped<RegisterLeaseCaseCommandHandler>();

        services.AddScoped<IQueryHandler<GetLeaseCaseByIdQuery, LeaseCaseDto>, GetLeaseCaseByIdQueryHandler>();
        services.AddScoped<GetLeaseCaseByIdQueryHandler>();

        // Validators
        services.AddScoped<IRequestValidator<RegisterLeaseCaseCommand>, RegisterLeaseCaseCommandValidator>();
        services.AddScoped<RegisterLeaseCaseCommandValidator>();

        return services;
    }
}
