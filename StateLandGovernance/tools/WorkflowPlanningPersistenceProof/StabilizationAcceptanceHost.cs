// ISOLATED ACCEPTANCE ONLY. Never registered by the ordinary API. No repository .env is loaded.
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Application.Screening.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.Screening.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
using StateLandGovernance.WorkflowGovernance.Application;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;
using StateLandGovernance.WorkflowGovernance.Infrastructure;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;
using StateLandGovernance.WorkflowGovernance.Presentation.Controllers;

internal static class StabilizationAcceptanceHost
{
    public const string Connection = "Host=127.0.0.1;Port=55439;Database=stabilization_acceptance_20261018;Username=batch4b";
    public static readonly Guid CaseId = Guid.Parse("fac70000-0000-4000-8000-000000000001");
    public static readonly Guid ApplicantId = Guid.Parse("fac70000-0000-4000-8000-000000000002");
    public static readonly Guid OfficerId = Guid.Parse("fac70000-0000-4000-8000-000000000003");
    public static readonly Guid ProposalId = Guid.Parse("fac70000-0000-4000-8000-000000000004");
    public static readonly Guid ParcelId = Guid.Parse("fac70000-0000-4000-8000-000000000005");
    public static readonly string[] Capabilities = ["CaseWorkspaceReviewer", "WorkflowPlanner", "WorkflowPlanApprover", "WorkflowPlanSuperseder",
        "WorkflowExecutor", "CabinetReviewer", "DepartmentReviewer", "DSReviewer", "ProvincialReviewer", "CommissionerReviewer",
        "WorkflowDecisionReviser", "DocumentSubmitter", "CaseEvidenceDistributor", "FulfillmentConfigurator", "FulfillmentReviewer",
        "FulfillmentForwardReviewer", "ConditionReviewer", "WorkflowFinalRecorder", "FulfillmentWaiverReviewer", "ProposalReviewer",
        "ClassificationReviewer", "HandoffPreparer", "HandoffDispatcher", "HandoffAcknowledger"];
    public static VerifiedAuthorityContext Authority(Guid actor) => new(actor, Capabilities.Append("LeaseInitiator").ToArray(), "LeaseCase",
        CaseId.ToString("D"), DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddHours(8));

    public static async Task RunAsync(string[] args)
    {
        var database = new Npgsql.NpgsqlConnectionStringBuilder(Connection).Database!;
        var selected = args.SingleOrDefault(a => a.StartsWith("--isolated-database=", StringComparison.Ordinal));
        if (selected != null)
        {
            database = selected["--isolated-database=".Length..];
            if (!System.Text.RegularExpressions.Regex.IsMatch(database, "^stabilization_acceptance_[a-z0-9_]{1,48}$"))
                throw new ArgumentException("Acceptance databases must have the isolated stabilization_acceptance_ prefix.");
        }
        var connection = new Npgsql.NpgsqlConnectionStringBuilder(Connection) { Database = database }.ConnectionString;
        // This fixed, dedicated database must be created explicitly by the acceptance operator.
        // Migrations are additive on this test destination. No reset, EnsureDeleted, or real-case access.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development,
            ApplicationName = typeof(CasesController).Assembly.GetName().Name });
        builder.WebHost.UseUrls("http://127.0.0.1:5764");
        builder.Logging.ClearProviders(); builder.Logging.AddConsole(); builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddAuthentication("ISOLATED-TEST").AddScheme<AuthenticationSchemeOptions, AcceptanceIdentity>("ISOLATED-TEST", _ => { });
        builder.Services.AddAuthorization(o => o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        var testKeys = Directory.CreateDirectory(Path.GetFullPath(".tmp/stabilization-acceptance-final/test-keys"));
        builder.Services.AddDataProtection().PersistKeysToFileSystem(testKeys).UseEphemeralDataProtectionProvider(); // Never load a production/user key ring.
        builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins("http://127.0.0.1:3001").AllowAnyHeader().AllowAnyMethod()));
        builder.Services.AddControllers().AddApplicationPart(typeof(CasesController).Assembly);
        builder.Services.AddWorkflowGovernanceApplication().AddWorkflowGovernanceInfrastructure(true).AddWorkflowGovernancePostgresPersistence(connection);
        builder.Services.AddWorkflowGovernanceLocalDocumentStorage(configureOptions: o => o.RootPath = Path.GetFullPath(selected == null
            ? ".tmp/stabilization-acceptance-final/storage" : ".tmp/stabilization-acceptance-runs/" + database + "/storage"));
        builder.Services.AddWorkflowGovernanceDocumentIntelligence(configureOptions: o => o.BaseUrl = "http://127.0.0.1:8009");
        builder.Services.AddDbContext<GovernanceIntelligenceDbContext>(o => o.UseNpgsql(connection,
            n => n.MigrationsHistoryTable("__ef_migrations_history", GovernanceIntelligenceDbContext.SchemaName)));
        builder.Services.AddScoped<IConsolidatedGovernanceScreeningStore, PostgresConsolidatedGovernanceScreeningStore>();
        builder.Services.AddScoped<IGovernanceEvaluationStore, PostgresGovernanceEvaluationStore>();
        builder.Services.AddScoped<IEarlyGovernanceScreeningStore, PostgresEarlyGovernanceScreeningStore>();
        builder.Services.AddSingleton<IRegulatoryComplianceEngine, RegulatoryComplianceEngine>();
        builder.Services.AddSingleton<IRegulatoryRuleProvider, InMemoryRegulatoryRuleProvider>();
        builder.Services.AddSingleton<IGovernanceConflictEngine, GovernanceConflictEngine>();
        builder.Services.AddSingleton<IEarlyGovernanceScreeningEngine, EarlyGovernanceScreeningEngine>();
        builder.Services.AddSingleton<IApprovedScreeningPolicySource, AcceptancePolicy>();
        builder.Services.AddScoped<OrchestrateGovernanceScreeningCommandHandler>();
        builder.Services.AddSingleton<IScreeningEvidenceProvider, AcceptanceEvidence>();
        builder.Services.AddSingleton<IDevelopmentSpatialInputProvider, AcceptanceSpatial>();
        var app = builder.Build();
        using (var scope = app.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<WorkflowGovernanceDbContext>().Database.MigrateAsync();
            await scope.ServiceProvider.GetRequiredService<GovernanceIntelligenceDbContext>().Database.MigrateAsync();
            await SeedAsync(scope.ServiceProvider);
        }
        app.UseCors(); app.UseAuthentication(); app.UseAuthorization();
        // No C1 routes, production token minting, downstream delivery or external clearance in this host.
        app.MapControllers();
        app.MapGet("/acceptance", () => Results.Ok(new { label = "ISOLATED TEST ONLY; actual C4 deterministic engines; synthetic documents/policy/evidence",
            CaseId, ProposalId, database, productionAuthentication = false })).AllowAnonymous();
        await app.RunAsync();
    }

    private static async Task SeedAsync(IServiceProvider sp)
    {
        var cases = sp.GetRequiredService<ILeaseCaseRepository>();
        if (await cases.GetByIdAsync(new(CaseId)) != null) return; // Restart retrieves; never replace prior work.
        var at = DateTime.UtcNow; var actor = Authority(ApplicantId);
        var c = new LeaseCase(new(CaseId), "ISOLATED-ACCEPTANCE-20261017", ApplicantId, at, actor.ToDomain());
        await cases.AddAsync(c);
        var facts = new Dictionary<string,string> { ["applicant_name"] = "ISOLATED TEST APPLICANT", ["proposed_land_use"] = "Agriculture",
            ["land_extent"] = "51", ["land_extent_unit"] = "acres", ["requested_lease_period"] = "30 years",
            ["investment_amount"] = "1000000", ["investment_currency"] = "LKR", ["district"] = "TEST District",
            ["divisional_secretariat_division"] = "TEST DS" };
        var content = TestPdf(facts.Select(f => "ISOLATED TEST FIXTURE: " + f.Key + "=" + f.Value));
        var version = Guid.NewGuid();
        var receipt = await sp.GetRequiredService<IDocumentContentWriter>().WriteAsync(new MemoryStream(content), "ISOLATED_TEST_Proposal.pdf", "application/pdf", ProposalId, version);
        var document = new GovernedDocument(new(ProposalId), c.Id, "Proposal", new(version), new(receipt.ChecksumAlgorithm, receipt.ChecksumValue),
            new(receipt.ContentReference), receipt.OriginalFileName, receipt.MediaType, receipt.FileSizeInBytes, ApplicantId, at, actor.ToDomain());
        await sp.GetRequiredService<IGovernedDocumentRepository>().AddAsync(document);
        var analysis = new DocumentAnalysis(new(Guid.NewGuid()), document.Id, document.ActiveVersionId, document.Versions.Single().Checksum, 1, 1, at);
        var runId = new AnalysisRunId(Guid.NewGuid()); var resultId = new AnalysisRunResultId(Guid.NewGuid());
        analysis.RequestRun(runId, new("EXPLICIT_TEST_FIXTURE", "Not OCR/Gemini", "1"), [new("TEST_FIXTURE")], at);
        analysis.StartRun(runId, at);
        var artifact = new AnalysisResultArtifactReference(new(Guid.NewGuid()), "TEST_FIXTURE", "TEST_ONLY/proposal.txt", "text/plain", new("SHA-256", new string('a',64)));
        var inputs = facts.Select(f => new ExtractedFactInput(new(Guid.NewGuid()), new(f.Key), new(AnalysisFactValueKind.Text, f.Value), new(1m),
            new(artifact.Id, 1, "ISOLATED TEST FIXTURE: " + f.Key + "=" + f.Value))).ToArray();
        analysis.CompleteRun(runId, resultId, AnalysisResultOutcome.OutputsProduced, [artifact], inputs, at);
        var docAuthority = Authority(OfficerId) with { Capabilities = ["FactVerifier", "FactSnapshotPublisher"], ScopeKind = "GovernedDocument", ScopeTargetIdentifier = ProposalId.ToString("D") };
        foreach (var fact in inputs) analysis.RecordFactVerification(new(Guid.NewGuid()), resultId, fact.Id, FactVerificationDecision.Confirmed, null, null, OfficerId, at, docAuthority.ToDomain());
        analysis.PublishVerifiedFactSnapshot(new(Guid.NewGuid()), resultId, OfficerId, at, docAuthority.ToDomain());
        c.UpdateCurrentVerifiedFactSnapshot(analysis.VerifiedFactSnapshots.Single().Id.Value);
        await sp.GetRequiredService<IDocumentAnalysisRepository>().AddAsync(analysis);
        await sp.GetRequiredService<IWorkflowGovernanceUnitOfWork>().CommitAsync();
        var writer = sp.GetRequiredService<CompletenessConfigurationWriter>();
        var template = new ProposalTemplate(new("STD-PROP-2026-V1"), "1.0", "ISOLATED TEST TEMPLATE", "EXPLICIT AUTOMATED ACCEPTANCE FIXTURE; not approved runtime configuration",
            ProposalTemplateStatus.Active, [new(new("TEST_CONTENT"), ProposalRequirementKind.Section, "ISOLATED TEST CONTENT", true, new("STD-PROP-2026-V1"), "1.0", "TEST ONLY", 1)]);
        await writer.PublishTemplateAsync(template, OfficerId);
        await writer.PublishDocumentsAsync("DOC-REQ-AGRI-V1", "1.0", "EXPLICIT TEST ONLY", OfficerId,
            new[] { "Proposal", "SurveyPlan" }.Select(code => new DocumentRequirementSnapshot(new(Guid.NewGuid()), new(code), RequirementCriticality.Mandatory,
                RequirementApplicability.Required, 1, "EXPLICIT_TEST_ONLY", "Synthetic acceptance requirement")).ToArray());
        await sp.GetRequiredService<ICommandHandler<AssessProposalContentCommand, ProposalContentAssessmentDto>>().HandleAsync(new(CaseId, ProposalId, version,
            receipt.ChecksumAlgorithm, receipt.ChecksumValue, "STD-PROP-2026-V1", "1.0", runId.Value.ToString("D"),
            [new("TEST_CONTENT", "Present", 1, "ISOLATED TEST FIXTURE", "TEST_ONLY", runId.Value.ToString("D"))], c.Revision));
        await sp.GetRequiredService<PostgresWorkflowPlanningStore>().PublishPolicyAsync(new("ISOLATED_ACCEPTANCE_ROUTING", "1", "EXPLICIT TEST ONLY; user workflow topology", OfficerId,
            at.AddDays(-1), at.AddDays(7), new Dictionary<string,string> { ["land_extent"] = "51" }, ["TEST_A", "TEST_B"], true,
            1, 1, 1, "DepartmentReviewer", "DSReviewer", "CommissionerReviewer", ["ISOLATED TEST decision evidence"],
            new("EXPLICIT TEST ONLY", "1", "ISOLATED_SPATIAL_2", true, ["TEST Province"], new Dictionary<string,string> { ["TEST_A"] = "ISOLATED TEST explicitly selected department" })), default);
    }

    private static byte[] TestPdf(IEnumerable<string> lines)
    {
        var text = "BT /F1 10 Tf 40 790 Td 14 TL (ISOLATED TEST DOCUMENT - NOT OCR OR GEMINI) Tj T* " +
            string.Join(" ", lines.Select(l => "(" + l.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)") + ") Tj T*")) + " ET";
        var objects = new[] { "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>", $"<< /Length {Encoding.ASCII.GetByteCount(text)} >>\nstream\n{text}\nendstream" };
        var pdf = new StringBuilder("%PDF-1.4\n"); var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++) { offsets.Add(pdf.Length); pdf.Append($"{i+1} 0 obj\n{objects[i]}\nendobj\n"); }
        var xref = pdf.Length; pdf.Append($"xref\n0 {objects.Length+1}\n0000000000 65535 f \n");
        foreach (var offset in offsets) pdf.Append(offset.ToString("D10") + " 00000 n \n");
        pdf.Append($"trailer\n<< /Size {objects.Length+1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}

internal sealed class AcceptanceIdentity(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var value = Request.Headers.Authorization.ToString();
        if (!value.StartsWith("Bearer ISOLATED-TEST-", StringComparison.Ordinal)) return Task.FromResult(AuthenticateResult.NoResult());
        var name = value["Bearer ISOLATED-TEST-".Length..];
        var institutions = new[] { "CABINET", "TEST_A", "TEST_B", "DS", "PROVINCIAL_LC", "INTER_PROVINCIAL_LC", "COMMISSIONER", "MINISTRY_LANDS", "SURVEY", "VALUATION" };
        if (name != "OFFICER" && name != "APPLICANT" && !institutions.Contains(name)) return Task.FromResult(AuthenticateResult.Fail("Unknown isolated test identity"));
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, (name == "APPLICANT" ? StabilizationAcceptanceHost.ApplicantId : StabilizationAcceptanceHost.OfficerId).ToString("D")),
            new(ClaimTypes.Role, name == "APPLICANT" ? "Applicant" : "Officer"), new("authority_case_id", StabilizationAcceptanceHost.CaseId.ToString("D")),
            new("authority_document_id", StabilizationAcceptanceHost.ProposalId.ToString("D")), new("exp", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString()) };
        if (name != "APPLICANT") claims.AddRange(StabilizationAcceptanceHost.Capabilities.Where(c => name == "OFFICER" || c != "CaseWorkspaceReviewer").Select(c => new Claim("capability", c)));
        if (institutions.Contains(name)) claims.Add(new("institution", name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
    }
}

internal sealed class AcceptancePolicy : IApprovedScreeningPolicySource
{
    public Task<ApprovedScreeningPolicy?> ReadAsync(DateTime at, CancellationToken ct) => Task.FromResult<ApprovedScreeningPolicy?>(new("ISOLATED TEST POLICY ONLY", "ISOLATED_POLICY_1",
        [NpdRuleCatalogue.GetDefaultRuleDefinitions().First() with { SourceReference = new(StateLandGovernance.GovernanceIntelligence.Domain.Enums.RuleSourceType.ResearchConfiguration,
            "ISOLATED TEST ONLY", "Acceptance fixture policy", "Synthetic", null, "1") }]));
}
internal sealed class AcceptanceEvidence : IScreeningEvidenceProvider
{
    public Task<ScreeningEvidenceContext> ReadAsync(Guid caseId, Guid? parcelId, CancellationToken ct) => Task.FromResult(new ScreeningEvidenceContext("ISOLATED_EVIDENCE_1",
        JsonSerializer.Serialize(new GovernanceEvidence("ISOLATED TEST ONLY; synthetic coverage, not clearance", false, false, false, false, false, [], []), CaseScreeningService.JsonOptions),
        JsonSerializer.Serialize(new SpatialReviewEvidence("Development mock spatial TEST ONLY", false, [], []), CaseScreeningService.JsonOptions), [], caseId, StabilizationAcceptanceHost.ParcelId));
}
internal sealed class AcceptanceSpatial : IDevelopmentSpatialInputProvider
{
    public Task<DevelopmentSpatialInput?> ReadAsync(Guid caseId, Guid? parcelId, CancellationToken ct) => Task.FromResult<DevelopmentSpatialInput?>(new(caseId, StabilizationAcceptanceHost.ParcelId,
        "ISOLATED_SPATIAL_2", JsonSerializer.SerializeToElement(new { id = StabilizationAcceptanceHost.ParcelId, identifier = new { cadastralNumber = "ISOLATED_TEST_ONLY" },
            area = new { value = 25m, unit = 2 }, location = new { province = "TEST Province", district = "TEST District", divisionalSecretariat = "TEST DS", gramaNiladhariDivision = "TEST GN" },
            spatial = new { coordinateSystem = "EPSG:4326", centroidLatitude = 7d, centroidLongitude = 80d } }),
        JsonSerializer.Serialize(new SpatialReviewEvidence("Development mock spatial TEST ONLY", false, [], []), CaseScreeningService.JsonOptions), ["TEST Province"], "Development mock spatial inputs; ISOLATED TEST ONLY"));
}
