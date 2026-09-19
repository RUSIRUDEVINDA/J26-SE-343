using StateLandGovernance.BuildingBlocks.DependencyInjection;
using StateLandGovernance.WorkflowGovernance.Application;
using StateLandGovernance.WorkflowGovernance.Infrastructure;
using StateLandGovernance.WorkflowGovernance.Presentation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using StateLandGovernance.LandIntelligence.Infrastructure.DependencyInjection;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Presentation;
using StateLandGovernance.LandIntelligence.Presentation.DependencyInjection;
using StateLandGovernance.Shared.Infrastructure.Configuration;

EnvFileLoader.LoadFromRepositoryRoot();

var builder = WebApplication.CreateBuilder(args);

// TODO: Register shared infrastructure services (logging, persistence, security, storage)
// TODO: Register BuildingBlocks (CQRS, events, observability)
builder.Services.AddBuildingBlocks();

// TODO: Register LandIntelligence module (Application + Infrastructure + Presentation)
// TODO: Register LeaseFeasibility module (Application + Infrastructure + Presentation)
// TODO: Register WorkflowGovernance module (Application + Infrastructure + Presentation)

builder.Services
    .AddWorkflowGovernanceApplication()
    .AddWorkflowGovernanceInfrastructure()
    .AddWorkflowGovernancePresentation();

// TODO: Register GovernanceIntelligence module (Application + Infrastructure + Presentation)

// Register GovernanceIntelligence module (Application + Infrastructure + Presentation)
builder.Services.AddSingleton<IRegulatoryComplianceEngine, RegulatoryComplianceEngine>();
builder.Services.AddSingleton<IRegulatoryRuleProvider, InMemoryRegulatoryRuleProvider>();
builder.Services.AddGovernanceIntelligenceInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddTransient<EvaluateComplianceCommandHandler>();
builder.Services.AddGovernanceConflictDetection();
builder.Services.AddGovernanceRiskIntelligence();
builder.Services.AddExplainableGovernanceEngine();
builder.Services.AddGovernanceConsensusEngine();
builder.Services.AddConditionalGovernanceVerification();


builder.Services.AddControllers();
builder.Services.AddLandIntelligenceInfrastructure(builder.Configuration);
builder.Services.AddLandIntelligencePresentation();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(LandIntelligenceApiGroups.External, new OpenApiInfo
    {
        Title = "State Land Governance — Component 1 External Read API",
        Version = "v1",
        Description =
            "Read-only REST contract for external platform modules: land parcel queries, search, spatial constraints, knowledge graph relationships, and explainable recommendations."
    });

    options.SwaggerDoc(LandIntelligenceApiGroups.Internal, new OpenApiInfo
    {
        Title = "State Land Governance — Component 1 Internal Maintenance API",
        Version = "v1",
        Description =
            "Component 1 parcel persistence endpoints. Not for consumption by external platform modules."
    });

    options.DocInclusionPredicate((documentName, apiDescription) =>
        string.Equals(apiDescription.GroupName, documentName, StringComparison.Ordinal));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            $"/swagger/{LandIntelligenceApiGroups.External}/swagger.json",
            "Land Intelligence External Read API v1");
        options.SwaggerEndpoint(
            $"/swagger/{LandIntelligenceApiGroups.Internal}/swagger.json",
            "Land Intelligence Internal API v1");
    });

    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<LandIntelligenceDbContext>();
    dbContext.Database.Migrate();
}

app.UseLandIntelligenceExceptionHandling();
app.MapControllers();

app.Run();
