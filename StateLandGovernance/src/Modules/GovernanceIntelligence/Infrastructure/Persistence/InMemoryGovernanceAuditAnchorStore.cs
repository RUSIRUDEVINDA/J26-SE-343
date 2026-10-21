using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;

/// <summary>
/// In-memory implementation of IGovernanceAuditAnchorStore for development and test execution.
/// </summary>
public sealed class InMemoryGovernanceAuditAnchorStore : IGovernanceAuditAnchorStore
{
    private readonly ConcurrentDictionary<Guid, BlockchainAnchorReceipt> _receipts = new();
    private readonly ConcurrentDictionary<Guid, AnchorOutboxItem> _outbox = new();
    private readonly TimeProvider _timeProvider;

    public InMemoryGovernanceAuditAnchorStore(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public Task SaveReceiptAsync(
        BlockchainAnchorReceipt receipt,
        AuditVerificationStatus? verificationStatus = null,
        string? failureReason = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        _receipts[receipt.AuditRecordId] = receipt;
        return Task.CompletedTask;
    }

    public Task<BlockchainAnchorReceipt?> GetReceiptByAuditRecordIdAsync(
        Guid auditRecordId,
        CancellationToken cancellationToken = default)
    {
        _receipts.TryGetValue(auditRecordId, out var receipt);
        return Task.FromResult(receipt);
    }

    public Task EnqueueOutboxAsync(
        Guid auditRecordId,
        string recordHash,
        EngineType engineType,
        string recordVersion = "AUDIT-V1",
        CancellationToken cancellationToken = default)
    {
        // Check if already enqueued
        if (_outbox.Values.Any(x => x.AuditRecordId == auditRecordId))
        {
            return Task.CompletedTask;
        }

        var item = new AnchorOutboxItem(
            Id: Guid.NewGuid(),
            AuditRecordId: auditRecordId,
            RecordHash: recordHash,
            EngineType: engineType,
            RecordVersion: recordVersion,
            RetryCount: 0,
            NextAttemptAtUtc: _timeProvider.GetUtcNow().UtcDateTime
        );

        _outbox[item.Id] = item;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AnchorOutboxItem>> GetPendingOutboxBatchAsync(
        int maxBatchSize,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        IReadOnlyList<AnchorOutboxItem> batch = _outbox.Values
            .Where(x => x.NextAttemptAtUtc <= now)
            .OrderBy(x => x.NextAttemptAtUtc)
            .Take(maxBatchSize)
            .ToList();

        return Task.FromResult(batch);
    }

    public Task MarkOutboxCompletedAsync(
        Guid outboxId,
        CancellationToken cancellationToken = default)
    {
        _outbox.TryRemove(outboxId, out _);
        return Task.CompletedTask;
    }

    public Task RecordOutboxFailureAsync(
        Guid outboxId,
        string errorMessage,
        int retryDelaySeconds,
        CancellationToken cancellationToken = default)
    {
        if (_outbox.TryGetValue(outboxId, out var item))
        {
            var updated = item with
            {
                RetryCount = item.RetryCount + 1,
                NextAttemptAtUtc = _timeProvider.GetUtcNow().UtcDateTime.AddSeconds(retryDelaySeconds)
            };
            _outbox[outboxId] = updated;
        }
        return Task.CompletedTask;
    }
}
