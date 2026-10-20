namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Mappings;

public sealed class PostgresCorrectionRepository(WorkflowGovernanceDbContext db) : ICorrectionRepository
{
    public async Task<CorrectionRecord?> GetAsync(Guid caseId, Guid requestId, CancellationToken cancellationToken = default)
    {
        var row = await db.Set<CorrectionRecordEntity>().SingleOrDefaultAsync(x => x.Id == requestId && x.LeaseCaseId == caseId, cancellationToken);
        return row is null ? null : PersistenceJsonSerializer.Deserialize<CorrectionRecord>(row.RecordJson);
    }
    public async Task<IReadOnlyList<CorrectionRecord>> ListAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var rows = await db.Set<CorrectionRecordEntity>().AsNoTracking().Where(x => x.LeaseCaseId == caseId).ToListAsync(cancellationToken);
        return rows.Select(x => PersistenceJsonSerializer.Deserialize<CorrectionRecord>(x.RecordJson)!).ToList();
    }
    public async Task SaveAsync(CorrectionRecord record, CancellationToken cancellationToken = default)
    {
        var row = await db.Set<CorrectionRecordEntity>().FindAsync(new object[] { record.Id }, cancellationToken);
        if (row is null)
        {
            row = new CorrectionRecordEntity { Id = record.Id, LeaseCaseId = record.LeaseCaseId };
            db.Add(row);
        }
        row.Revision = record.Revision;
        row.RecordJson = PersistenceJsonSerializer.Serialize(record);
    }
}
