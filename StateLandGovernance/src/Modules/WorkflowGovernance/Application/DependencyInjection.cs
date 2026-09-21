namespace StateLandGovernance.WorkflowGovernance.Application;

using System.Collections.Generic;
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
        // 4A.1 Handlers & Validators
        services.AddScoped<ICommandHandler<RegisterLeaseCaseCommand, LeaseCaseDto>, RegisterLeaseCaseCommandHandler>();
        services.AddScoped<RegisterLeaseCaseCommandHandler>();

        services.AddScoped<IQueryHandler<GetLeaseCaseByIdQuery, LeaseCaseDto>, GetLeaseCaseByIdQueryHandler>();
        services.AddScoped<GetLeaseCaseByIdQueryHandler>();

        services.AddScoped<IRequestValidator<RegisterLeaseCaseCommand>, RegisterLeaseCaseCommandValidator>();
        services.AddScoped<RegisterLeaseCaseCommandValidator>();

        // 4A.2 Handlers & Validators
        services.AddScoped<ICommandHandler<RegisterGovernedDocumentCommand, GovernedDocumentDto>, RegisterGovernedDocumentCommandHandler>();
        services.AddScoped<RegisterGovernedDocumentCommandHandler>();

        services.AddScoped<ICommandHandler<AddDocumentVersionCommand, GovernedDocumentDto>, AddDocumentVersionCommandHandler>();
        services.AddScoped<AddDocumentVersionCommandHandler>();

        services.AddScoped<IQueryHandler<GetGovernedDocumentByIdQuery, GovernedDocumentDto>, GetGovernedDocumentByIdQueryHandler>();
        services.AddScoped<GetGovernedDocumentByIdQueryHandler>();

        services.AddScoped<IQueryHandler<GetDocumentsByLeaseCaseIdQuery, IReadOnlyList<GovernedDocumentDto>>, GetDocumentsByLeaseCaseIdQueryHandler>();
        services.AddScoped<GetDocumentsByLeaseCaseIdQueryHandler>();

        services.AddScoped<IRequestValidator<RegisterGovernedDocumentCommand>, RegisterGovernedDocumentCommandValidator>();
        services.AddScoped<RegisterGovernedDocumentCommandValidator>();

        services.AddScoped<IRequestValidator<AddDocumentVersionCommand>, AddDocumentVersionCommandValidator>();
        services.AddScoped<AddDocumentVersionCommandValidator>();

        return services;
    }
}
