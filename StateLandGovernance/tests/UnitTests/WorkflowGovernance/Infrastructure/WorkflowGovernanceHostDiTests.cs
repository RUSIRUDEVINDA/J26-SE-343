using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Queries;
using StateLandGovernance.WorkflowGovernance.Infrastructure;
using Xunit;

namespace StateLandGovernance.UnitTests.WorkflowGovernance.Infrastructure;

public sealed class WorkflowGovernanceHostDiTests
{
    [Fact]
    public void AddWorkflowGovernance_ApplicationAndInfrastructure_ResolvesLeaseCaseHandlers()
    {
        var services = new ServiceCollection();
        services.AddWorkflowGovernanceApplication();
        services.AddWorkflowGovernanceInfrastructure(allowInMemoryLeasePersistence: true);

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true
            });

        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        Assert.NotNull(sp.GetRequiredService<ILeaseCaseRepository>());
        Assert.NotNull(sp.GetRequiredService<IWorkflowGovernanceUnitOfWork>());
        Assert.NotNull(sp.GetRequiredService<TimeProvider>());
        Assert.NotNull(sp.GetRequiredService<ICommandHandler<RegisterLeaseCaseCommand, LeaseCaseDto>>());
        Assert.NotNull(sp.GetRequiredService<RegisterLeaseCaseCommandHandler>());
        Assert.NotNull(sp.GetRequiredService<IQueryHandler<GetLeaseCaseByIdQuery, LeaseCaseDto>>());
        Assert.NotNull(sp.GetRequiredService<GetLeaseCaseByIdQueryHandler>());
    }

    [Fact]
    public void AddWorkflowGovernanceInfrastructure_rejects_in_memory_when_not_allowed()
    {
        var services = new ServiceCollection();
        services.AddWorkflowGovernanceApplication();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddWorkflowGovernanceInfrastructure(allowInMemoryLeasePersistence: false));

        Assert.Contains("Development and Testing only", ex.Message, StringComparison.Ordinal);
    }
}
