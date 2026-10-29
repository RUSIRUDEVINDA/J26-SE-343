using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;

public sealed class ComplaintRoutingStore(GovernanceIntelligenceDbContext db, IComplaintActorContext actor,
    ComplaintRoutingPolicy policy, TimeProvider clock, IRegisteredComplaintStore complaints, IComplaintHandlingStore? handling = null) : IComplaintRoutingStore
{
    private DbSet<ComplaintAssignmentEntity> Events => db.Set<ComplaintAssignmentEntity>();
    private IQueryable<ComplaintAssignmentEntity> Current => Events.AsNoTracking().Where(e => !Events.Any(n => n.ComplaintId == e.ComplaintId && n.Revision > e.Revision));
    public async Task<ComplaintAssignment?> CurrentAsync(Guid id, CancellationToken ct) => (await Current.SingleOrDefaultAsync(x => x.ComplaintId == id, ct))?.ToRecord();
    public async Task<IReadOnlyList<ComplaintAssignment>> HistoryAsync(Guid id, CancellationToken ct) =>
        (await Events.AsNoTracking().Where(x => x.ComplaintId == id).OrderBy(x => x.Revision).ToArrayAsync(ct)).Select(x => x.ToRecord()).ToArray();
    public async Task<IReadOnlyList<RegisteredComplaint>> AssignedAsync(Guid target, CancellationToken ct)
    {
        if (!await policy.EligibleAsync(target, ct)) return [];
        var offices = (await policy.SnapshotAsync(ct)).Officers.Where(x => x.ActorId == target).Select(x => x.OfficeCode).ToArray();
        var rows = await db.Set<RegisteredComplaintEntity>().AsNoTracking()
            .Where(x => Current.Any(e => e.ComplaintId == x.Id && e.TargetActorId == target && offices.Contains(e.TargetOfficeCode)&&(handling==null||!db.Set<ComplaintHandlingEntity>().Any(h=>h.ComplaintId==x.Id)||db.Set<ComplaintHandlingEntity>().Any(h=>h.ComplaintId==x.Id&&h.HandlingMode=="Ds"&&h.AssignmentEventId==e.Id&&!db.Set<ComplaintHandlingEntity>().Any(n=>n.ComplaintId==h.ComplaintId&&n.Revision>h.Revision)))))
            .OrderByDescending(x => x.SubmittedAtUtc).ThenBy(x => x.Id).Take(100).ToArrayAsync(ct);
        var allowed = new List<RegisteredComplaint>();
        foreach (var row in rows) if (await IsAssignedAsync(target, row.Id, ct)) allowed.Add(JsonSerializer.Deserialize<RegisteredComplaint>(row.PayloadJson)!);
        return allowed;
    }
    public async Task<(IReadOnlyList<RegisteredComplaint> Items, string? Next)> QueueAsync(bool assigned, string? cursor, CancellationToken ct)
    {
        if(handling!=null)return await handling.QueueAsync(assigned?"ds":"not-ds",cursor,ct);
        Guid? after = null;
        if (cursor != null) { if (!Guid.TryParseExact(cursor, "D", out var id)) throw new ArgumentException("Invalid queue cursor."); after = id; }
        // Server filtering precedes keyset pagination; a queue is never the first global 100 filtered in memory.
        var query = db.Set<RegisteredComplaintEntity>().AsNoTracking().Where(x => Events.Any(e => e.ComplaintId == x.Id) == assigned);
        var anchor = after == null ? null : await db.Set<RegisteredComplaintEntity>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == after.Value, ct);
        if (after != null && anchor == null) throw new ArgumentException("Queue cursor expired. Refresh the queue.");
        var rows = await ComplaintQueueOrder.Apply(query, anchor).Take(51).ToArrayAsync(ct);
        return (rows.Take(50).Select(x => JsonSerializer.Deserialize<RegisteredComplaint>(x.PayloadJson)!).ToArray(), rows.Length > 50 ? rows[49].Id.ToString("D") : null);
    }
    private async Task<bool> IsAssignedAsync(Guid target, Guid id, CancellationToken ct)
    {
        if(handling!=null)return await handling.IsAssignedAsync(target,id,ct);
        if (!await policy.EligibleAsync(target, ct)) return false;
        var current = await CurrentAsync(id, ct);
        return current != null && current.TargetActorId == target && (await policy.SnapshotAsync(ct)).Officers.Any(x => x.ActorId == target && x.OfficeCode == current.TargetOfficeCode);
    }
    // Same local transaction lock is used for assignment and triage writes. Never spans ML/HTTP.
    private Task LockAsync(Guid complaint, CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync(
        $"SELECT pg_advisory_xact_lock(hashtextextended({"c4-complaint:" + complaint.ToString("D")}, 0))", ct);
    public async Task<ComplaintAssignmentResult> AssignAsync(Guid complaint, AssignComplaint input, CancellationToken ct)
    {
        await policy.RequireCentralAsync(actor.ActorId, true, ct); // Permission before existence lookup.
        if (handling!=null && input.ExpectedHandlingRevision==null)throw new ArgumentException("Expected handling revision is required.");
        if (input.SubmissionKey == Guid.Empty || input.TargetActorId == Guid.Empty || input.ExpectedAssignmentRevision < 0 ||
            string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length > 2000) throw new ArgumentException("Choose an officer, current revision and a reason (up to 2000 characters).");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await LockAsync(complaint, ct);
            var grant = await policy.RequireCentralAsync(actor.ActorId, true, ct);
            if (!await db.Set<RegisteredComplaintEntity>().AnyAsync(x => x.Id == complaint, ct)) throw new KeyNotFoundException();
            var hash = ComplaintRoutingPolicy.InputHash(complaint, input);
            var old = await Events.AsNoTracking().SingleOrDefaultAsync(x => x.AssignedByActorId == actor.ActorId && x.SubmissionKey == input.SubmissionKey, ct);
            var current = await CurrentAsync(complaint, ct);
            if (old != null)
            {
                if (old.InputSha256 != hash || old.ComplaintId != complaint) throw new ComplaintConcurrencyException();
                await policy.RequireCentralAsync(actor.ActorId, true, ct);
                await tx.CommitAsync(ct);
                return new(old.ToRecord(), current!, true,handling==null?null:await handling.CurrentAsync(complaint,ct));
            }
            var handlingBefore=handling==null?null:await handling.PrepareAssignmentAsync(complaint,input,ct);
            if ((current?.Revision ?? 0) != input.ExpectedAssignmentRevision) throw new ComplaintConcurrencyException();
            var snapshot = await policy.SnapshotAsync(ct);
            var targets = snapshot.Officers.Where(x => x.ActorId == input.TargetActorId).ToArray();
            if (targets.Length != 1) throw new ArgumentException("The selected officer is no longer eligible.");
            var target = targets[0];
            if ((handlingBefore==null||handlingBefore.Mode=="Ds") && current?.TargetActorId == target.ActorId && current.TargetOfficeCode == target.OfficeCode) throw new ComplaintConcurrencyException();
            var entity = new ComplaintAssignmentEntity
            {
                Id = Guid.NewGuid(), ComplaintId = complaint, Revision = (current?.Revision ?? 0) + 1,
                TargetActorId = target.ActorId, TargetOfficeCode = target.OfficeCode,
                Reason = input.Reason, SubmissionKey = input.SubmissionKey, InputSha256 = hash,
                PreviousAssignmentId = current?.Id, DirectorySnapshotSha256 = snapshot.Sha256,
                AuthorizationGrantId = grant.GrantId, AuthorizationSourceReference = grant.SourceReference
            };
            // Re-read both trusted sources immediately before the local write.
            var fresh = await policy.SnapshotAsync(ct);
            if (fresh.Sha256 != snapshot.Sha256) throw new ComplaintConcurrencyException();
            await policy.RequireCentralAsync(actor.ActorId, true, ct);
            Events.Add(entity); await db.SaveChangesAsync(ct);
            if(handling!=null)await handling.RecordAssignmentAsync(handlingBefore!,entity.ToRecord(),input,ct);
            await policy.RequireCentralAsync(actor.ActorId, true, ct);
            if ((await policy.SnapshotAsync(ct)).Sha256 != snapshot.Sha256) throw new ComplaintConcurrencyException();
            await tx.CommitAsync(ct);
            return new(entity.ToRecord(), entity.ToRecord(), false,handling==null?null:await handling.CurrentAsync(complaint,ct));
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException p && p.SqlState == "23505")
        { db.ChangeTracker.Clear(); throw new ComplaintConcurrencyException(); }
    }
    public async Task<RegisteredComplaint> ReviewAsync(Guid target, Guid id, int revision, string status, string reason, CancellationToken ct, int? expectedHandlingRevision = null)
    {
        if (actor.ActorId != target) throw new KeyNotFoundException();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(id, ct);
        if (!await IsAssignedAsync(target, id, ct)) throw new KeyNotFoundException();
        var handlingBefore=handling==null?null:await handling.CurrentAsync(id,ct);
        if(handlingBefore!=null&&expectedHandlingRevision==null)throw new ArgumentException("Expected handling revision is required.");
        if(handlingBefore!=null&&handlingBefore.Revision!=expectedHandlingRevision)throw new ComplaintConcurrencyException();
        var row = await complaints.GetAsync(id, ct) ?? throw new KeyNotFoundException();
        if (row.Revision != revision) throw new ComplaintConcurrencyException();
        var updated = row with { Revision = revision + 1, ReviewStatus = status,
            Reviews = row.Reviews.Append(new ComplaintReview(Guid.NewGuid(), target, clock.GetUtcNow(), status, reason)).ToArray() };
        if (!await IsAssignedAsync(target, id, ct)) throw new KeyNotFoundException();
        await complaints.SaveAsync(updated, revision, null, ct);
        if(handling!=null)await handling.RecordReviewAsync(id,handlingBefore!,updated.Reviews.Last(),ct);
        await tx.CommitAsync(ct);
        return updated;
    }
}
