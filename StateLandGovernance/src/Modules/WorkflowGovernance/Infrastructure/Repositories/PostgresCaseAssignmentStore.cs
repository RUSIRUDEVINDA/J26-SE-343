namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Repositories;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;

public sealed class PostgresCaseAssignmentStore(WorkflowGovernanceDbContext db) : ICaseAssignmentStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private IntakeJurisdictionPolicy[]? policyCache;
    public async Task<CaseAssignmentState?> ReadAsync(Guid caseId, CancellationToken ct = default)
    {
        var row = await db.CaseAssignments.AsNoTracking().SingleOrDefaultAsync(x => x.CaseId == caseId, ct);
        return row == null ? null : JsonSerializer.Deserialize<CaseAssignmentState>(row.StateJson, Json)!;
    }
    public static string? DeclaredJurisdiction(string? proposalIntakeJson)
    {
        if (string.IsNullOrWhiteSpace(proposalIntakeJson)) return null;
        using var json = JsonDocument.Parse(proposalIntakeJson);
        return json.RootElement.TryGetProperty("jurisdiction", out var source) &&
            source.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String ? code.GetString() : null;
    }
    private async Task<CaseAssignmentState> InitialAsync(LeaseCaseEntity item, CancellationToken ct)
    {
        var jurisdiction = DeclaredJurisdiction(item.ProposalIntakeJson);
        policyCache ??= (await db.IntakeJurisdictionPolicies.AsNoTracking().ToListAsync(ct))
            .Select(x => JsonSerializer.Deserialize<IntakeJurisdictionPolicy>(x.PolicyJson, Json)!).ToArray();
        var policies = policyCache.Where(x => !string.IsNullOrWhiteSpace(jurisdiction) && x.JurisdictionCode == jurisdiction &&
                x.EffectiveFrom <= item.CreatedAt && (x.EffectiveUntil == null || x.EffectiveUntil > item.CreatedAt)).ToArray();
        var match = CaseAssignmentService.ResolveJurisdiction(policyCache, jurisdiction, item.CreatedAt);
        return new(item.Id, 0, jurisdiction, match?.OfficeCode, match?.Id, match?.Version,
            match != null ? "RoutedByVersionedJurisdiction" : policies.Length > 1 ? "AmbiguousJurisdiction" : "UnassignedJurisdiction", null, []);
    }
    public async Task<CaseAssignmentState> EnsureAsync(Guid caseId, CancellationToken ct = default)
    {
        if (await ReadAsync(caseId, ct) is { } existing) return existing;
        var item = await db.LeaseCases.AsNoTracking().SingleOrDefaultAsync(x => x.Id == caseId, ct)
            ?? throw new ArgumentException("Case not found.");
        var state = await InitialAsync(item, ct);
        var row = new CaseAssignmentEntity { CaseId = caseId, Revision = 0, StateJson = JsonSerializer.Serialize(state, Json) };
        db.CaseAssignments.Add(row);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { db.Entry(row).State = EntityState.Detached; return await ReadAsync(caseId, ct) ?? throw new InvalidOperationException("Intake routing could not be persisted."); }
        return state;
    }
    public async Task<IReadOnlyList<IntakeCaseSummary>> ListAsync(Guid? applicant, Guid? reviewer, string? coordinatorOffice,
        bool includeUnassigned, CancellationToken ct = default)
    {
        var query = db.LeaseCases.AsNoTracking().AsQueryable();
        if (applicant != null) query = query.Where(x => x.CreatedByActorId == applicant);
        var cases = await query.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        var states = (await db.CaseAssignments.AsNoTracking().ToListAsync(ct)).ToDictionary(x => x.CaseId,
            x => JsonSerializer.Deserialize<CaseAssignmentState>(x.StateJson, Json)!);
        var result = new List<IntakeCaseSummary>();
        foreach (var item in cases)
        {
            var state = states.GetValueOrDefault(item.Id);
            if (reviewer != null && (state?.Active?.ActorId != reviewer || !CaseAssignmentService.IsActive(state.Active, DateTimeOffset.UtcNow))) continue;
            state ??= await InitialAsync(item, ct);
            if (coordinatorOffice != null && state.OfficeCode != coordinatorOffice && !(includeUnassigned && state.OfficeCode == null)) continue;
            var docs = await db.GovernedDocuments.AsNoTracking().Where(x => x.LeaseCaseId == item.Id).Select(x => x.Id).ToArrayAsync(ct);
            result.Add(new(item.Id, item.ApplicationReference, item.CreatedByActorId, item.CreatedAt, item.Status, docs, state));
        }
        return result;
    }
    public Task<bool> DocumentBelongsAsync(Guid caseId, Guid documentId, CancellationToken ct = default) =>
        db.GovernedDocuments.AnyAsync(x => x.Id == documentId && x.LeaseCaseId == caseId, ct);
    public async Task<CaseAssignmentState> SaveAsync(CaseAssignmentState state, int expectedRevision, CancellationToken ct = default)
    {
        var row = await db.CaseAssignments.SingleAsync(x => x.CaseId == state.CaseId, ct);
        if (row.Revision != expectedRevision) throw new InvalidOperationException("Assignment changed. Refresh before assigning.");
        row.Revision = state.Revision; row.StateJson = JsonSerializer.Serialize(state, Json);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new InvalidOperationException("Assignment changed concurrently. Refresh before assigning."); }
        return state;
    }
    // Trusted one-time repair of this adapter's original flat-property projection defect.
    // No facts/documents/reviews change. Active office/grant/history remain unchanged.
    public async Task<CaseAssignmentState> RepairDeclaredJurisdictionAsync(Guid caseId, CancellationToken ct = default)
    {
        var state = await ReadAsync(caseId, ct) ?? throw new ArgumentException("Managed intake required.");
        if (state.JurisdictionCode != null) return state;
        var item = await db.LeaseCases.AsNoTracking().SingleAsync(x => x.Id == caseId, ct);
        var corrected = await InitialAsync(item, ct);
        if (corrected.JurisdictionCode == null) return state;
        var next = state.OfficeCode == null && state.Active == null && state.History.Count == 0
            ? corrected with { Revision = state.Revision + 1 }
            : state with { Revision = state.Revision + 1, JurisdictionCode = corrected.JurisdictionCode,
                RoutingState = state.RoutingPolicyId == null ? "CoordinatorAssignedUnresolvedJurisdiction" : state.RoutingState };
        return await SaveAsync(next, state.Revision, ct);
    }
    public async Task<IReadOnlyList<IntakeJurisdictionPolicy>> PoliciesAsync(CancellationToken ct = default) =>
        (await db.IntakeJurisdictionPolicies.AsNoTracking().ToListAsync(ct)).Select(x => JsonSerializer.Deserialize<IntakeJurisdictionPolicy>(x.PolicyJson, Json)!).ToArray();
    public async Task PublishPolicyAsync(IntakeJurisdictionPolicy policy, CancellationToken ct = default)
    {
        if (policy.Id == Guid.Empty || policy.PublishedBy == Guid.Empty || string.IsNullOrWhiteSpace(policy.Identifier) ||
            string.IsNullOrWhiteSpace(policy.Version) || string.IsNullOrWhiteSpace(policy.JurisdictionCode) ||
            string.IsNullOrWhiteSpace(policy.OfficeCode) || string.IsNullOrWhiteSpace(policy.SourceReference) ||
            policy.EffectiveUntil <= policy.EffectiveFrom) throw new ArgumentException("A complete sourced jurisdiction policy is required.");
        db.IntakeJurisdictionPolicies.Add(new() { Id = policy.Id, Identifier = policy.Identifier,
            Version = policy.Version, PolicyJson = JsonSerializer.Serialize(policy, Json) });
        await db.SaveChangesAsync(ct);
    }
}
