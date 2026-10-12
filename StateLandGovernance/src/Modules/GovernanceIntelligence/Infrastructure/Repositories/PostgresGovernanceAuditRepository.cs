using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Mappings;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;

/// <summary>
/// PostgreSQL implementation of the IGovernanceAuditRepository interface using Entity Framework Core.
/// Encapsulated inside Infrastructure layer.
/// </summary>
public sealed class PostgresGovernanceAuditRepository : IGovernanceAuditRepository, IGovernanceAuditCaseHistoryReader
{
    private readonly GovernanceIntelligenceDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public PostgresGovernanceAuditRepository(
        GovernanceIntelligenceDbContext dbContext,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<GovernanceAuditRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.GovernanceAuditRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity is null ? null : GovernanceAuditRecordMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<GovernanceAuditCaseHistoryDto>> GetByCaseIdAsync(
        string caseId, int limit, CancellationToken cancellationToken = default)
    {
        return await (
            from evaluation in _dbContext.ComplianceEvaluations.AsNoTracking()
            join audit in _dbContext.GovernanceAuditRecords.AsNoTracking() on evaluation.AuditRecordId equals audit.Id
            join receipt in _dbContext.GovernanceAuditAnchorReceipts.AsNoTracking() on audit.Id equals receipt.AuditRecordId into receiptGroup
            from receipt in receiptGroup.DefaultIfEmpty()
            where evaluation.CaseId == caseId
            orderby audit.Timestamp descending
            select new GovernanceAuditCaseHistoryDto(
                audit.Id, evaluation.CaseId!, audit.ActionName, audit.HashVersion, audit.Timestamp,
                receipt == null ? AnchorStatus.NotAnchored.ToString() : ((AnchorStatus)receipt.AnchorStatus).ToString(),
                receipt == null ? null : receipt.TransactionReference,
                receipt == null ? null : receipt.BlockNumber,
                receipt == null ? AuditVerificationStatus.NotAnchored.ToString() : ((AuditVerificationStatus)receipt.VerificationStatus).ToString(),
                receipt == null ? null : receipt.LastVerifiedAtUtc))
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(GovernanceAuditRecord record, CancellationToken cancellationToken = default)
    {
        if (record is null)
        {
            throw new ArgumentNullException(nameof(record));
        }

        var entity = GovernanceAuditRecordMapper.ToEntity(record);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var canonicalHash = GovernanceAuditHasher.ComputeCanonicalHash(record, record.HashVersion);

        var outboxEntity = new GovernanceAuditAnchorOutboxEntity
        {
            Id = Guid.NewGuid(),
            AuditRecordId = record.Id,
            RecordHash = canonicalHash,
            EngineType = (int)record.EngineType,
            RecordVersion = record.HashVersion,
            Status = 0, // Pending
            RetryCount = 0,
            NextAttemptAtUtc = now,
            CreatedAtUtc = now
        };

        await _dbContext.GovernanceAuditRecords.AddAsync(entity, cancellationToken);
        await _dbContext.GovernanceAuditAnchorOutbox.AddAsync(outboxEntity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
