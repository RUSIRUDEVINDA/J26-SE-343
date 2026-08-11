using System;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public class DependencyInjectionTests
{
    [Fact]
    public void ServiceProvider_ShouldResolveGovernanceIntelligenceController_WhenDependenciesAreRegistered()
    {
        // Arrange
        var services = new ServiceCollection();

        // Register exactly as configured in Program.cs
        services.AddSingleton<IRegulatoryComplianceEngine, RegulatoryComplianceEngine>();
        services.AddSingleton<IRegulatoryRuleProvider, InMemoryRegulatoryRuleProvider>();
        services.AddSingleton<IGovernanceAuditRepository, InMemoryGovernanceAuditRepository>();
        services.AddTransient<EvaluateComplianceCommandHandler>();
        services.AddGovernanceConflictDetection();

        // Register the controller itself
        services.AddTransient<GovernanceIntelligenceController>();

        // Act
        using var serviceProvider = services.BuildServiceProvider();
        var controller = serviceProvider.GetService<GovernanceIntelligenceController>();

        // Assert
        Assert.NotNull(controller);
    }
}
