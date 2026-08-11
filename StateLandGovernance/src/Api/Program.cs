using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// TODO: Register shared infrastructure services (logging, persistence, security, storage)
// TODO: Register BuildingBlocks (CQRS, events, observability)
// TODO: Register LandIntelligence module (Application + Infrastructure + Presentation)
// TODO: Register LeaseFeasibility module (Application + Infrastructure + Presentation)
// TODO: Register WorkflowGovernance module (Application + Infrastructure + Presentation)

// Register GovernanceIntelligence module (Application + Infrastructure + Presentation)
builder.Services.AddSingleton<IRegulatoryComplianceEngine, RegulatoryComplianceEngine>();
builder.Services.AddSingleton<IRegulatoryRuleProvider, InMemoryRegulatoryRuleProvider>();
builder.Services.AddSingleton<IGovernanceAuditRepository, InMemoryGovernanceAuditRepository>();
builder.Services.AddTransient<EvaluateComplianceCommandHandler>();
builder.Services.AddGovernanceConflictDetection();

builder.Services.AddControllers();

var app = builder.Build();

// TODO: Configure middleware pipeline (exception handling, authentication, etc.)
app.MapControllers();

app.Run();
