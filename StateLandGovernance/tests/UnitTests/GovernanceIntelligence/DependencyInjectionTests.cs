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
        services.AddEarlyGovernanceScreening();

        // Register the controller itself
        services.AddTransient<GovernanceIntelligenceController>();

        // Act
        using var serviceProvider = services.BuildServiceProvider();
        var controller = serviceProvider.GetService<GovernanceIntelligenceController>();
        var auditRepository = serviceProvider.GetService<IGovernanceAuditRepository>();
        var evaluationStore = serviceProvider.GetService<IGovernanceEvaluationStore>();
        var riskEngine = serviceProvider.GetService<IGovernanceRiskEngine>();
        var riskHandler = serviceProvider.GetService<EvaluateGovernanceRiskCommandHandler>();
        var explanationEngine = serviceProvider.GetService<IExplainableGovernanceEngine>();
        var explanationHandler = serviceProvider.GetService<GenerateGovernanceExplanationCommandHandler>();
        var consensusEngine = serviceProvider.GetService<IGovernanceConsensusEngine>();
        var consensusHandler = serviceProvider.GetService<EvaluateGovernanceConsensusCommandHandler>();
        var verificationEngine = serviceProvider.GetService<IConditionalGovernanceVerificationEngine>();
        var verificationHandler = serviceProvider.GetService<EvaluateConditionalVerificationCommandHandler>();
        var screeningEngine = serviceProvider.GetService<IEarlyGovernanceScreeningEngine>();
        var screeningHandler = serviceProvider.GetService<ScreenEarlyGovernanceCommandHandler>();

        // Assert
        Assert.NotNull(controller);
        Assert.NotNull(auditRepository);
        Assert.IsType<InMemoryGovernanceAuditRepository>(auditRepository);
        Assert.NotNull(evaluationStore);
        Assert.IsType<InMemoryGovernanceEvaluationStore>(evaluationStore);
        Assert.NotNull(riskEngine);
        Assert.NotNull(riskHandler);
        Assert.NotNull(explanationEngine);
        Assert.NotNull(explanationHandler);
        Assert.NotNull(consensusEngine);
        Assert.NotNull(consensusHandler);
        Assert.NotNull(verificationEngine);
        Assert.NotNull(verificationHandler);
        Assert.NotNull(screeningEngine);
        Assert.IsType<EarlyGovernanceScreeningEngine>(screeningEngine);
        Assert.NotNull(screeningHandler);
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
        var evaluationStore = serviceProvider.GetService<IGovernanceEvaluationStore>();
        var dbContext = serviceProvider.GetService<GovernanceIntelligenceDbContext>();

        // Assert
        Assert.NotNull(repository);
        Assert.IsType<PostgresGovernanceAuditRepository>(repository);
        Assert.NotNull(evaluationStore);
        Assert.IsType<PostgresGovernanceEvaluationStore>(evaluationStore);
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
        var evaluationStore = serviceProvider.GetService<IGovernanceEvaluationStore>();
        var dbContext = serviceProvider.GetService<GovernanceIntelligenceDbContext>();

        // Assert
        Assert.NotNull(repository);
        Assert.IsType<InMemoryGovernanceAuditRepository>(repository);
        Assert.NotNull(evaluationStore);
        Assert.IsType<InMemoryGovernanceEvaluationStore>(evaluationStore);
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

    [Fact]
    public void AddEarlyGovernanceScreening_ShouldResolveEngineAsSingletonAndHandlerAsTransient()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEarlyGovernanceScreening();
        using var serviceProvider = services.BuildServiceProvider();

        var engine1 = serviceProvider.GetService<IEarlyGovernanceScreeningEngine>();
        var engine2 = serviceProvider.GetService<IEarlyGovernanceScreeningEngine>();
        var handler1 = serviceProvider.GetService<ScreenEarlyGovernanceCommandHandler>();
        var handler2 = serviceProvider.GetService<ScreenEarlyGovernanceCommandHandler>();

        // Assert
        Assert.NotNull(engine1);
        Assert.IsType<EarlyGovernanceScreeningEngine>(engine1);
        Assert.Same(engine1, engine2);

        Assert.NotNull(handler1);
        Assert.NotNull(handler2);
        Assert.NotSame(handler1, handler2);
    }

    [Fact]
    public void AddGovernanceIntelligenceInfrastructure_ShouldRegisterPostgresEarlyGovernanceScreeningStore_WhenConnectionStringExists()
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
        using var scope = serviceProvider.CreateScope();

        var store = scope.ServiceProvider.GetService<IEarlyGovernanceScreeningStore>();

        // Assert
        Assert.NotNull(store);
        Assert.IsType<PostgresEarlyGovernanceScreeningStore>(store);
    }

    [Fact]
    public void PostgresEarlyGovernanceScreeningStore_Lifetime_ShouldBeCompatibleWithRequestScopes()
    {
        // Arrange
        var services = new ServiceCollection();
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "ConnectionStrings:GovernanceIntelligenceConnection", "Host=localhost;Database=stateland_governance;Username=postgres;Password=postgres" }
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var environment = new TestHostEnvironment { EnvironmentName = Environments.Development };

        services.AddGovernanceIntelligenceInfrastructure(configuration, environment);
        services.AddEarlyGovernanceScreening();

        // Act: build service provider with scope validation enabled to detect captive dependencies
        using var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        // Assert: Cannot resolve scoped store directly from root provider
        Assert.Throws<InvalidOperationException>(() =>
            serviceProvider.GetRequiredService<IEarlyGovernanceScreeningStore>());

        // Assert: Resolves properly within individual scopes and shares scoped DbContext
        using (var scope1 = serviceProvider.CreateScope())
        {
            var store1 = scope1.ServiceProvider.GetRequiredService<IEarlyGovernanceScreeningStore>();
            var store1Again = scope1.ServiceProvider.GetRequiredService<IEarlyGovernanceScreeningStore>();
            Assert.Same(store1, store1Again);

            using (var scope2 = serviceProvider.CreateScope())
            {
                var store2 = scope2.ServiceProvider.GetRequiredService<IEarlyGovernanceScreeningStore>();
                Assert.NotSame(store1, store2);
            }
        }
    }

    [Fact]
    public void AddGovernanceIntelligenceInfrastructure_ShouldFailClearly_WhenResolvingScreeningStoreWithoutPostgresConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var environment = new TestHostEnvironment { EnvironmentName = Environments.Development };

        // Act
        services.AddGovernanceIntelligenceInfrastructure(configuration, environment);
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        // Assert: Must not return in-memory or no-op store; must throw explicit sanitized configuration error
        var ex = Assert.Throws<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<IEarlyGovernanceScreeningStore>());

        Assert.Contains("GovernanceIntelligenceConnection", ex.Message);
        Assert.Contains("GOVERNANCE_INTELLIGENCE_CONNECTION", ex.Message);
        Assert.Contains("In-memory storage is not supported", ex.Message);
    }
}
