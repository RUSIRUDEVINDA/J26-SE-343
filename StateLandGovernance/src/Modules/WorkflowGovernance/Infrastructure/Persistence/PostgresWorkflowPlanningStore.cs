namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;

public sealed class PostgresWorkflowPlanningStore(WorkflowGovernanceDbContext db) : IWorkflowPlanningStore
{
    private static readonly JsonSerializerOptions Json = CaseScreeningService.JsonOptions;
    public async Task<IReadOnlyList<RoutingPolicyDto>> GetPoliciesAsync(CancellationToken ct)
    {
        var rows = await db.InstitutionalRoutingConfigs.AsNoTracking().Where(r => r.PolicyJson != null).ToListAsync(ct);
        var policies = new List<RoutingPolicyDto>();
        foreach (var row in rows)
        {
            var p = JsonSerializer.Deserialize<RoutingPolicyDto>(row.PolicyJson!, Json);
            if (p is null || p.Identifier != row.RuleSetName || p.Version != row.RuleSetVersion)
                throw new InvalidOperationException("Routing configuration identity does not match its persisted version.");
            policies.Add(p with { StandardDays = row.StandardStageDurationDays, DivisionalDays = row.DivisionalSecretariatDurationDays,
                CommissionerDays = row.CommissionerDurationDays, StandardCapability = row.StandardCapability,
                DivisionalCapability = row.DivisionalSecretaryCapability, CommissionerCapability = row.CommissionerCapability });
        }
        return policies;
    }
    public async Task<IReadOnlyList<WorkflowPlanDto>> GetPlansAsync(Guid caseId, CancellationToken ct)
    {
        var rows = await db.WorkflowPlans.AsNoTracking().Where(p => p.LeaseCaseId == caseId).OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
        return rows.Select(r => JsonDocument.Parse(r.RuleSetReferenceJson)).Select(d => {
            using (d) return d.RootElement.TryGetProperty("planning", out var p) ? p.Deserialize<WorkflowPlanDto>(Json) : null;
        }).Where(p => p != null).Cast<WorkflowPlanDto>().ToList();
    }
    public Task<bool> HasExecutionAsync(Guid caseId, CancellationToken ct) => db.WorkflowExecutions.AnyAsync(e => e.LeaseCaseId == caseId, ct);
    public async Task SaveAsync(Guid caseId, WorkflowPlanDto plan, CancellationToken ct)
    {
        var row = await db.WorkflowPlans.SingleOrDefaultAsync(p => p.Id == plan.Id, ct);
        if (row is null)
        {
            var completeness = await db.DocumentCompletenessAssessments.Where(a => a.LeaseCaseId == caseId).OrderByDescending(a => a.AssessedAt).FirstAsync(ct);
            row = new WorkflowPlanEntity { Id = plan.Id, LeaseCaseId = caseId, Source = "RuleBased", DocumentCompletenessAssessmentId = completeness.Id };
            db.WorkflowPlans.Add(row);
        }
        row.Status = plan.Status; row.Revision = plan.Revision; row.VerifiedFactSnapshotId = plan.SnapshotId;
        row.RuleSetReferenceJson = JsonSerializer.Serialize(new { identifier = plan.RuleIdentifier, version = plan.RuleVersion, planning = plan }, Json);
        row.StagesJson = JsonSerializer.Serialize(plan.Stages, Json); row.CreatedAt = plan.CreatedAt;
        row.ApprovedAt = plan.ApprovedAt; row.SupersessionReason = plan.RevisionReason;
        row.SupersededAt = plan.SupersededAt; row.UpdatedAt = DateTime.UtcNow;
        // The aggregate and this record are committed together by the existing unit of work.
    }
    public async Task PublishPolicyAsync(RoutingPolicyDto policy, CancellationToken ct)
    {
        if (policy.ApprovedByActorId == Guid.Empty || string.IsNullOrWhiteSpace(policy.SourceReference)
            || policy.ApplicableFacts.Count == 0 || policy.Institutions.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Approved source, actor and explicit applicability are required.");
        if (await db.InstitutionalRoutingConfigs.AnyAsync(r => r.RuleSetName == policy.Identifier && r.RuleSetVersion == policy.Version, ct))
            throw new InvalidOperationException("Routing versions are append-only. Publish a new version.");
        db.InstitutionalRoutingConfigs.Add(new() { Id = Guid.NewGuid(), RuleSetName = policy.Identifier, RuleSetVersion = policy.Version,
            PolicyJson = JsonSerializer.Serialize(policy, Json), StandardStageDurationDays = policy.StandardDays,
            DivisionalSecretariatDurationDays = policy.DivisionalDays, CommissionerDurationDays = policy.CommissionerDays,
            StandardCapability = policy.StandardCapability, DivisionalSecretaryCapability = policy.DivisionalCapability,
            CommissionerCapability = policy.CommissionerCapability, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
    }
}
