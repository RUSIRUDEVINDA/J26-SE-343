using System;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

/// <summary>
/// Infrastructure persistence entity representing a blockchain anchor confirmation receipt in PostgreSQL.
/// Keeps blockchain transport details and ledger verification states isolated from the core GovernanceAuditRecord.
/// </summary>
public sealed class GovernanceAuditAnchorReceiptEntity
{
    public Guid Id { get; set; }
    public Guid AuditRecordId { get; set; }
    public string RecordHash { get; set; } = string.Empty;
    public int AnchorStatus { get; set; }
    public string? TransactionReference { get; set; }
    public long? BlockNumber { get; set; }
    public string? ContractAddress { get; set; }
    public DateTime? AnchoredAtUtc { get; set; }
    public DateTime? LastVerifiedAtUtc { get; set; }
    public int VerificationStatus { get; set; }
    public string? FailureReason { get; set; }
    public int RetryCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
