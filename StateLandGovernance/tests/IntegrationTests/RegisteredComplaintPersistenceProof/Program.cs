using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
using StateLandGovernance.GovernanceIntelligence.Application.ComplaintClassification;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;

// Explicit isolated connection only. Never load .env, start the host or contact an ML service.
var connection = Environment.GetEnvironmentVariable("C4_COMPLAINTS_ISOLATED_TEST_CONNECTION")
    ?? throw new InvalidOperationException("Explicit isolated PostgreSQL connection required.");
var target = new NpgsqlConnectionStringBuilder(connection);
if (target.Host != "127.0.0.1" || target.Port < 55000 ||
    !(target.Database?.StartsWith("c4_complaints_test_", StringComparison.Ordinal) == true ||
      target.Database?.StartsWith("c4_complaints_restore_", StringComparison.Ordinal) == true))
    throw new InvalidOperationException("Refusing ordinary database target.");
var options = new DbContextOptionsBuilder<GovernanceIntelligenceDbContext>().UseNpgsql(connection,
    p => p.MigrationsHistoryTable("__ef_migrations_history", "governance_intelligence")).Options;
GovernanceIntelligenceDbContext Open() => new(options);
var checks = new List<string>();
void Check(bool ok, string name) { if (!ok) throw new Exception("FAILED: " + name); checks.Add(name); }
var restored = args.Contains("--restored", StringComparer.Ordinal);
using (var db = Open())
{
    var migrations = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
    if (!restored)
    {
        if (migrations.Length != 0) throw new Exception("Use a fresh isolated database; no reset/deletion supported.");
        await db.GetService<IMigrator>().MigrateAsync("20261016123700_AddDurableConsolidatedScreening");
        var legacy = Assessment(Guid.NewGuid(), "isolated-legacy");
        await new PostgresComplaintClassificationAssessmentStore(db).AddAsync(legacy);
        await db.GetService<IMigrator>().MigrateAsync("20261021170456_AddRegisteredComplaints");
        Check(await db.Set<RegisteredComplaintEntity>().CountAsync() == 0 &&
              await db.Set<RegisteredComplaintAssessmentLinkEntity>().CountAsync() == 0,
              "Bounded complaints migration creates empty aggregate/link tables");
        Check((await new PostgresComplaintClassificationAssessmentStore(db).GetByIdAsync(legacy.AssessmentId)) == legacy ||
              (await new PostgresComplaintClassificationAssessmentStore(db).GetByIdAsync(legacy.AssessmentId))?.ComplaintText == legacy.ComplaintText,
              "Existing standalone classifier history survives additive migration");
        await db.GetService<IMigrator>().MigrateAsync("20261021173048_AddOwnedRegulatoryProvenance");
    }
    Check((await db.Database.GetAppliedMigrationsAsync()).TakeLast(2).SequenceEqual(new[] {
        "20261021170456_AddRegisteredComplaints", "20261021173048_AddOwnedRegulatoryProvenance" }),
        "Complaints predecessor and regulatory provenance migration ordering is compatible");
    var restrictive = await db.Database.SqlQueryRaw<int>("""
        SELECT count(*)::integer AS "Value" FROM pg_constraint
        WHERE conrelid='governance_intelligence.registered_complaint_classifications'::regclass
        AND contype='f' AND confdeltype='r'
        """).SingleAsync();
    Check(restrictive == 2, "Both assessment association foreign keys use restrictive deletion");
}

var owner = Guid.NewGuid(); var key = Guid.NewGuid(); var access = new Access(); var clock = new Clock();
RegisteredComplaint Pending(Guid actor, Guid submission) => new(Guid.NewGuid(), actor, submission, "test-fingerprint",
    "Isolated complaint", "Original fictional concern; not an allegation about a real person.", access.Case, null,
    clock.GetUtcNow(), 0, "Pending", "Open", [], []);
async Task<bool> Insert(RegisteredComplaint row)
{
    using var db = Open(); return await new RegisteredComplaintStore(db, new PostgresComplaintClassificationAssessmentStore(db)).InsertAsync(row, default);
}
var duplicates = await Task.WhenAll(Insert(Pending(owner, key)), Insert(Pending(owner, key)));
Check(duplicates.Count(x => x) == 1, "Concurrent applicant/submission-key inserts have exactly one unique winner");
Check(await Insert(Pending(Guid.NewGuid(), key)), "Submission-key uniqueness is scoped to applicant rather than global");

var cas = Pending(owner, Guid.NewGuid()); await Insert(cas);
async Task<bool> SaveCas(string reason)
{
    using var db = Open(); var store = new RegisteredComplaintStore(db, new PostgresComplaintClassificationAssessmentStore(db));
    try { await store.SaveAsync(cas with { Revision = 1, ReviewStatus = "UnderReview",
        Reviews = [new(Guid.NewGuid(), access.Reviewer, clock.GetUtcNow(), "UnderReview", reason)] }, 0, null, default); return true; }
    catch (ComplaintConcurrencyException) { return false; }
}
var races = await Task.WhenAll(SaveCas("First isolated reason"), SaveCas("Second isolated reason"));
Check(races.Count(x => x) == 1, "PostgreSQL revision CAS admits one concurrent review and rejects stale update");

var rollback = Pending(owner, Guid.NewGuid()); await Insert(rollback); var attemptedAssessment = Assessment(Guid.NewGuid(), "registered-complaint:" + rollback.Id);
using (var db = Open())
{
    var real = new PostgresComplaintClassificationAssessmentStore(db);
    var store = new RegisteredComplaintStore(db, new FailAfterAssessmentWrite(real));
    try { await store.SaveAsync(rollback with { Revision = 1, ClassificationState = "Classified" }, 0, attemptedAssessment, default); throw new Exception("Injected failure was ignored"); }
    catch (InjectedFailure) { }
}
using (var db = Open())
{
    var assessments = new PostgresComplaintClassificationAssessmentStore(db); var store = new RegisteredComplaintStore(db, assessments);
    Check((await store.GetAsync(rollback.Id, default))!.Revision == 0 &&
          await assessments.GetByIdAsync(attemptedAssessment.AssessmentId) == null &&
          await store.ComplaintForAssessmentAsync(attemptedAssessment.AssessmentId, default) == null,
          "Failure after actual assessment INSERT rolls back complaint revision, assessment and association atomically");
}

var classifier = new TestClassifier();
using (var db = Open())
{
    var saved = new PostgresComplaintClassificationAssessmentStore(db); var store = new RegisteredComplaintStore(db, saved);
    var service = new RegisteredComplaintService(store, access, classifier, saved, clock);
    var input = new SubmitRegisteredComplaint(Guid.NewGuid(), "Isolated concern", "  Original English text is retained exactly.  ", "test-case", null);
    classifier.During = async () => {
        using var probe = Open(); var copy = new RegisteredComplaintStore(probe, new PostgresComplaintClassificationAssessmentStore(probe));
        var row = await copy.FindSubmissionAsync(owner, input.SubmissionKey, default);
        if (row?.ClassificationState != "InProgress" || row.Revision != 1) throw new Exception("Registration/claim not committed before inference");
        checks.Add("Registration and InProgress claim are committed and visible to another session before classifier call");
    };
    var failed = await service.SubmitAsync(owner, input, default); classifier.During = null;
    Check(failed.ClassificationState == "Failed" && failed.ComplaintText == input.ComplaintText &&
          failed.SubmittedAtUtc == clock.GetUtcNow() && failed.Attempts.Single().AssessmentId == null,
          "ML failure preserves original/timestamp and records failure without invented prediction");
    var replay = await service.SubmitAsync(owner, input, default);
    Check(replay.Id == failed.Id && classifier.Calls == 1, "Duplicate service submission returns stored complaint without another classifier call");
    try { await service.SubmitAsync(owner, input with { ComplaintText = "Different request" }, default); throw new Exception("Conflict accepted"); }
    catch (ComplaintConcurrencyException) { checks.Add("Same applicant/key with changed payload conflicts without duplicate registration"); }
    classifier.Fail = false;
    var success = await service.ClassifyAsync(owner, false, failed.Id, failed.Revision, default);
    var detail = await service.DetailAsync(owner, false, success.Id, default);
    Check(success.Id == failed.Id && success.ClassificationState == "Classified" && success.Attempts.Length == 2 &&
          success.Attempts[0].State == "Failed" && detail.Classifications.Count == 1 &&
          detail.Classifications[0].ModelVersion == "isolated-test-double-not-live-ML" &&
          detail.Classifications[0].ClassProbabilities.Count == 4 && success.ReviewStatus == "Open" &&
          await store.ComplaintForAssessmentAsync(detail.Classifications[0].AssessmentId, default) == success.Id,
          "Controlled retry saves assessment/link under column grants despite sub-microsecond original timestamp precision");
    try { await service.ClassifyAsync(owner, false, success.Id, success.Revision, default); throw new Exception("Successful classification repeated"); }
    catch (ComplaintConcurrencyException) { checks.Add("Successful classification cannot be repeated by retry"); }
    try { await service.DetailAsync(Guid.NewGuid(), false, success.Id, default); throw new Exception("Other applicant read accepted"); }
    catch (UnauthorizedAccessException) { checks.Add("Other applicant denied against persisted ownership"); }
    try { await service.DetailAsync(Guid.NewGuid(), true, success.Id, default); throw new Exception("Unassigned officer accepted"); }
    catch (UnauthorizedAccessException) { checks.Add("Officer without case assignment denied against persisted complaint"); }
    Check((await service.ListAsync(access.Reviewer, true, default)).Any(x => x.Id == success.Id), "Assigned officer inbox uses existing assignment port");
    try { await service.ReviewAsync(access.Reviewer, success.Id, success.Revision, "Verified", "Not an available authority", default); throw new Exception("Invented power accepted"); }
    catch (ArgumentException) { checks.Add("Triage rejects unsupported verification/resolution authority"); }
    var reviewed = await service.ReviewAsync(access.Reviewer, success.Id, success.Revision, "UnderReview", "Read original concern", default);
    var next = await service.ReviewAsync(access.Reviewer, success.Id, reviewed.Revision, "InformationRequested", "Request supporting details", default);
    Check(next.Reviews.Length == 2 && next.ComplaintText == input.ComplaintText && next.ClassificationState == "Classified",
          "Reasoned review history appends without changing original or classification");
    try { await service.ReviewAsync(access.Reviewer, success.Id, reviewed.Revision, "ReviewRecorded", "Stale", default); throw new Exception("Stale review accepted"); }
    catch (ComplaintConcurrencyException) { checks.Add("Stale review is rejected with preserved history"); }
    access.Revoked = true;
    try { await service.DetailAsync(access.Reviewer, true, success.Id, default); throw new Exception("Revoked reviewer accepted"); }
    catch (UnauthorizedAccessException) { checks.Add("Revoked officer assignment denies persisted complaint detail"); }
}
Console.WriteLine(JsonSerializer.Serialize(new { status = "passed", database = target.Database, port = target.Port,
    restoredSchema = restored, noOrdinaryDatabaseAccess = true, noRealMLOrRegulatoryEngineCalls = true, checks }, new JsonSerializerOptions { WriteIndented = true }));

static ComplaintClassificationAssessment Assessment(Guid id, string binding) => new(id, binding, "Fictional isolated test input",
    "isolated-test-double-not-live-ML", ComplaintClassificationCategories.AdministrativeProceduralIntegrity,
    ComplaintClassificationCategories.All.ToDictionary(x => x, x => x == ComplaintClassificationCategories.AdministrativeProceduralIntegrity ? 1d : 0d),
    "Advisory test prediction", "Closed-set test; no allegation verified", DateTimeOffset.Parse("2026-10-22T00:00:00Z"));
sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => DateTimeOffset.Parse("2026-10-22T00:00:00Z").AddTicks(7); }
sealed class Access : IComplaintReferenceAccess
{
    public Guid Reviewer = Guid.NewGuid(), Case = Guid.NewGuid(); public bool Revoked;
    public Task<ComplaintReference> ValidateAsync(string? c, string? p, CancellationToken ct) => Task.FromResult(new ComplaintReference(c == null ? null : Case, p));
    public Task<IReadOnlyList<Guid>> ReviewerCasesAsync(Guid actor, CancellationToken ct) => Task.FromResult<IReadOnlyList<Guid>>(actor == Reviewer && !Revoked ? [Case] : []);
}
sealed class TestClassifier : IComplaintClassificationClient
{
    public int Calls; public bool Fail = true; public Func<Task>? During;
    public async Task<ComplaintClassificationResult> ClassifyAsync(string text, string? binding = null, CancellationToken ct = default)
    {
        Calls++; if (During != null) await During();
        return Fail ? ComplaintClassificationResult.Failure(ComplaintClassificationErrorCode.ServiceUnavailable, "Isolated test failure") :
            ComplaintClassificationResult.Success(new("isolated-test-double-not-live-ML", ComplaintClassificationCategories.AdministrativeProceduralIntegrity,
            ComplaintClassificationCategories.All.ToDictionary(x => x, x => x == ComplaintClassificationCategories.AdministrativeProceduralIntegrity ? 1d : 0d), binding, "advisory", "closed-set"));
    }
}
sealed class InjectedFailure : Exception { }
sealed class FailAfterAssessmentWrite(IComplaintClassificationAssessmentStore inner) : IComplaintClassificationAssessmentStore
{
    public async Task AddAsync(ComplaintClassificationAssessment a, CancellationToken ct = default) { await inner.AddAsync(a, ct); throw new InjectedFailure(); }
    public Task<ComplaintClassificationAssessment?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
    public Task<IReadOnlyList<ComplaintClassificationAssessment>> GetByCaseIdAsync(string id, int limit = 100, CancellationToken ct = default) => inner.GetByCaseIdAsync(id, limit, ct);
}
