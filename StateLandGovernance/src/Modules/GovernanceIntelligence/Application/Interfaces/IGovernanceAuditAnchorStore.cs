using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;

namespace StateLandGovernance.GovernanceIntelligence.Application.Interfaces;

/// <summary>
/// Application abstraction for persisting blockchain anchor receipts and outbox records in PostgreSQL.
/// Keeps off-chain audit records cleanly decoupled from asynchronous blockchain transport details.
/// </summary>
public interface IGovernanceAuditAnchorStore
{
    Task SaveReceiptAsync(
        BlockchainAnchorReceipt receipt,
        AuditVerificationStatus? verificationStatus = null,
        string? failureReason = null,
        CancellationToken cancellationToken = default);

    Task<BlockchainAnchorReceipt?> GetReceiptByAuditRecordIdAsync(
        Guid auditRecordId,
        CancellationToken cancellationToken = default);

    Task EnqueueOutboxAsync(
        Guid auditRecordId,
        string recordHash,
        EngineType engineType,
        string recordVersion = "AUDIT-V1",
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AnchorOutboxItem>> GetPendingOutboxBatchAsync(
        int maxBatchSize,
        CancellationToken cancellationToken = default);

    Task MarkOutboxCompletedAsync(
        Guid outboxId,
        CancellationToken cancellationToken = default);

    Task RecordOutboxFailureAsync(
        Guid outboxId,
        string errorMessage,
        int retryDelaySeconds,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Model representing an outbox item queued for asynchronous blockchain anchoring.
/// </summary>
public sealed record AnchorOutboxItem(
    Guid Id,
    Guid AuditRecordId,
    string RecordHash,
    EngineType EngineType,
    string RecordVersion,
    int RetryCount,
    DateTime NextAttemptAtUtc
);
