using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Queries;
using StateLandGovernance.WorkflowGovernance.Infrastructure;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Repositories;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Services;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Storage;
using StateLandGovernance.WorkflowGovernance.Presentation;
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

        // Stubs for Batch 4A.2–4A.5 downstream ports deferred to Batch 4B infrastructure
        services.AddScoped<IGovernedDocumentRepository>(_ => null!);
        services.AddScoped<IDocumentAnalysisRepository>(_ => null!);
        services.AddScoped<IDocumentCompletenessAssessmentRepository>(_ => null!);
        services.AddScoped<IProposalTemplateProvider>(_ => null!);
        services.AddScoped<IDocumentRequirementProvider>(_ => null!);
        services.AddScoped<IComponent4ScreeningGateway>(_ => null!);

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

    [Fact]
    public void NormalProductionComposition_WithDemoDisabled_BuildsSuccessfullyWithoutDemoGraph()
    {
        var services = new ServiceCollection();

        // 1. Simulate Production environment and configuration with demo disabled
        var environment = new StubHostEnvironment { EnvironmentName = "Production" };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WorkflowGovernance:OcrDemo:Enabled"] = "false"
            })
            .Build();

        var isOcrDemoEnabled = environment.IsDevelopment()
            && configuration.GetValue<bool>("WorkflowGovernance:OcrDemo:Enabled", false);

        // 2. Supply durable repository placeholders that production provides (Batch 4B)
        services.AddScoped<ILeaseCaseRepository>(_ => null!);
        services.AddScoped<IWorkflowGovernanceUnitOfWork>(_ => null!);
        services.AddScoped<IGovernedDocumentRepository>(_ => null!);
        services.AddScoped<IDocumentAnalysisRepository>(_ => null!);
        services.AddScoped<IDocumentCompletenessAssessmentRepository>(_ => null!);
        services.AddScoped<IProposalTemplateProvider>(_ => null!);
        services.AddScoped<IDocumentRequirementProvider>(_ => null!);
        services.AddScoped<IComponent4ScreeningGateway>(_ => null!);

        // 3. Register production modules
        services.AddLogging();
        services.AddWorkflowGovernanceApplication();
        services.AddWorkflowGovernanceInfrastructure(environment);
        services.AddWorkflowGovernancePresentation(configuration);

        // When demo is disabled, demo storage and document intelligence must NOT be registered
        if (isOcrDemoEnabled)
        {
            services.AddWorkflowGovernanceLocalDocumentStorage(configuration);
            services.AddWorkflowGovernanceDocumentIntelligence(configuration);
        }

        // 4. Build with strict validation
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        // 5. Assert: No in-memory demo repositories
        Assert.Null(services.FirstOrDefault(d => d.ImplementationType == typeof(InMemoryGovernedDocumentRepository)));
        Assert.Null(services.FirstOrDefault(d => d.ImplementationType == typeof(InMemoryDocumentAnalysisRepository)));

        // 6. Assert: No local document store
        Assert.Null(provider.GetService<IDocumentContentReader>());
        Assert.Null(provider.GetService<IDocumentContentWriter>());

        // 7. Assert: No local artifact store
        Assert.Null(provider.GetService<IAnalysisArtifactWriter>());
        Assert.Null(provider.GetService<IAnalysisArtifactReader>());

        // 8. Assert: No temporary OCR execution service requiring missing demo storage
        Assert.Null(provider.GetService<IDocumentAnalysisExecutionService>());
        Assert.Null(provider.GetService<IDocumentIntelligenceService>());
    }

    [Fact]
    public void DevelopmentDemoComposition_WithDemoEnabled_ResolvesCompleteDemoGraph()
    {
        var services = new ServiceCollection();

        // 1. Simulate Development environment and configuration with demo enabled
        var environment = new StubHostEnvironment { EnvironmentName = "Development" };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WorkflowGovernance:OcrDemo:Enabled"] = "true",
                ["WorkflowGovernance:DocumentIntelligence:BaseUrl"] = "http://127.0.0.1:8009",
                ["WorkflowGovernance:LocalStorage:RootPath"] = ".test-data/workflow-governance"
            })
            .Build();

        var isOcrDemoEnabled = environment.IsDevelopment()
            && configuration.GetValue<bool>("WorkflowGovernance:OcrDemo:Enabled", false);

        // 2. Register development demo modules
        services.AddLogging();
        services.AddWorkflowGovernanceApplication();
        services.AddWorkflowGovernanceInfrastructure(environment);
        services.AddWorkflowGovernancePresentation(configuration);

        if (isOcrDemoEnabled)
        {
            services.AddWorkflowGovernanceLocalDocumentStorage(configuration);
            services.AddWorkflowGovernanceDocumentIntelligence(configuration);
        }

        // 3. Build with strict validation
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        // 4. Assert complete demo graph resolves to exact expected types
        var docRepo = sp.GetRequiredService<IGovernedDocumentRepository>();
        Assert.IsType<InMemoryGovernedDocumentRepository>(docRepo);

        var analysisRepo = sp.GetRequiredService<IDocumentAnalysisRepository>();
        Assert.IsType<InMemoryDocumentAnalysisRepository>(analysisRepo);

        var contentReader = sp.GetRequiredService<IDocumentContentReader>();
        Assert.IsType<LocalFileDocumentContentStore>(contentReader);

        var contentWriter = sp.GetRequiredService<IDocumentContentWriter>();
        Assert.IsType<LocalFileDocumentContentStore>(contentWriter);

        var artifactWriter = sp.GetRequiredService<IAnalysisArtifactWriter>();
        Assert.IsType<LocalFileAnalysisArtifactStore>(artifactWriter);

        var artifactReader = sp.GetRequiredService<IAnalysisArtifactReader>();
        Assert.IsType<LocalFileAnalysisArtifactStore>(artifactReader);

        var intelligenceService = sp.GetRequiredService<IDocumentIntelligenceService>();
        Assert.IsType<FastApiDocumentIntelligenceService>(intelligenceService);

        var executionService = sp.GetRequiredService<IDocumentAnalysisExecutionService>();
        Assert.IsType<DocumentAnalysisExecutionService>(executionService);
    }

    [Fact]
    public void PreExistingRepositories_AreNotOverriddenByInfrastructureRegistration()
    {
        var services = new ServiceCollection();

        // Register custom/durable repository implementations before Infrastructure
        var customDocRepo = new CustomTestGovernedDocumentRepository();
        var customAnalysisRepo = new CustomTestDocumentAnalysisRepository();
        services.AddSingleton<IGovernedDocumentRepository>(customDocRepo);
        services.AddSingleton<IDocumentAnalysisRepository>(customAnalysisRepo);

        services.AddWorkflowGovernanceApplication();
        services.AddWorkflowGovernanceInfrastructure(allowInMemoryLeasePersistence: true);

        using var provider = services.BuildServiceProvider();

        var resolvedDocRepo = provider.GetRequiredService<IGovernedDocumentRepository>();
        var resolvedAnalysisRepo = provider.GetRequiredService<IDocumentAnalysisRepository>();

        // Custom registrations MUST NOT be overwritten by TryAdd
        Assert.Same(customDocRepo, resolvedDocRepo);
        Assert.Same(customAnalysisRepo, resolvedAnalysisRepo);
    }

    private sealed class CustomTestGovernedDocumentRepository : IGovernedDocumentRepository
    {
        public Task<StateLandGovernance.WorkflowGovernance.Domain.Documents.GovernedDocument?> GetByIdAsync(
            StateLandGovernance.WorkflowGovernance.Domain.Documents.GovernedDocumentId id,
            CancellationToken cancellationToken = default) => Task.FromResult<StateLandGovernance.WorkflowGovernance.Domain.Documents.GovernedDocument?>(null);

        public Task<IReadOnlyList<StateLandGovernance.WorkflowGovernance.Domain.Documents.GovernedDocument>> GetByLeaseCaseIdAsync(
            StateLandGovernance.WorkflowGovernance.Domain.LeaseCases.LeaseCaseId leaseCaseId,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<StateLandGovernance.WorkflowGovernance.Domain.Documents.GovernedDocument>>(Array.Empty<StateLandGovernance.WorkflowGovernance.Domain.Documents.GovernedDocument>());

        public Task AddAsync(
            StateLandGovernance.WorkflowGovernance.Domain.Documents.GovernedDocument document,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class CustomTestDocumentAnalysisRepository : IDocumentAnalysisRepository
    {
        public Task<StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis.DocumentAnalysis?> GetByIdAsync(
            StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis.DocumentAnalysisId id,
            CancellationToken cancellationToken = default) => Task.FromResult<StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis.DocumentAnalysis?>(null);

        public Task<StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis.DocumentAnalysis?> GetByDocumentVersionIdAsync(
            StateLandGovernance.WorkflowGovernance.Domain.Documents.DocumentVersionId documentVersionId,
            CancellationToken cancellationToken = default) => Task.FromResult<StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis.DocumentAnalysis?>(null);

        public Task AddAsync(
            StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis.DocumentAnalysis documentAnalysis,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "StateLandGovernance.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
