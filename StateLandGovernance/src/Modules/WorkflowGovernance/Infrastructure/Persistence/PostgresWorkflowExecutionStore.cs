namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Mappings;

public sealed class PostgresWorkflowExecutionStore(WorkflowGovernanceDbContext db) : IWorkflowExecutionStore
{
    public async Task<WorkflowExecution?> GetAsync(Guid caseId, CancellationToken ct)
    {
        var rows = await db.WorkflowExecutions.Where(e => e.LeaseCaseId == caseId).ToListAsync(ct);
        if (rows.Count > 1) throw new InvalidOperationException("Multiple active execution records require resolution.");
        if (rows.Count == 0) return null;
        using var json = JsonDocument.Parse(rows[0].RuleSetReferenceJson);
        if (!json.RootElement.TryGetProperty("execution", out var state))
            throw new InvalidOperationException("Legacy execution lacks full committed definition/history; explicit migration required.");
        var value = PersistenceJsonSerializer.Deserialize<WorkflowExecution>(state.GetRawText())!;
        if (value.Id.Value != rows[0].Id || value.LeaseCaseId.Value != caseId || value.Revision != rows[0].Revision || value.WorkflowPlanId.Value != rows[0].WorkflowPlanId)
            throw new InvalidOperationException("Execution row and committed-state bindings disagree.");
        return value;
    }
    public async Task SaveAsync(WorkflowExecution execution, CancellationToken ct)
    {
        var row = await db.WorkflowExecutions.SingleOrDefaultAsync(e => e.Id == execution.Id.Value, ct);
        if (row == null)
        {
            row = new WorkflowExecutionEntity { Id = execution.Id.Value, LeaseCaseId = execution.LeaseCaseId.Value };
            db.WorkflowExecutions.Add(row);
        }
        row.WorkflowPlanId = execution.WorkflowPlanId.Value; row.WorkflowPlanRevision = execution.WorkflowPlanRevision;
        row.Revision = execution.Revision; row.StartedAt = execution.StartedAt; row.Status = execution.Status.ToString();
        row.StagesJson = PersistenceJsonSerializer.Serialize(execution.Stages);
        row.RuleSetReferenceJson = PersistenceJsonSerializer.Serialize(new { identifier = execution.RuleSetReference.Identifier,
            version = execution.RuleSetReference.Version, execution });
        row.UpdatedAt = DateTime.UtcNow;
        if (execution.Status == WorkflowExecutionStatus.Completed) row.CompletedWorkflowJson = PersistenceJsonSerializer.Serialize(execution.CreateCompletionRecord());
        // Save via the shared C3 unit of work; row revision remains an EF concurrency token.
    }
    public async Task<IReadOnlyList<Guid>> RejectedCasesAsync(CancellationToken ct)
    {
        var rows = await db.WorkflowExecutions.AsNoTracking().ToListAsync(ct);
        return rows.Where(r => {
            using var doc = JsonDocument.Parse(r.RuleSetReferenceJson);
            return doc.RootElement.TryGetProperty("execution", out var e) &&
                PersistenceJsonSerializer.Deserialize<WorkflowExecution>(e.GetRawText())!.Stages.Any(s => s.Decision?.Outcome == WorkflowStageDecisionOutcome.Rejected);
        }).Select(r => r.LeaseCaseId).ToArray();
    }
}
