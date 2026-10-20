namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using StateLandGovernance.GovernanceIntelligence.Application.Screening.Contracts;
using StateLandGovernance.GovernanceIntelligence.Application.Screening.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

public sealed class PostgresConsolidatedGovernanceScreeningStore(GovernanceIntelligenceDbContext db) : IConsolidatedGovernanceScreeningStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static ConsolidatedGovernanceScreeningResultDto? Read(ConsolidatedScreeningEntity? row) => row == null ? null :
        JsonSerializer.Deserialize<ConsolidatedGovernanceScreeningResultDto>(row.ResultJson, Json);
    public async Task SaveAsync(ConsolidatedGovernanceScreeningResultDto result, CancellationToken ct = default)
    {
        if (await db.Set<ConsolidatedScreeningEntity>().AnyAsync(r => r.Id == result.AssessmentId, ct))
            throw new InvalidOperationException("Consolidated assessments are append-only; existing IDs cannot be replaced.");
        db.Add(new ConsolidatedScreeningEntity { Id = result.AssessmentId, CaseId = result.CaseId,
            ProposalVersion = result.ProposalVersion, SnapshotSha256 = result.SnapshotSha256,
            EvaluatedAtUtc = result.EvaluatedAtUtc, ResultJson = JsonSerializer.Serialize(result, Json) });
        await db.SaveChangesAsync(ct);
    }
    public async Task<ConsolidatedGovernanceScreeningResultDto?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Read(await db.Set<ConsolidatedScreeningEntity>().AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct));
    public async Task<ConsolidatedGovernanceScreeningResultDto?> GetLatestByCaseIdAsync(string caseId, CancellationToken ct = default) =>
        Read(await db.Set<ConsolidatedScreeningEntity>().AsNoTracking().Where(r => r.CaseId == caseId).OrderByDescending(r => r.EvaluatedAtUtc).FirstOrDefaultAsync(ct));
    public async Task<ConsolidatedGovernanceScreeningResultDto?> FindByIdempotencyKeyAsync(string caseId, string proposalVersion, string hash, CancellationToken ct = default) =>
        Read(await db.Set<ConsolidatedScreeningEntity>().AsNoTracking().SingleOrDefaultAsync(r => r.CaseId == caseId && r.ProposalVersion == proposalVersion && r.SnapshotSha256 == hash, ct));
    public async Task<IReadOnlyList<ConsolidatedGovernanceScreeningResultDto>> GetHistoryByCaseIdAsync(string caseId, int limit = 50, CancellationToken ct = default) =>
        (await db.Set<ConsolidatedScreeningEntity>().AsNoTracking().Where(r => r.CaseId == caseId).OrderByDescending(r => r.EvaluatedAtUtc)
            .Take(Math.Clamp(limit, 1, 200)).ToListAsync(ct)).Select(r => Read(r)!).ToArray();
}
