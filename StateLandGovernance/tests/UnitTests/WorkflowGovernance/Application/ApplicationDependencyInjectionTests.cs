namespace StateLandGovernance.UnitTests.WorkflowGovernance.Application;

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

        // Assert: Command Handlers
        Assert.Contains(services, d =>
            d.ServiceType == typeof(ICommandHandler<RegisterLeaseCaseCommand, LeaseCaseDto>) &&
            d.ImplementationType == typeof(RegisterLeaseCaseCommandHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(RegisterLeaseCaseCommandHandler) &&
            d.ImplementationType == typeof(RegisterLeaseCaseCommandHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        // Assert: Query Handlers
        Assert.Contains(services, d =>
            d.ServiceType == typeof(IQueryHandler<GetLeaseCaseByIdQuery, LeaseCaseDto>) &&
            d.ImplementationType == typeof(GetLeaseCaseByIdQueryHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(GetLeaseCaseByIdQueryHandler) &&
            d.ImplementationType == typeof(GetLeaseCaseByIdQueryHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        // Assert: Validators
        Assert.Contains(services, d =>
            d.ServiceType == typeof(IRequestValidator<RegisterLeaseCaseCommand>) &&
            d.ImplementationType == typeof(RegisterLeaseCaseCommandValidator) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(RegisterLeaseCaseCommandValidator) &&
            d.ImplementationType == typeof(RegisterLeaseCaseCommandValidator) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }
}
