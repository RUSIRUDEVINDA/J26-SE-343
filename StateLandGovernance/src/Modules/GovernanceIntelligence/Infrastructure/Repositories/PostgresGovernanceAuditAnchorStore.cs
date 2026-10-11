using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;

/// <summary>
/// PostgreSQL implementation of IGovernanceAuditAnchorStore using Entity Framework Core.
/// Encapsulated inside Infrastructure layer.
/// </summary>
public sealed class PostgresGovernanceAuditAnchorStore : IGovernanceAuditAnchorStore
{
    private readonly GovernanceIntelligenceDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public PostgresGovernanceAuditAnchorStore(
        GovernanceIntelligenceDbContext dbContext,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task SaveReceiptAsync(
        BlockchainAnchorReceipt receipt,
        AuditVerificationStatus? verificationStatus = null,
        string? failureReason = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var existing = await _dbContext.GovernanceAuditAnchorReceipts
            .FirstOrDefaultAsync(x => x.AuditRecordId == receipt.AuditRecordId, cancellationToken);

        if (existing is null)
        {
            var entity = new GovernanceAuditAnchorReceiptEntity
            {
                Id = Guid.NewGuid(),
                AuditRecordId = receipt.AuditRecordId,
                RecordHash = receipt.RecordHash,
                AnchorStatus = (int)receipt.AnchorStatus,
                TransactionReference = receipt.TransactionReference,
                BlockNumber = receipt.BlockNumber,
                AnchoredAtUtc = receipt.AnchoredAtUtc,
                VerificationStatus = (int)(verificationStatus ?? (receipt.AnchorStatus == AnchorStatus.Anchored ? AuditVerificationStatus.Pending : AuditVerificationStatus.NotAnchored)),
                FailureReason = failureReason,
                RetryCount = 0,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            await _dbContext.GovernanceAuditAnchorReceipts.AddAsync(entity, cancellationToken);
        }
        else
        {
            existing.RecordHash = receipt.RecordHash;
            existing.AnchorStatus = (int)receipt.AnchorStatus;
            existing.TransactionReference = receipt.TransactionReference ?? existing.TransactionReference;
            existing.BlockNumber = receipt.BlockNumber ?? existing.BlockNumber;
            existing.AnchoredAtUtc = receipt.AnchoredAtUtc ?? existing.AnchoredAtUtc;
            if (verificationStatus.HasValue)
            {
                existing.VerificationStatus = (int)verificationStatus.Value;
                existing.LastVerifiedAtUtc = now;
            }
            if (failureReason != null)
            {
                existing.FailureReason = failureReason;
            }
            existing.UpdatedAtUtc = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<BlockchainAnchorReceipt?> GetReceiptByAuditRecordIdAsync(
        Guid auditRecordId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.GovernanceAuditAnchorReceipts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AuditRecordId == auditRecordId, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        return new BlockchainAnchorReceipt(
            AuditRecordId: entity.AuditRecordId,
            RecordHash: entity.RecordHash,
            AnchorStatus: (AnchorStatus)entity.AnchorStatus,
            TransactionReference: entity.TransactionReference,
            BlockNumber: entity.BlockNumber,
            AnchoredAtUtc: entity.AnchoredAtUtc
        );
    }

    public async Task EnqueueOutboxAsync(
        Guid auditRecordId,
        string recordHash,
        EngineType engineType,
        string recordVersion = "AUDIT-V1",
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Idempotency check: if outbox entry already exists in pending/completed state, do not duplicate
        var existing = await _dbContext.GovernanceAuditAnchorOutbox
            .FirstOrDefaultAsync(x => x.AuditRecordId == auditRecordId && x.Status != 3, cancellationToken);

        if (existing != null)
        {
            return;
        }

        var outboxEntity = new GovernanceAuditAnchorOutboxEntity
        {
            Id = Guid.NewGuid(),
            AuditRecordId = auditRecordId,
            RecordHash = recordHash,
            EngineType = (int)engineType,
            RecordVersion = recordVersion,
            Status = 0, // Pending
            RetryCount = 0,
            NextAttemptAtUtc = now,
            CreatedAtUtc = now
        };

        await _dbContext.GovernanceAuditAnchorOutbox.AddAsync(outboxEntity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AnchorOutboxItem>> GetPendingOutboxBatchAsync(
        int maxBatchSize,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var entities = await _dbContext.GovernanceAuditAnchorOutbox
            .Where(x => (x.Status == 0 || x.Status == 1) && x.NextAttemptAtUtc <= now)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(maxBatchSize)
            .ToListAsync(cancellationToken);

        return entities.Select(e => new AnchorOutboxItem(
            Id: e.Id,
            AuditRecordId: e.AuditRecordId,
            RecordHash: e.RecordHash,
            EngineType: (EngineType)e.EngineType,
            RecordVersion: e.RecordVersion,
            RetryCount: e.RetryCount,
            NextAttemptAtUtc: e.NextAttemptAtUtc
        )).ToList();
    }

    public async Task MarkOutboxCompletedAsync(
        Guid outboxId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.GovernanceAuditAnchorOutbox
            .FirstOrDefaultAsync(x => x.Id == outboxId, cancellationToken);

        if (entity != null)
        {
            entity.Status = 2; // Completed
            entity.ProcessedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RecordOutboxFailureAsync(
        Guid outboxId,
        string errorMessage,
        int retryDelaySeconds,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.GovernanceAuditAnchorOutbox
            .FirstOrDefaultAsync(x => x.Id == outboxId, cancellationToken);

        if (entity != null)
        {
            entity.RetryCount++;
            entity.LastErrorMessage = errorMessage.Length > 1000 ? errorMessage[..1000] : errorMessage;
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            if (entity.RetryCount >= 10)
            {
                entity.Status = 3; // DeadLetter
            }
            else
            {
                entity.Status = 0; // Return to pending
                entity.NextAttemptAtUtc = now.AddSeconds(retryDelaySeconds);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
