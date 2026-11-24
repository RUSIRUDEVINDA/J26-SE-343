using StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.Projections;
using StateLandGovernance.GovernanceIntelligence.Application.WorkflowAnomaly;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;

public sealed class PostWorkflowDemonstrationStore(GovernanceIntelligenceDbContext db,
    IComponent3WorkflowGovernanceProjectionStore projections, IWorkflowAnomalyAssessmentStore assessments,
    AssessWorkflowAnomalyCommandHandler handler, TimeProvider clock,ICommissionerReportReadAccess? commissioner=null) : IPostWorkflowDemonstrationStore
{
    private static void Authorize(Guid actor) { if (actor == Guid.Empty) throw new UnauthorizedAccessException(); }
    private async Task<PostWorkflowReport> Report(PostWorkflowDemonstrationEntity row, CancellationToken ct)
    {
        // Pending reservation may predate projection persistence after interruption. Recreate only this
        // explicitly versioned server fixture; never reconstruct historical C3 evidence.
        var projection = await projections.GetByIdAsync(row.ProjectionId, ct)
            ?? PostWorkflowDemonstrations.Create(row.Scenario, row.WorkflowRunId, row.ProjectionId, row.CreatedAtUtc);
        var assessment = row.AssessmentId is {} id ? await assessments.GetByIdAsync(id, ct) : null;
        return new(row.Id, row.CreatorActorId, row.Scenario, row.Version, row.CreatedAtUtc, row.State,
            row.Failure, projection, assessment, row.Attempt,
            JsonSerializer.Deserialize<PostWorkflowAttempt[]>(row.AttemptsJson) ?? []);
    }
    public async Task<IReadOnlyList<PostWorkflowReport>> ListAsync(Guid actor, CancellationToken ct)
    {
        Authorize(actor);
        var all=commissioner!=null&&await commissioner.CanReadAllAsync(actor,ct);
        var rows = await db.Set<PostWorkflowDemonstrationEntity>().AsNoTracking().Where(x => x.CreatorActorId == actor || all&&x.State=="Saved")
            .OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
        var result = new List<PostWorkflowReport>();
        foreach (var row in rows) result.Add(await Report(row, ct));
        var still=all&&await commissioner!.CanReadAllAsync(actor,ct);return result.Where(x=>still||x.CreatorActorId==actor).ToArray();
    }
    public async Task<PostWorkflowReport> GetAsync(Guid actor, Guid id, CancellationToken ct)
    {
        Authorize(actor);
        var row = await db.Set<PostWorkflowDemonstrationEntity>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (row == null) throw new KeyNotFoundException();
        if(row.CreatorActorId!=actor&&!(row.State=="Saved"&&commissioner!=null&&await commissioner.CanReadAllAsync(actor,ct)))throw new UnauthorizedAccessException();
        var report=await Report(row,ct);if(row.CreatorActorId!=actor&&!await commissioner!.CanReadAllAsync(actor,ct))throw new UnauthorizedAccessException();return report;
    }
    public async Task<PostWorkflowReport> RunAsync(Guid actor, string scenario, bool retry, CancellationToken ct, int expectedAttempt = 0)
    {
        Authorize(actor);
        if (!PostWorkflowDemonstrations.Scenarios.Contains(scenario, StringComparer.Ordinal)) throw new ArgumentException("Unknown demonstration.");
        // Reservation commits before inference. A process crash leaves Pending, which is never
        // automatically rerun. Repeated clicks and uncertain HTTP outcomes retrieve the same job.
        var version = PostWorkflowDemonstrations.VersionFor(scenario);
        var set = db.Set<PostWorkflowDemonstrationEntity>();
        var row = await set.SingleOrDefaultAsync(x => x.CreatorActorId == actor && x.Scenario == scenario && x.Version == version, ct);
        var fresh = row == null;
        if (row == null)
        {
            row = new() { Id = Guid.NewGuid(), CreatorActorId = actor, Scenario = scenario, Version = version,
                CreatedAtUtc = clock.GetUtcNow(), WorkflowRunId = Guid.NewGuid(), ProjectionId = Guid.NewGuid() };
            set.Add(row);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException e) when (e.InnerException is Npgsql.PostgresException { SqlState: "23505" })
            {
                db.Entry(row).State = EntityState.Detached;
                row = await set.SingleAsync(x => x.CreatorActorId == actor && x.Scenario == scenario && x.Version == version, ct);
                fresh = false;
            }
        }
        if (!fresh && !(retry && row.State == "Failed" && row.Attempt == expectedAttempt)) return await Report(row, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Cross-process lock prevents concurrent explicit retries from invoking ML twice.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({row.Id.ToString()}, 0))", ct);
        await db.Entry(row).ReloadAsync(ct);
        if ((!fresh && (row.State != "Failed" || row.Attempt != expectedAttempt)) || row.State == "Saved") return await Report(row, ct);
        if (!fresh) row.Attempt++;
        var projection = PostWorkflowDemonstrations.Create(scenario, row.WorkflowRunId, row.ProjectionId, row.CreatedAtUtc);
        await projections.UpsertAsync(projection, ct);
        var result = await handler.HandleAsync(AssessWorkflowAnomalyCommand.FromProjection(projection.ProjectionId, projection.CaseId, projection.WorkflowRunId), ct);
        row.State = result.IsSuccess ? "Saved" : "Failed";
        row.AssessmentId = result.Assessment?.AssessmentId;
        row.Failure = result.IsSuccess ? null : "The ML service did not produce a valid saved assessment. No prediction is inferred. Explicit retry is required.";
        var attempts = (JsonSerializer.Deserialize<PostWorkflowAttempt[]>(row.AttemptsJson) ?? []).ToList();
        attempts.Add(new(row.Attempt, clock.GetUtcNow(), row.State, row.Failure));
        row.AttemptsJson = JsonSerializer.Serialize(attempts);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await Report(row, ct);
    }
}
