using StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StateLandGovernance.GovernanceIntelligence.Application.Demonstrations;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
public sealed class KiwulaAssessmentStore(GovernanceIntelligenceDbContext db, IConfiguration config, TimeProvider clock,ICommissionerReportReadAccess? commissioner=null) : IKiwulaAssessmentStore
{
    private DbSet<KiwulaAssessmentEntity> Rows => db.Set<KiwulaAssessmentEntity>();
    private static KiwulaReport Read(KiwulaAssessmentEntity row) => JsonSerializer.Deserialize<KiwulaReport>(row.PayloadJson, KiwulaCatalogue.Json) ?? throw new KiwulaUnavailableException();
    public async Task<bool> ReadyAsync(CancellationToken ct)
    {
        if (!config.GetValue<bool>("GovernanceIntelligence:KiwulaWorkspace:WritesEnabled")) return false;
        try { return await db.Database.SqlQueryRaw<bool>("""
            SELECT current_setting('transaction_read_only')='off' AND
            has_table_privilege(current_user,'governance_intelligence.kiwula_assessment_provenance','INSERT') AND
            has_table_privilege(current_user,'governance_intelligence.conflict_evaluations','INSERT') AND
            has_table_privilege(current_user,'governance_intelligence.conflict_findings','INSERT') AND
            has_table_privilege(current_user,'governance_intelligence.early_governance_screening_evaluations','INSERT') AND
            has_table_privilege(current_user,'governance_intelligence.governance_audit_records','INSERT') AND
            has_table_privilege(current_user,'governance_intelligence.governance_audit_anchor_outbox','INSERT') AS "Value"
            """).SingleAsync(ct); }
        catch (OperationCanceledException) { throw; } catch { return false; }
    }
    public async Task<IReadOnlyList<KiwulaReport>> ListAsync(Guid actor, CancellationToken ct)
    {
        if (actor == Guid.Empty) throw new UnauthorizedAccessException();
        var all=commissioner!=null&&await commissioner.CanReadAllAsync(actor,ct);var rows=await Rows.AsNoTracking().Where(x=>all||x.CreatorActorId==actor).ToArrayAsync(ct);var still=all&&await commissioner!.CanReadAllAsync(actor,ct);return rows.Where(x=>still||x.CreatorActorId==actor).Select(Read).OrderByDescending(x=>x.CreatedAtUtc).ToArray();
    }
    public async Task<KiwulaReport> GetAsync(Guid actor, Guid id, CancellationToken ct)
    {
        var row = await Rows.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException();
        if(actor==Guid.Empty||row.CreatorActorId!=actor&&!(commissioner!=null&&await commissioner.CanReadAllAsync(actor,ct)))throw new UnauthorizedAccessException();var result=Read(row);if(row.CreatorActorId!=actor&&!await commissioner!.CanReadAllAsync(actor,ct))throw new UnauthorizedAccessException();return result;
    }
    public async Task<KiwulaReport> RunAsync(Guid actor, KiwulaRunRequest request, string inputJson, Func<Guid, Task<KiwulaComputed>> compute, CancellationToken ct)
    {
        if (actor == Guid.Empty) throw new UnauthorizedAccessException();
        if (!await ReadyAsync(ct)) throw new KiwulaUnavailableException();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Local deterministic computation only. Serialize one creator/key before engine execution.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({actor + ":" + request.SubmissionKey}, 0))", ct);
        var existing = await Rows.AsNoTracking().SingleOrDefaultAsync(x => x.CreatorActorId == actor && x.SubmissionKey == request.SubmissionKey, ct);
        var hash = KiwulaCatalogue.Hash(inputJson);
        if (existing != null) { var saved = Read(existing); if (saved.Engine != request.Engine || saved.InputHash != hash || saved.InputVersion != KiwulaCatalogue.Version) throw new KiwulaSubmissionConflictException(); return saved; }
        var id = Guid.NewGuid(); var at = clock.GetUtcNow(); var actual = await compute(id);
        Guid evaluation = id; Guid? audit = null;
        if (request.Engine == "conflict") { var row = await db.ConflictEvaluations.SingleAsync(x => x.ActionName == KiwulaCatalogue.Prefix + id, ct); evaluation = row.Id; audit = row.AuditRecordId; }
        var report = new KiwulaReport(id, actor, request.SubmissionKey, request.Engine, KiwulaCatalogue.ProposalId, KiwulaCatalogue.Version,
            hash, typeof(Domain.Services.GovernanceConflictEngine).Assembly.GetName().Version?.ToString() ?? "Unavailable", at, evaluation, audit,
            "C4 server Kiwula example catalogue; English adaptation; unverified example decisions / missing indicator evidence; no official approvals",
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(Domain.Services.GovernanceConflictEngine).Assembly.Location))).ToLowerInvariant(),
            inputJson, JsonSerializer.Deserialize<JsonElement>(inputJson), actual.Result);
        Rows.Add(new() { Id = id, CreatorActorId = actor, SubmissionKey = request.SubmissionKey, Engine = request.Engine,
            ConflictEvaluationId = request.Engine == "conflict" ? evaluation : null, EarlyAssessmentId = request.Engine == "early" ? evaluation : null,
            PayloadJson = JsonSerializer.Serialize(report, KiwulaCatalogue.Json) });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return report;
    }
}
