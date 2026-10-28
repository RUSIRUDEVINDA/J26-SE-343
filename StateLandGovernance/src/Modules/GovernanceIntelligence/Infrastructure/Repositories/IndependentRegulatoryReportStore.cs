using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;

public sealed class IndependentRegulatoryReportStore(GovernanceIntelligenceDbContext db) : IIndependentRegulatoryReportStore
{
    public async Task<IReadOnlyList<Guid>> SavedIdsAsync(CancellationToken ct)=>await db.ComplianceEvaluations.AsNoTracking().Select(x=>x.Id).ToArrayAsync(ct);
    public async Task<IReadOnlyList<IndependentRegulatorySummary>> ListAsync(IReadOnlyList<Guid> allowedIds, int limit, CancellationToken ct)
    {
        var rows = await db.ComplianceEvaluations.AsNoTracking().Where(x => allowedIds.Contains(x.Id))
            .OrderByDescending(x => x.EvaluationTimestamp).ThenBy(x => x.Id).Take(limit).ToListAsync(ct);
        return rows.Select(Metadata).ToArray();
    }
    public async Task<IndependentRegulatoryReport?> GetAsync(Guid id, CancellationToken ct)
    {
        var row = await db.ComplianceEvaluations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (row == null) return null;
        IReadOnlyList<ComplianceFindingDto>? findings = null;
        if (!string.IsNullOrWhiteSpace(row.FindingsJson))
        {
            try
            {
                findings = JsonSerializer.Deserialize<ComplianceFindingDto[]>(row.FindingsJson,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if (findings == null || findings.Any(f => f == null || string.IsNullOrWhiteSpace(f.RuleCode) ||
                    string.IsNullOrWhiteSpace(f.Status) || string.IsNullOrWhiteSpace(f.Applicability)))
                    throw new IndependentRegulatoryReportUnavailableException();
            }
            catch (JsonException) { throw new IndependentRegulatoryReportUnavailableException(); }
        }
        // Only persisted fields. No reconstructed actor, snapshot, input, policy digest, or currentness.
        return new(Metadata(row), findings);
    }
    private static IndependentRegulatorySummary Metadata(ComplianceEvaluationEntity x) => new(x.Id,
        x.AuditRecordId, x.CaseId, x.ProposalId, x.ActionName, x.Status, x.EvaluationTimestamp,
        x.RuleSetVersion, x.DeterministicEvaluationId);
}
public sealed class UnavailableIndependentRegulatoryReportStore : IIndependentRegulatoryReportStore
{
    public Task<IReadOnlyList<IndependentRegulatorySummary>> ListAsync(IReadOnlyList<Guid> ids, int limit, CancellationToken ct)
        => throw new IndependentRegulatoryReportUnavailableException();
    public Task<IndependentRegulatoryReport?> GetAsync(Guid id, CancellationToken ct)
        => throw new IndependentRegulatoryReportUnavailableException();
}
