using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Mappings;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;

/// <summary>
/// PostgreSQL implementation of the IGovernanceAuditRepository interface using Entity Framework Core.
/// Encapsulated inside Infrastructure layer.
/// </summary>
public sealed class PostgresGovernanceAuditRepository : IGovernanceAuditRepository
{
    private readonly GovernanceIntelligenceDbContext _dbContext;

    public PostgresGovernanceAuditRepository(GovernanceIntelligenceDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<GovernanceAuditRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.GovernanceAuditRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity is null ? null : GovernanceAuditRecordMapper.ToDomain(entity);
    }

    public async Task AddAsync(GovernanceAuditRecord record, CancellationToken cancellationToken = default)
    {
        if (record is null)
        {
            throw new ArgumentNullException(nameof(record));
        }

        var entity = GovernanceAuditRecordMapper.ToEntity(record);

        await _dbContext.GovernanceAuditRecords.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
