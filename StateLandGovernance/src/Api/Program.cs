using StateLandGovernance.BuildingBlocks.DependencyInjection;
using StateLandGovernance.WorkflowGovernance.Application;
using StateLandGovernance.WorkflowGovernance.Infrastructure;
using StateLandGovernance.WorkflowGovernance.Presentation;

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

var app = builder.Build();

// TODO: Configure middleware pipeline (exception handling, authentication, etc.)
// TODO: Map module controllers and endpoints

app.Run();
