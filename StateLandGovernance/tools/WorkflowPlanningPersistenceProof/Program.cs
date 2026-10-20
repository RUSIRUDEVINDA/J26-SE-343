// EXPLICIT ISOLATED TEST FIXTURE TOOL. Not a real approved case, policy, authority or C4 result.
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.UnitTests.WorkflowGovernance.Application;
using StateLandGovernance.WorkflowGovernance.Application;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Domain.Screening;
using StateLandGovernance.WorkflowGovernance.Infrastructure;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;

if (args.Contains("--stabilization-host")) { await StabilizationAcceptanceHost.RunAsync(args); return; }
var Connection = args.Any(a => (a.StartsWith("--execution-", StringComparison.Ordinal) || a.StartsWith("--extensions-", StringComparison.Ordinal)))
    ? "Host=127.0.0.1;Port=55434;Database=workflow_execution_20261016;Username=batch4b"
    : "Host=127.0.0.1;Port=55434;Database=repair_planning_20261016;Username=batch4b";
if (args.Contains("--isolated-port-55439")) Connection = Connection.Replace("Port=55434;", "Port=55439;", StringComparison.Ordinal);
// Fixed, isolated destination; never discover repository connections or reset a database.
var services = new ServiceCollection();
services.AddLogging(); services.AddWorkflowGovernanceApplication().AddWorkflowGovernanceInfrastructure(true);
services.AddWorkflowGovernancePostgresPersistence(Connection);
if (args.Any(a => (a.StartsWith("--execution-", StringComparison.Ordinal) || a.StartsWith("--extensions-", StringComparison.Ordinal)))) services.AddScoped<ICaseScreeningReadiness, ExecutionFixtureReadiness>();
else services.AddScoped<ICaseScreeningReadiness, FixtureReadiness>();
if (args.Any(a => a.StartsWith("--execution-complete-", StringComparison.Ordinal))) {
    services.AddScoped<IPostWorkflowAssessmentGateway, FullWorkflowFixtureGateway>();
    services.AddScoped<IGovernedHandoffTransport, FullWorkflowFixtureTransport>();
}
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope(); var sp = scope.ServiceProvider;
var db = sp.GetRequiredService<WorkflowGovernanceDbContext>();
await db.Database.MigrateAsync();
if (args.Any(a => a.StartsWith("--extensions-concurrency-", StringComparison.Ordinal))) { await ConcurrencyPersistenceProof.RunAsync(provider, args.Contains("--extensions-concurrency-write")); return; }
if (args.Any(a => a.StartsWith("--extensions-", StringComparison.Ordinal))) { await GovernedExtensionsPersistenceProof.RunAsync(sp, args.Contains("--extensions-write") || args.Contains("--extensions-v2-write"), args.Any(a => a.StartsWith("--extensions-v2-", StringComparison.Ordinal))); return; }
if (args.Any(a => (a.StartsWith("--execution-", StringComparison.Ordinal) || a.StartsWith("--extensions-", StringComparison.Ordinal)))) { await ExecutionPersistenceProof.RunAsync(sp, args.Contains("--execution-write") || args.Contains("--execution-complete-write"), args.Any(a => a.StartsWith("--execution-complete-", StringComparison.Ordinal))); return; }
var caseId = Guid.Parse("fac30000-0000-0000-0000-000000000001");
var actor = Guid.Parse("fac30000-0000-0000-0000-000000000002");
var cases = sp.GetRequiredService<ILeaseCaseRepository>();
var planning = new WorkflowPlanningService(cases, sp.GetRequiredService<IDocumentAnalysisRepository>(),
    sp.GetRequiredService<IDocumentCompletenessAssessmentRepository>(), sp.GetRequiredService<IWorkflowPlanningStore>(),
    sp.GetRequiredService<ICaseScreeningReadiness>(), sp.GetRequiredService<IWorkflowGovernanceUnitOfWork>(), TimeProvider.System);
var now = DateTime.UtcNow;
var authority = new VerifiedAuthorityContext(actor, ["WorkflowPlanner", "WorkflowPlanApprover", "WorkflowPlanSuperseder"],
    "LeaseCase", caseId.ToString("D"), now.AddMinutes(-1), now, now.AddHours(1));
if (args.Contains("--write"))
{
    if (await cases.GetByIdAsync(new(caseId)) is not null) throw new InvalidOperationException("Fixture already exists. Use --read; do not overwrite it.");
    var fixture = ScreeningOrchestrationTests.CreateCaseWithVerifiedSnapshot(caseId, actor);
    await cases.AddAsync(fixture.LeaseCase);
    await sp.GetRequiredService<IGovernedDocumentRepository>().AddAsync(fixture.Document);
    await sp.GetRequiredService<IDocumentAnalysisRepository>().AddAsync(fixture.Analysis);
    var binding = new AssessedDocumentBinding(fixture.Document.Id, fixture.Document.ActiveVersionId, fixture.Document.Versions.First().Checksum);
    var classification = new ClassifiedDocument(new(Guid.NewGuid()), binding, new("TEST"), new(1m), DocumentClassificationStatus.Accepted, null, null, null, now);
    await sp.GetRequiredService<IDocumentCompletenessAssessmentRepository>().AddAsync(new(new(Guid.NewGuid()), fixture.LeaseCase.Id,
        "ISOLATED-TEST-ONLY", "1", [binding], [classification], [], now));
    fixture.LeaseCase.RecordScreeningResult(new(Guid.NewGuid(), fixture.LeaseCase.Id, fixture.Snapshot.Id.Value, ScreeningOutcome.Advisory, "ISOLATED TEST ONLY", now));
    await sp.GetRequiredService<IWorkflowGovernanceUnitOfWork>().CommitAsync();
    await sp.GetRequiredService<PostgresWorkflowPlanningStore>().PublishPolicyAsync(new("ISOLATED-TEST-ONLY", "1", "EXPLICIT TEST FIXTURE; not approved runtime rules", actor,
        now.AddDays(-1), now.AddDays(1), new Dictionary<string,string> { ["SurveyPlan.PlanNumber"] = "SP-2026-9901" }, ["TEST_A", "TEST_B"], true,
        10, 8, 5, "TestReviewer", "TestDS", "TestLC", ["TEST EVIDENCE"]), default);
    var state = await planning.GenerateAsync(caseId, new(fixture.Snapshot.Id.Value, fixture.LeaseCase.Revision, null, null, null), authority, default);
    var approved = await planning.ApproveAsync(caseId, state.ActivePlan!.Id, new(fixture.Snapshot.Id.Value, state.CaseRevision, state.ActivePlan.Revision), authority, default);
    await planning.GenerateAsync(caseId, new(fixture.Snapshot.Id.Value, approved.CaseRevision, approved.ActivePlan!.Id, approved.ActivePlan.Revision,
        "ISOLATED TEST ONLY: preserve approved prior plan on governed revision"), authority, default);
}
var stored = await cases.GetByIdAsync(new(caseId)) ?? throw new InvalidOperationException("Run --write first.");
var history = await sp.GetRequiredService<IWorkflowPlanningStore>().GetPlansAsync(caseId, default);
if (history.Count != 2 || stored.WorkflowPlanHistory.Count != 2 || stored.ActiveWorkflowPlan?.Status.ToString() != "Draft"
    || !stored.WorkflowPlanHistory.Any(p => p.Status.ToString() == "Superseded" && p.ApprovedAt.HasValue))
    throw new InvalidOperationException("Persistence assertions failed.");
Console.WriteLine(JsonSerializer.Serialize(new { fixture = "ISOLATED TEST ONLY", database = "repair_planning_20261016", caseId,
    mode = args.Contains("--write") ? "write-approve-revise" : "read-after-process-restart", status = stored.Status.ToString(),
    activePlanId = stored.ActiveWorkflowPlan.Id.Value, aggregateHistoryCount = stored.WorkflowPlanHistory.Count,
    plans = history.Select(p => new { p.Id, p.Revision, p.Status, p.ApprovedAt, p.RevisionReason, p.Binding, stages = p.Stages.Count,
        p.SnapshotId, hasFullInputBindings = p.InputBindingsJson.Contains("ISOLATED-TEST-ONLY") }) }));

sealed class FixtureReadiness(ILeaseCaseRepository cases) : ICaseScreeningReadiness
{
    public async Task<CaseScreeningState> GetAsync(Guid caseId, Guid? snapshotId, Guid? parcelId, CancellationToken ct)
    {
        // Trusted test double only in this isolated executable; never installed in normal-host DI.
        var leaseCase = (await cases.GetByIdAsync(new(caseId), ct))!;
        var result = JsonSerializer.SerializeToElement(new { assessmentId = leaseCase.LatestScreening!.Id,
            caseId, verifiedFactSnapshotId = leaseCase.CurrentVerifiedFactSnapshotId, overallStatus = "RequiresReview" });
        return new(caseId, leaseCase.Revision, leaseCase.CurrentVerifiedFactSnapshotId, null, "Assessed", true, [],
            new("c3-c4-v1", caseId, leaseCase.CurrentVerifiedFactSnapshotId, null, "ISOLATED-TEST-ONLY", "ISOLATED-TEST-ONLY", result, "{}", "ISOLATED-TEST-ONLY"));
    }
}
