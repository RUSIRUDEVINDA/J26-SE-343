using StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using StateLandGovernance.Api.Observability;
using StateLandGovernance.Api.Security;
using StateLandGovernance.BuildingBlocks.DependencyInjection;
using StateLandGovernance.BuildingBlocks.Observability;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.DependencyInjection;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Infrastructure.DependencyInjection;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Presentation;
using StateLandGovernance.LandIntelligence.Presentation.DependencyInjection;
using StateLandGovernance.LandIntelligence.Presentation.Security;
using StateLandGovernance.Shared.Infrastructure.Configuration;
using StateLandGovernance.WorkflowGovernance.Application;
using StateLandGovernance.WorkflowGovernance.Infrastructure;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations;
using StateLandGovernance.WorkflowGovernance.Presentation;
using StateLandGovernance.WorkflowGovernance.Presentation.Security;

EnvFileLoader.LoadFromRepositoryRoot();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        "GovernanceCaseReviewer",
        policy => policy
            .RequireAuthenticatedUser()
            .RequireRole("Officer", "ComplianceOfficer", "LandOfficer")
            .RequireClaim("authority_case_id")
            .RequireClaim("capability", "CaseWorkspaceReviewer")
    );
});

if (args.Contains("--setup-demo-accounts", StringComparer.Ordinal))
{
    await PortalAccountStore.SetupAsync(
        builder.Configuration,
        builder.Environment,
        builder.Environment.ContentRootPath
    );

    return;
}

if (
    builder.Configuration.GetValue<bool>("PortalAuthentication:Enabled")
    || (
        builder.Environment.IsDevelopment()
        && builder.Configuration.GetValue<bool>(
            "LocalDevelopmentSignIn:Enabled"
        )
    )
)
{
    await PortalAccountStore.LoadAsync(builder.Configuration);
}

var isOcrDemoEnabled =
    builder.Environment.IsDevelopment()
    && builder.Configuration.GetValue<bool>(
        "WorkflowGovernance:OcrDemo:Enabled",
        false
    );

builder.Services.AddBuildingBlocks();

var workflowConn =
    Environment.GetEnvironmentVariable("WORKFLOW_GOVERNANCE_CONNECTION")
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

var governanceConn =
    Environment.GetEnvironmentVariable("GOVERNANCE_INTELLIGENCE_CONNECTION")
    ?? builder.Configuration.GetConnectionString(
        "GovernanceIntelligenceConnection"
    )
    ?? workflowConn;

if (!string.IsNullOrWhiteSpace(governanceConn))
{
    builder.Configuration[
        "ConnectionStrings:GovernanceIntelligenceConnection"
    ] = governanceConn;
}

builder.Services.AddSingleton<
    IRegulatoryComplianceEngine,
    RegulatoryComplianceEngine
>();

builder.Services.AddSingleton<
    IRegulatoryRuleProvider,
    InMemoryRegulatoryRuleProvider
>();

builder.Services.AddGovernanceIntelligenceInfrastructure(
    builder.Configuration,
    builder.Environment
);

builder.Services.Configure<DevelopmentRegulatoryReportReadOptions>(
    builder.Configuration.GetSection(
        "GovernanceIntelligence:IndependentRegulatoryRead"
    )
);

builder.Services.Replace(
    ServiceDescriptor.Scoped<
        IIndependentRegulatoryReadAssignments,
        DevelopmentRegulatoryReportAssignments
    >()
);

builder.Services.AddTransient<EvaluateComplianceCommandHandler>();
builder.Services.AddGovernanceConflictDetection();
builder.Services.AddGovernanceRiskIntelligence();
builder.Services.AddExplainableGovernanceEngine();
builder.Services.AddGovernanceConsensusEngine();
builder.Services.AddConditionalGovernanceVerification();
builder.Services.AddEarlyGovernanceScreening();
builder.Services.AddLeaseApprovalTimingMonitoring();
builder.Services.AddGovernanceScreeningOrchestration();

// Disambiguate Component4ScreeningGateway constructor registration
// for Microsoft DI.
builder.Services.RemoveAll<
    StateLandGovernance.WorkflowGovernance.Application.Interfaces.IComponent4ScreeningGateway
>();

builder.Services.AddScoped<
    StateLandGovernance.WorkflowGovernance.Application.Interfaces.IComponent4ScreeningGateway
>(sp =>
    new StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations.Component4ScreeningGateway(
        sp.GetRequiredService<EvaluateComplianceCommandHandler>(),
        sp.GetRequiredService<
            StateLandGovernance.WorkflowGovernance.Application.Interfaces.ILeaseCaseRepository
        >(),
        sp.GetRequiredService<
            StateLandGovernance.WorkflowGovernance.Application.Interfaces.ICommandHandler<
                StateLandGovernance.WorkflowGovernance.Application.Commands.RecordScreeningResultCommand,
                StateLandGovernance.WorkflowGovernance.Application.DTOs.ScreeningResultDto
            >
        >(),
        sp.GetService<TimeProvider>(),
        sp.GetService<
            ILogger<
                StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations.Component4ScreeningGateway
            >
        >()
    )
);

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "DevelopmentFrontend",
        policy => policy
            .WithOrigins(
                "http://localhost:3000",
                "http://127.0.0.1:3000",
                "http://localhost:5173",
                "http://127.0.0.1:5173"
            )
            .WithExposedHeaders("X-Correlation-ID")
            .AllowAnyHeader()
            .AllowAnyMethod()
    );
});

builder.Services.AddControllers();

builder.Services.AddLandIntelligenceInfrastructure(builder.Configuration);
builder.Services.AddLandIntelligencePresentation();
builder.Services.AddLandIntelligenceAuthentication(
    builder.Configuration,
    builder.Environment
);

builder.Services.AddScoped<
    StateLandGovernance.WorkflowGovernance.Application.Interfaces.IIntakeAssignmentDirectory,
    IntakeAssignmentDirectory
>();

builder.Services.AddTransient<
    IClaimsTransformation,
    WorkflowAssignmentClaimsTransformation
>();

builder.Services.AddSingleton<DevelopmentAccounts>();
builder.Services.AddScoped<UploadedCaseResearchScenarios>();

builder.Services.AddScoped<
    StateLandGovernance.WorkflowGovernance.Application.Interfaces.IDevelopmentRegulatoryInputSource,
    DevelopmentRegulatoryInputSource
>();

builder.Services.AddScoped<DevelopmentSpatialInputProvider>();

builder.Services.AddScoped<
    IDevelopmentSpatialInputProvider,
    UploadedCaseDevelopmentSpatial
>();

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<
    IRegulatoryCaseBindingValidator,
    RegulatoryCaseBindingValidator
>();

builder.Services.AddScoped<
    StateLandGovernance.GovernanceIntelligence.Application.Complaints.IComplaintReferenceAccess,
    ComplaintReferenceAccess
>();

builder.Services.AddScoped<
    StateLandGovernance.GovernanceIntelligence.Application.Complaints.IComplaintOfficerDirectory,
    CentralComplaintDirectory
>();

builder.Services.AddScoped<
    StateLandGovernance.GovernanceIntelligence.Application.Complaints.IComplaintCentralGrantSource,
    CentralComplaintGrantFile
>();

builder.Services.Configure<CommissionerReportReadOptions>(
    builder.Configuration.GetSection(
        "GovernanceIntelligence:CommissionerReportReads"
    )
);

builder.Services.AddScoped(
    sp => sp.GetRequiredService<
        IOptionsSnapshot<CommissionerReportReadOptions>
    >().Value
);

builder.Services.AddScoped<
    ICommissionerReportGrantSource,
    CommissionerReportGrantFile
>();

builder.Services.AddScoped<
    ICommissionerReportReadAccess,
    CommissionerReportReadAccess
>();

builder.Services.AddScoped<
    StateLandGovernance.GovernanceIntelligence.Application.Complaints.IComplaintActorContext,
    ComplaintHttpActor
>();

builder.Services.AddScoped<
    StateLandGovernance.WorkflowGovernance.Application.Interfaces.IScreeningEvidenceProvider,
    PortalResearchEvidence
>();

builder.Services.AddScoped<
    StateLandGovernance.GovernanceIntelligence.Application.Screening.Interfaces.IApprovedScreeningPolicySource,
    PortalResearchPolicy
>();

builder.Services.PostConfigure<JwtBearerOptions>(
    JwtBearerDefaults.AuthenticationScheme,
    options =>
    {
        options.Events ??= new JwtBearerEvents();

        var previous = options.Events.OnTokenValidated;

        options.Events.OnTokenValidated = async context =>
        {
            if (previous != null)
            {
                await previous(context);
            }

            if (
                context.Principal == null
                || !context.HttpContext.RequestServices
                    .GetRequiredService<DevelopmentAccounts>()
                    .Validate(context.HttpContext, context.Principal)
            )
            {
                context.Fail(
                    "Local development session is disabled, expired or revoked."
                );
            }
        };
    }
);

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "all",
        new OpenApiInfo
        {
            Title =
                "State Land Governance — All Platform Services (Unified)",
            Version = "v1",
            Description =
                "Unified interactive API surface capturing all platform services: "
                + "Land Intelligence (C1), Workflow Governance (C3), and "
                + "AI Governance Intelligence (C4)."
        }
    );

    options.SwaggerDoc(
        "governance-intelligence",
        new OpenApiInfo
        {
            Title =
                "State Land Governance — Component 4 Governance Intelligence API",
            Version = "v1",
            Description =
                "AI-driven regulatory compliance, conflict detection, early "
                + "governance screening, risk assessment, explanation, and "
                + "consensus verification."
        }
    );

    options.SwaggerDoc(
        LandIntelligenceApiGroups.External,
        new OpenApiInfo
        {
            Title =
                "State Land Governance — Component 1 External Read API",
            Version = "v1",
            Description =
                "Read-only REST contract for external platform modules: "
                + "land parcel queries, search, spatial constraints, knowledge "
                + "graph relationships, and explainable recommendations."
        }
    );

    options.SwaggerDoc(
        LandIntelligenceApiGroups.Internal,
        new OpenApiInfo
        {
            Title =
                "State Land Governance — Component 1 Internal Maintenance API",
            Version = "v1",
            Description =
                "Component 1 parcel persistence endpoints. Not for consumption "
                + "by external platform modules."
        }
    );

    if (isOcrDemoEnabled)
    {
        options.SwaggerDoc(
            "workflow-governance",
            new OpenApiInfo
            {
                Title =
                    "State Land Governance — Component 3 Workflow Governance API",
                Version = "v1",
                Description =
                    "Component 3 workflow governance, document intake, and "
                    + "OCR intelligence demonstration endpoints."
            }
        );
    }

    options.DocInclusionPredicate(
        (documentName, apiDescription) =>
        {
            if (
                string.Equals(
                    documentName,
                    "all",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return true;
            }

            return string.Equals(
                apiDescription.GroupName,
                documentName,
                StringComparison.OrdinalIgnoreCase
            );
        }
    );
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/swagger/all/swagger.json",
            "All Platform Services (Unified) v1"
        );

        options.SwaggerEndpoint(
            "/swagger/governance-intelligence/swagger.json",
            "Component 4 — Governance Intelligence API v1"
        );

        options.SwaggerEndpoint(
            $"/swagger/{LandIntelligenceApiGroups.External}/swagger.json",
            "Land Intelligence External Read API v1"
        );

        options.SwaggerEndpoint(
            $"/swagger/{LandIntelligenceApiGroups.Internal}/swagger.json",
            "Land Intelligence Internal API v1"
        );

        if (isOcrDemoEnabled)
        {
            options.SwaggerEndpoint(
                "/swagger/workflow-governance/swagger.json",
                "Workflow Governance API v1"
            );
        }
    });

    if (isOcrDemoEnabled)
    {
        var docOpt = app.Services
            .GetService<IOptions<DocumentIntelligenceOptions>>()
            ?.Value;

        if (docOpt != null)
        {
            app.Logger.LogInformation(
                "[WorkflowGovernance] DocumentIntelligence configured with "
                    + "BaseUrl: {BaseUrl}, RequestTimeoutSeconds: {Timeout}s",
                docOpt.BaseUrl,
                docOpt.RequestTimeoutSeconds
            );
        }
    }
}

app.Use(async (context, next) =>
{
    var reference =
        System.Diagnostics.Activity.Current?.Id
        ?? context.TraceIdentifier;

    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Correlation-ID"] = reference;
        return Task.CompletedTask;
    });

    using var scope = app.Logger.BeginScope(
        new Dictionary<string, object>
        {
            ["ErrorReference"] = reference
        }
    );

    await next(context);

    if (context.Response.StatusCode >= 500)
    {
        app.Logger.LogError(
            "Request failed with status {Status}; "
                + "error reference {ErrorReference}",
            context.Response.StatusCode,
            reference
        );
    }
});

app.UseLandIntelligenceExceptionHandling();

app.UseRouting();

if (app.Environment.IsDevelopment())
{
    app.UseCors("DevelopmentFrontend");
}

app.Use(async (context, next) =>
{
    var accounts = context.RequestServices
        .GetRequiredService<DevelopmentAccounts>();

    if (accounts.Enabled)
    {
        if (
            context.Request.Path.Equals(
                "/api/v1/land/auth/session",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            context.Response.StatusCode = 404;
            return;
        }

        context.Request.Headers.Remove("X-Actor-Role");
        context.Request.Headers.Remove("X-Actor-Id");
    }

    await next(context);
});

app.UseAuthentication();
app.UseLandIntelligenceCallerContext();
app.UseAuthorization();

app.UseMiddleware<PrometheusMetricsMiddleware>();

app.MapControllers();

app.MapGet(
    "/health",
    () =>
        Results.Ok(
            new
            {
                status = "Healthy",
                service = "StateLandGovernance.Api",
                dataMode = "Live"
            }
        )
);

app.MapGet(
    "/metrics",
    (PrometheusMetricsRegistry metrics) =>
        Results.Text(
            metrics.Render(),
            "text/plain; version=0.0.4; charset=utf-8"
        )
);

app.Run();

public partial class Program;