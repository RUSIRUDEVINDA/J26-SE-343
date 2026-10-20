using StateLandGovernance.BuildingBlocks.DependencyInjection;
using StateLandGovernance.WorkflowGovernance.Application;
using StateLandGovernance.WorkflowGovernance.Infrastructure;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations;
using StateLandGovernance.WorkflowGovernance.Presentation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
using StateLandGovernance.LandIntelligence.Presentation.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using StateLandGovernance.Api.Security;
using StateLandGovernance.WorkflowGovernance.Presentation.Security;
using StateLandGovernance.LandIntelligence.Presentation.DependencyInjection;
using StateLandGovernance.Shared.Infrastructure.Configuration;
using StateLandGovernance.Api.Observability;
using StateLandGovernance.BuildingBlocks.Observability;

EnvFileLoader.LoadFromRepositoryRoot();

var builder = WebApplication.CreateBuilder(args);
if (builder.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("LocalDevelopmentSignIn:Enabled"))
    builder.Configuration.AddJsonFile("development-accounts.local.json", optional: false, reloadOnChange: false);


var isOcrDemoEnabled = builder.Environment.IsDevelopment()
    && builder.Configuration.GetValue<bool>("WorkflowGovernance:OcrDemo:Enabled", false);

builder.Services.AddBuildingBlocks();

var workflowConn = Environment.GetEnvironmentVariable("WORKFLOW_GOVERNANCE_CONNECTION")
    ?? Environment.GetEnvironmentVariable("LAND_INTELLIGENCE_CONNECTION");

if (!string.IsNullOrWhiteSpace(workflowConn))
{
    builder.Services.AddWorkflowGovernancePostgresPersistence(workflowConn);
}

builder.Services
    .AddWorkflowGovernanceApplication()
    .AddWorkflowGovernanceInfrastructure(builder.Environment)
    .AddWorkflowGovernancePresentation(builder.Configuration);

if (isOcrDemoEnabled)
{
    builder.Services
        .AddWorkflowGovernanceLocalDocumentStorage(builder.Configuration)
        .AddWorkflowGovernanceDocumentIntelligence(builder.Configuration);
}

var governanceConn = Environment.GetEnvironmentVariable("GOVERNANCE_INTELLIGENCE_CONNECTION")
    ?? builder.Configuration.GetConnectionString("GovernanceIntelligenceConnection")
    ?? workflowConn;

if (!string.IsNullOrWhiteSpace(governanceConn))
{
    builder.Configuration["ConnectionStrings:GovernanceIntelligenceConnection"] = governanceConn;
}

builder.Services.AddSingleton<IRegulatoryComplianceEngine, RegulatoryComplianceEngine>();
builder.Services.AddSingleton<IRegulatoryRuleProvider, InMemoryRegulatoryRuleProvider>();
builder.Services.AddGovernanceIntelligenceInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddTransient<EvaluateComplianceCommandHandler>();
builder.Services.AddGovernanceConflictDetection();
builder.Services.AddGovernanceRiskIntelligence();
builder.Services.AddExplainableGovernanceEngine();
builder.Services.AddGovernanceConsensusEngine();
builder.Services.AddConditionalGovernanceVerification();
builder.Services.AddEarlyGovernanceScreening();
builder.Services.AddLeaseApprovalTimingMonitoring();
builder.Services.AddGovernanceScreeningOrchestration();

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "DevelopmentFrontend",
        policy => policy
            .WithOrigins(
                "http://localhost:3000",
                "http://127.0.0.1:3000",
                "http://localhost:5173",
                "http://127.0.0.1:5173")
            .WithExposedHeaders("X-Correlation-ID")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddControllers();
builder.Services.AddLandIntelligenceInfrastructure(builder.Configuration);
builder.Services.AddLandIntelligencePresentation();
builder.Services.AddLandIntelligenceAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddScoped<StateLandGovernance.WorkflowGovernance.Application.Interfaces.IIntakeAssignmentDirectory, IntakeAssignmentDirectory>();
builder.Services.AddTransient<IClaimsTransformation, WorkflowAssignmentClaimsTransformation>();
builder.Services.AddSingleton<DevelopmentAccounts>();
builder.Services.AddScoped<UploadedCaseResearchScenarios>();
builder.Services.AddScoped<DevelopmentSpatialInputProvider>();
builder.Services.AddScoped<IDevelopmentSpatialInputProvider, UploadedCaseDevelopmentSpatial>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<StateLandGovernance.WorkflowGovernance.Application.Interfaces.IScreeningEvidenceProvider, PortalResearchEvidence>();
builder.Services.AddScoped<StateLandGovernance.GovernanceIntelligence.Application.Screening.Interfaces.IApprovedScreeningPolicySource, PortalResearchPolicy>();
builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.Events ??= new JwtBearerEvents();
    var previous = options.Events.OnTokenValidated;
    options.Events.OnTokenValidated = async context =>
    {
        if (previous != null) await previous(context);
        if (context.Principal == null || !context.HttpContext.RequestServices.GetRequiredService<DevelopmentAccounts>()
            .Validate(context.HttpContext, context.Principal)) context.Fail("Local development session is disabled, expired or revoked.");
    };
});

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

    if (isOcrDemoEnabled)
    {
        options.SwaggerDoc("workflow-governance", new OpenApiInfo
        {
            Title = "State Land Governance — Component 3 Workflow Governance API",
            Version = "v1",
            Description =
                "Component 3 workflow governance, document intake, and OCR intelligence demonstration endpoints."
        });
    }

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

        if (isOcrDemoEnabled)
        {
            options.SwaggerEndpoint(
                "/swagger/workflow-governance/swagger.json",
                "Workflow Governance API v1");
        }
    });

    if (isOcrDemoEnabled)
    {
        var docOpt = app.Services.GetService<IOptions<DocumentIntelligenceOptions>>()?.Value;
        if (docOpt != null)
        {
            app.Logger.LogInformation(
                "[WorkflowGovernance] DocumentIntelligence configured with BaseUrl: {BaseUrl}, RequestTimeoutSeconds: {Timeout}s",
                docOpt.BaseUrl,
                docOpt.RequestTimeoutSeconds);
        }
    }

    try
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetService<LandIntelligenceDbContext>();
        if (dbContext != null && dbContext.Database.CanConnect())
        {
            dbContext.Database.Migrate();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Development] LandIntelligence migration skipped: {ex.Message}");
    }
}
else
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<LandIntelligenceDbContext>();
    dbContext.Database.Migrate();
}

app.Use(async (context, next) =>
{
    var reference = System.Diagnostics.Activity.Current?.Id ?? context.TraceIdentifier;
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Correlation-ID"] = reference;
        return Task.CompletedTask;
    });
    using var scope = app.Logger.BeginScope(new Dictionary<string, object> { ["ErrorReference"] = reference });
    await next(context);
    if (context.Response.StatusCode >= 500)
        app.Logger.LogError("Request failed with status {Status}; error reference {ErrorReference}", context.Response.StatusCode, reference);
});
app.UseLandIntelligenceExceptionHandling();

app.UseRouting();
app.Use(async (context, next) =>
{
    var accounts = context.RequestServices.GetRequiredService<DevelopmentAccounts>();
    if (accounts.Enabled)
    {
        if (context.Request.Path.Equals("/api/v1/land/auth/session", StringComparison.OrdinalIgnoreCase))
        { context.Response.StatusCode = 404; return; }
        context.Request.Headers.Remove("X-Actor-Role");
        context.Request.Headers.Remove("X-Actor-Id");
    }
    await next(context);
});
app.UseAuthentication();
app.UseLandIntelligenceCallerContext();
app.UseAuthorization();
app.UseMiddleware<PrometheusMetricsMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseCors("DevelopmentFrontend");
}

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "StateLandGovernance.Api",
    dataMode = "Live"
}));
app.MapGet("/metrics", (PrometheusMetricsRegistry metrics) =>
    Results.Text(metrics.Render(), "text/plain; version=0.0.4; charset=utf-8"));

app.Run();

// Expose Program for WebApplicationFactory-based tests.
public partial class Program;
