using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
namespace StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;

public sealed record IndependentRegulatorySummary(Guid Id, Guid AuditRecordId, string? CaseId,
    string? ProposalId, string ActionName, string Status, DateTime EvaluationTimestamp,
    string RuleSetVersion, string? DeterministicEvaluationId);
public sealed record IndependentRegulatoryReport(IndependentRegulatorySummary Metadata,
    IReadOnlyList<ComplianceFindingDto>? Findings, SavedRegulatoryProvenance? Provenance=null);
public interface IIndependentRegulatoryReportStore
{
    Task<IReadOnlyList<Guid>> SavedIdsAsync(CancellationToken ct)=>Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>());
    Task<IReadOnlyList<IndependentRegulatorySummary>> ListAsync(IReadOnlyList<Guid> allowedIds, int limit, CancellationToken ct);
    Task<IndependentRegulatoryReport?> GetAsync(Guid id, CancellationToken ct);
}
// Trusted assignment source; never derive ownership from an arbitrary stored text label or DS role.
public interface IIndependentRegulatoryReadAssignments
{
    Task<IReadOnlyList<Guid>> ReadAllowedIdsAsync(Guid actorId, CancellationToken ct);
}
public sealed class DenyIndependentRegulatoryReadAssignments : IIndependentRegulatoryReadAssignments
{
    public Task<IReadOnlyList<Guid>> ReadAllowedIdsAsync(Guid actorId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>());
}
public sealed class IndependentRegulatoryReportUnavailableException : Exception
{
    public IndependentRegulatoryReportUnavailableException() : base("Stored regulatory report data is unavailable.") { }
}
public sealed class IndependentRegulatoryReportQueries(IIndependentRegulatoryReportStore store,
    IIndependentRegulatoryReadAssignments assignments, IRegulatorySubmissionStore? ownership=null,ICommissionerReportReadAccess? commissioner=null)
{
    private async Task<IReadOnlyList<Guid>> Allowed(Guid actor,CancellationToken ct)=>commissioner!=null&&await commissioner.CanReadAllAsync(actor,ct)?await store.SavedIdsAsync(ct):(await assignments.ReadAllowedIdsAsync(actor,ct)).Concat(ownership==null?Array.Empty<Guid>():await ownership.OwnedIdsAsync(actor,ct)).Distinct().ToArray();
    public Task<IReadOnlyList<Guid>> ReadableIdsAsync(Guid actorId, CancellationToken ct)
    {
        if (actorId == Guid.Empty) throw new UnauthorizedAccessException();
        return Allowed(actorId, ct);
    }
    public async Task<IReadOnlyList<IndependentRegulatorySummary>> ListAsync(Guid actorId, int limit, CancellationToken ct)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        if (actorId == Guid.Empty) throw new UnauthorizedAccessException();
        var allowed = await Allowed(actorId, ct);
        if (allowed.Count == 0) return Array.Empty<IndependentRegulatorySummary>();
        var rows = await store.ListAsync(allowed, limit, ct);
        var stillAllowed = await Allowed(actorId, ct);
        return rows.Where(x => stillAllowed.Contains(x.Id)).ToArray();
    }
    public async Task<IndependentRegulatoryReport?> GetAsync(Guid actorId, Guid id, CancellationToken ct)
    {
        if (actorId == Guid.Empty || id == Guid.Empty ||
            !(await Allowed(actorId, ct)).Contains(id))
            throw new UnauthorizedAccessException();
        var report = await store.GetAsync(id, ct);
        var provenance=ownership==null?null:await ownership.ProvenanceAsync(id,ct);
        if (!(await Allowed(actorId, ct)).Contains(id)) throw new UnauthorizedAccessException();
        return report==null?null:report with{Provenance=provenance};
    }
}
