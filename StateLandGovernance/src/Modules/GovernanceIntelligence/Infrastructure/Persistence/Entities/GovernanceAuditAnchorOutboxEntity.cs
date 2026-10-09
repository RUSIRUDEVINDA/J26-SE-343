using System;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

/// <summary>
/// Infrastructure persistence entity representing an audit anchor dispatch outbox queue entry in PostgreSQL.
/// Supports reliable asynchronous dispatch and exponential backoff retry to the blockchain microservice.
/// </summary>
public sealed class GovernanceAuditAnchorOutboxEntity
{
    public Guid Id { get; set; }
    public Guid AuditRecordId { get; set; }
    public string RecordHash { get; set; } = string.Empty;
    public int EngineType { get; set; }
    public string RecordVersion { get; set; } = "AUDIT-V1";
    public int Status { get; set; } // 0 = Pending, 1 = Processing, 2 = Completed, 3 = DeadLetter
    public int RetryCount { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }
    public string? LastErrorMessage { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
}
