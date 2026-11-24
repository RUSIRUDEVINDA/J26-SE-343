using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Application.WorkflowAnomaly;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Integrations.WorkflowAnomaly;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;

// No .env/host/provisioning; explicit fresh isolated target only.
var connection = Environment.GetEnvironmentVariable("C4_POSTWORKFLOW_ISOLATED_TEST_CONNECTION") ?? throw new Exception("Isolated connection required.");
var target = new NpgsqlConnectionStringBuilder(connection);
if (target.Host != "127.0.0.1" || target.Port < 55000 || target.Database?.StartsWith("c4_postworkflow_test_", StringComparison.Ordinal) != true)
    throw new Exception("Refusing ordinary database.");
var options = new DbContextOptionsBuilder<GovernanceIntelligenceDbContext>().UseNpgsql(connection,
    p => p.MigrationsHistoryTable("__ef_migrations_history", "governance_intelligence")).Options;
await using (var db = new GovernanceIntelligenceDbContext(options)) await db.Database.MigrateAsync();
var checks = new List<string>();
void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks.Add(message); }
var creator = Guid.Parse("e934306b-d74b-4ef3-a125-800ba8bc2069");
var real = new CountingClient(new HttpWorkflowAnomalyClient(new HttpClient { BaseAddress = new Uri("http://127.0.0.1:8104/"), Timeout = TimeSpan.FromSeconds(45) }, NullLogger<HttpWorkflowAnomalyClient>.Instance));
PostWorkflowDemonstrationStore Store(GovernanceIntelligenceDbContext db, IWorkflowAnomalyClient client) {
    var p = new PostgresComponent3WorkflowGovernanceProjectionStore(db); var a = new PostgresWorkflowAnomalyAssessmentStore(db);
    return new(db, p, a, new AssessWorkflowAnomalyCommandHandler(client, a, TimeProvider.System, p), TimeProvider.System);
}
if (args.Contains("verify")) {
    await using var reopened = new GovernanceIntelligenceDbContext(options);
    var saved = await Store(reopened, real).ListAsync(creator, default);
    Check(saved.Count == 2 && saved.All(r => r.State == "Saved" && r.Assessment != null), "Separate-process retrieval of both real saved results");
    Check(real.Calls == 0, "Restart retrieval performs no inference");
    Console.WriteLine(JsonSerializer.Serialize(new { checks, saved }, new JsonSerializerOptions(JsonSerializerDefaults.Web))); return;
}
if (args.Contains("httpverify")) {
    await using var db = new GovernanceIntelligenceDbContext(options);
    await HttpProof.Run(Store(db, real),creator);
    Check(real.Calls == 0,"HTTP preview/retrieval/replay never calls inference");
    Console.WriteLine("HTTP inference call count: 0");return;
}
var results = new List<PostWorkflowReport>();
foreach (var scenario in new[] { "administrative", "delays-and-revisions" }) {
    await using var db = new GovernanceIntelligenceDbContext(options);
    var store = Store(db, real);
    var result = await store.RunAsync(creator, scenario, false, default);
    Check(result.State == "Saved" && result.Assessment != null, "Real inference saved: " + scenario);
    Check(result.Assessment!.EventCount == result.Projection.Events.Count, "Exact trace event count: " + scenario);
    Check(result.Assessment.InputSha256.Length == 64 && result.Projection.InputSha256.Length == 64, "Both hashes retained: " + scenario);
    Check(result.Projection.Provenance.IsSynthetic && result.Projection.Events[^1].Activity == "DS Final Notification", "Synthetic provenance and distinct notification: " + scenario);
    var calls = real.Calls;
    var replay = await store.RunAsync(creator, scenario, false, default);
    Check(replay.Id == result.Id && replay.Assessment?.AssessmentId == result.Assessment.AssessmentId && real.Calls == calls, "Replay returns same assessment without inference: " + scenario);
    try { await store.GetAsync(Guid.NewGuid(), result.Id, default); throw new Exception("Cross actor allowed"); } catch (UnauthorizedAccessException) { checks.Add("Cross actor denied: " + scenario); }
    results.Add(result);
}
Check(real.Calls == 2, "Exactly two real inference calls");
// Simulated failures/concurrency are separate evidence, never ordinary records or model predictions.
var failureActor = Guid.NewGuid(); var fail = new CountingClient(null);
await using (var db = new GovernanceIntelligenceDbContext(options)) {
    var failed = await Store(db, fail).RunAsync(failureActor, "administrative", false, default);
    Check(failed.State == "Failed" && failed.Assessment == null, "Simulated ML failure persists honest state without assessment");
    await Store(db, fail).RunAsync(failureActor, "administrative", false, default);
    Check(fail.Calls == 1, "Failed replay does not automatically retry");
}
await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ => {
    await using var db = new GovernanceIntelligenceDbContext(options);
    await Store(db, fail).RunAsync(failureActor, "administrative", true, default, 1);
}));
Check(fail.Calls == 2, "Concurrent retry of the same failed attempt invokes client once");
var raceActor = Guid.NewGuid(); var blocker = new BlockingFailure();
var first = Task.Run(async () => { await using var db = new GovernanceIntelligenceDbContext(options); return await Store(db, blocker).RunAsync(raceActor, "administrative", false, default); });
await blocker.Started.Task;
await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ => { await using var db = new GovernanceIntelligenceDbContext(options); var pending = await Store(db, blocker).RunAsync(raceActor, "administrative", false, default); Check(pending.State == "Pending", "Concurrent submission observes committed reservation"); }));
blocker.Release.SetResult(); await first;
Check(blocker.Calls == 1, "Concurrent submissions invoke client once");
await using (var db = new GovernanceIntelligenceDbContext(options)) {
    Check(await db.Set<PostWorkflowDemonstrationEntity>().CountAsync() == 4, "Actor/scenario uniqueness");
    Check(await db.WorkflowAnomalyAssessments.CountAsync() == 2, "Only real successful assessments persisted");
    Check(await db.ComplianceEvaluations.CountAsync() == 0 && await db.GovernanceAuditAnchorOutbox.CountAsync() == 0, "No unrelated regulatory/outbox writes");
    Check((await db.Database.GetAppliedMigrationsAsync()).Last() == "20261022093623_AddKiwulaAssessmentProvenance", "Migration ordering follows existing complaints and regulatory migrations");
}
Console.WriteLine(JsonSerializer.Serialize(new { checks, realInferenceCalls = real.Calls, simulatedFailureCalls = fail.Calls, results }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

sealed class CountingClient(IWorkflowAnomalyClient? inner) : IWorkflowAnomalyClient {
    public int Calls;
    public async Task<WorkflowAnomalyResult> PredictAnomalyAsync(string caseId, IReadOnlyList<WorkflowAnomalyEvent> events, CancellationToken ct = default) {
        Interlocked.Increment(ref Calls);
        return inner == null ? WorkflowAnomalyResult.Failure(WorkflowAnomalyErrorCode.ServiceUnavailable, "Isolated simulated outage") : await inner.PredictAnomalyAsync(caseId, events, ct);
    }
}
sealed class BlockingFailure : IWorkflowAnomalyClient {
    public int Calls; public TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously); public TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task<WorkflowAnomalyResult> PredictAnomalyAsync(string c, IReadOnlyList<WorkflowAnomalyEvent> e, CancellationToken ct = default) { Interlocked.Increment(ref Calls); Started.TrySetResult(); await Release.Task; return WorkflowAnomalyResult.Failure(WorkflowAnomalyErrorCode.ServiceUnavailable, "Isolated blocked failure"); }
}
