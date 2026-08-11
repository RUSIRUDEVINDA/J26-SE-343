using Microsoft.EntityFrameworkCore;
using StateLandGovernance.LandIntelligence.Infrastructure.DependencyInjection;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.Shared.Infrastructure.Configuration;

EnvFileLoader.LoadFromRepositoryRoot();

var builder = WebApplication.CreateBuilder(args);

// TODO: Register shared infrastructure services (logging, persistence, security, storage)
// TODO: Register BuildingBlocks (CQRS, events, observability)
builder.Services.AddLandIntelligenceInfrastructure(builder.Configuration);
// TODO: Register LandIntelligence Application handlers and Presentation
// TODO: Register LeaseFeasibility module (Application + Infrastructure + Presentation)
// TODO: Register WorkflowGovernance module (Application + Infrastructure + Presentation)
// TODO: Register GovernanceIntelligence module (Application + Infrastructure + Presentation)

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<LandIntelligenceDbContext>();
    dbContext.Database.Migrate();
}

// TODO: Configure middleware pipeline (exception handling, authentication, etc.)
// TODO: Map module controllers and endpoints

app.Run();
