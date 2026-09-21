namespace StateLandGovernance.UnitTests.WorkflowGovernance.Application;

using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Queries;
using StateLandGovernance.WorkflowGovernance.Application.Validators;

public class ApplicationDependencyInjectionTests
{
    [Fact]
    public void AddWorkflowGovernanceApplication_RegistersHandlersAndValidators()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddWorkflowGovernanceApplication();

        // Assert: 4A.1 Command Handlers
        Assert.Contains(services, d =>
            d.ServiceType == typeof(ICommandHandler<RegisterLeaseCaseCommand, LeaseCaseDto>) &&
            d.ImplementationType == typeof(RegisterLeaseCaseCommandHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(RegisterLeaseCaseCommandHandler) &&
            d.ImplementationType == typeof(RegisterLeaseCaseCommandHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        // Assert: 4A.1 Query Handlers
        Assert.Contains(services, d =>
            d.ServiceType == typeof(IQueryHandler<GetLeaseCaseByIdQuery, LeaseCaseDto>) &&
            d.ImplementationType == typeof(GetLeaseCaseByIdQueryHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(GetLeaseCaseByIdQueryHandler) &&
            d.ImplementationType == typeof(GetLeaseCaseByIdQueryHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        // Assert: 4A.1 Validators
        Assert.Contains(services, d =>
            d.ServiceType == typeof(IRequestValidator<RegisterLeaseCaseCommand>) &&
            d.ImplementationType == typeof(RegisterLeaseCaseCommandValidator) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(RegisterLeaseCaseCommandValidator) &&
            d.ImplementationType == typeof(RegisterLeaseCaseCommandValidator) &&
            d.Lifetime == ServiceLifetime.Scoped);

        // Assert: 4A.2 Command Handlers
        Assert.Contains(services, d =>
            d.ServiceType == typeof(ICommandHandler<RegisterGovernedDocumentCommand, GovernedDocumentDto>) &&
            d.ImplementationType == typeof(RegisterGovernedDocumentCommandHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(RegisterGovernedDocumentCommandHandler) &&
            d.ImplementationType == typeof(RegisterGovernedDocumentCommandHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(ICommandHandler<AddDocumentVersionCommand, GovernedDocumentDto>) &&
            d.ImplementationType == typeof(AddDocumentVersionCommandHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(AddDocumentVersionCommandHandler) &&
            d.ImplementationType == typeof(AddDocumentVersionCommandHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        // Assert: 4A.2 Query Handlers
        Assert.Contains(services, d =>
            d.ServiceType == typeof(IQueryHandler<GetGovernedDocumentByIdQuery, GovernedDocumentDto>) &&
            d.ImplementationType == typeof(GetGovernedDocumentByIdQueryHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(GetGovernedDocumentByIdQueryHandler) &&
            d.ImplementationType == typeof(GetGovernedDocumentByIdQueryHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IQueryHandler<GetDocumentsByLeaseCaseIdQuery, IReadOnlyList<GovernedDocumentDto>>) &&
            d.ImplementationType == typeof(GetDocumentsByLeaseCaseIdQueryHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(GetDocumentsByLeaseCaseIdQueryHandler) &&
            d.ImplementationType == typeof(GetDocumentsByLeaseCaseIdQueryHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        // Assert: 4A.2 Validators
        Assert.Contains(services, d =>
            d.ServiceType == typeof(IRequestValidator<RegisterGovernedDocumentCommand>) &&
            d.ImplementationType == typeof(RegisterGovernedDocumentCommandValidator) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(RegisterGovernedDocumentCommandValidator) &&
            d.ImplementationType == typeof(RegisterGovernedDocumentCommandValidator) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IRequestValidator<AddDocumentVersionCommand>) &&
            d.ImplementationType == typeof(AddDocumentVersionCommandValidator) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(AddDocumentVersionCommandValidator) &&
            d.ImplementationType == typeof(AddDocumentVersionCommandValidator) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }
}
