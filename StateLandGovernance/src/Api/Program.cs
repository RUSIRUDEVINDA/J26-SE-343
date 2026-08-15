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
using Microsoft.EntityFrameworkCore;
using StateLandGovernance.LandIntelligence.Infrastructure.DependencyInjection;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
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
builder.Services.AddLandIntelligenceInfrastructure(builder.Configuration);
// TODO: Register LandIntelligence Application handlers and Presentation
// TODO: Register LeaseFeasibility module (Application + Infrastructure + Presentation)
// TODO: Register WorkflowGovernance module (Application + Infrastructure + Presentation)

// Register GovernanceIntelligence module (Application + Infrastructure + Presentation)
builder.Services.AddSingleton<IRegulatoryComplianceEngine, RegulatoryComplianceEngine>();
builder.Services.AddSingleton<IRegulatoryRuleProvider, InMemoryRegulatoryRuleProvider>();
builder.Services.AddSingleton<IGovernanceAuditRepository, InMemoryGovernanceAuditRepository>();
builder.Services.AddTransient<EvaluateComplianceCommandHandler>();
builder.Services.AddGovernanceConflictDetection();
builder.Services.AddGovernanceRiskIntelligence();
builder.Services.AddExplainableGovernanceEngine();
builder.Services.AddGovernanceConsensusEngine();
builder.Services.AddConditionalGovernanceVerification();


builder.Services.AddControllers();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<LandIntelligenceDbContext>();
    dbContext.Database.Migrate();
}

// TODO: Configure middleware pipeline (exception handling, authentication, etc.)
app.MapControllers();

app.Run();
