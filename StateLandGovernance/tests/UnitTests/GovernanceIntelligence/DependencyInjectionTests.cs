using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.DependencyInjection;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
using StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;
using Xunit;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public class DependencyInjectionTests
{
    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "TestApplication";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    [Fact]
    public void ServiceProvider_ShouldResolveGovernanceIntelligenceController_WhenDependenciesAreRegistered()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var environment = new TestHostEnvironment { EnvironmentName = Environments.Development };

        // Register exactly as configured in Program.cs
        services.AddSingleton<IRegulatoryComplianceEngine, RegulatoryComplianceEngine>();
        services.AddSingleton<IRegulatoryRuleProvider, InMemoryRegulatoryRuleProvider>();
        services.AddGovernanceIntelligenceInfrastructure(configuration, environment);
        services.AddTransient<EvaluateComplianceCommandHandler>();
        services.AddGovernanceConflictDetection();
        services.AddGovernanceRiskIntelligence();
        services.AddExplainableGovernanceEngine();
        services.AddGovernanceConsensusEngine();
        services.AddConditionalGovernanceVerification();

        // Register the controller itself
        services.AddTransient<GovernanceIntelligenceController>();

        // Act
        using var serviceProvider = services.BuildServiceProvider();
        var controller = serviceProvider.GetService<GovernanceIntelligenceController>();
        var auditRepository = serviceProvider.GetService<IGovernanceAuditRepository>();
        var riskEngine = serviceProvider.GetService<IGovernanceRiskEngine>();
        var riskHandler = serviceProvider.GetService<EvaluateGovernanceRiskCommandHandler>();
        var explanationEngine = serviceProvider.GetService<IExplainableGovernanceEngine>();
        var explanationHandler = serviceProvider.GetService<GenerateGovernanceExplanationCommandHandler>();
        var consensusEngine = serviceProvider.GetService<IGovernanceConsensusEngine>();
        var consensusHandler = serviceProvider.GetService<EvaluateGovernanceConsensusCommandHandler>();
        var verificationEngine = serviceProvider.GetService<IConditionalGovernanceVerificationEngine>();
        var verificationHandler = serviceProvider.GetService<EvaluateConditionalVerificationCommandHandler>();

        // Assert
        Assert.NotNull(controller);
        Assert.NotNull(auditRepository);
        Assert.IsType<InMemoryGovernanceAuditRepository>(auditRepository);
        Assert.NotNull(riskEngine);
        Assert.NotNull(riskHandler);
        Assert.NotNull(explanationEngine);
        Assert.NotNull(explanationHandler);
        Assert.NotNull(consensusEngine);
        Assert.NotNull(consensusHandler);
        Assert.NotNull(verificationEngine);
        Assert.NotNull(verificationHandler);
    }

    [Fact]
    public void AddGovernanceIntelligenceInfrastructure_ShouldRegisterPostgresRepository_WhenConnectionStringExists()
    {
        // Arrange
        var services = new ServiceCollection();
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "ConnectionStrings:GovernanceIntelligenceConnection", "Host=localhost;Database=stateland_governance;Username=postgres;Password=postgres" }
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var environment = new TestHostEnvironment { EnvironmentName = Environments.Development };

        // Act
        services.AddGovernanceIntelligenceInfrastructure(configuration, environment);
        using var serviceProvider = services.BuildServiceProvider();
        var repository = serviceProvider.GetService<IGovernanceAuditRepository>();
        var dbContext = serviceProvider.GetService<GovernanceIntelligenceDbContext>();

        // Assert
        Assert.NotNull(repository);
        Assert.IsType<PostgresGovernanceAuditRepository>(repository);
        Assert.NotNull(dbContext);
    }

    [Fact]
    public void AddGovernanceIntelligenceInfrastructure_ShouldFallbackToInMemoryRepository_InDevelopmentWhenNoConnectionString()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var environment = new TestHostEnvironment { EnvironmentName = Environments.Development };

        // Act
        services.AddGovernanceIntelligenceInfrastructure(configuration, environment);
        using var serviceProvider = services.BuildServiceProvider();
        var repository = serviceProvider.GetService<IGovernanceAuditRepository>();
        var dbContext = serviceProvider.GetService<GovernanceIntelligenceDbContext>();

        // Assert
        Assert.NotNull(repository);
        Assert.IsType<InMemoryGovernanceAuditRepository>(repository);
        Assert.Null(dbContext);
    }

    [Fact]
    public void AddGovernanceIntelligenceInfrastructure_ShouldThrowInvalidOperationException_InProductionWhenNoConnectionString()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var environment = new TestHostEnvironment { EnvironmentName = Environments.Production };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddGovernanceIntelligenceInfrastructure(configuration, environment));

        Assert.Contains("GOVERNANCE_INTELLIGENCE_CONNECTION", ex.Message);
    }
}
