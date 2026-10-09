using System;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;

namespace StateLandGovernance.GovernanceIntelligence.Application.DTOs;

/// <summary>
/// Neutral application request to anchor a governance audit digest onto the blockchain trust ledger.
/// Contains only minimal metadata and the precomputed SHA-256 digest; never transmits PII.
/// </summary>
public sealed record AnchorAuditRequest(
    Guid AuditRecordId,
    string RecordHash,
    string EngineType,
    string RecordVersion = "AUDIT-V1"
);

/// <summary>
/// Neutral application model representing a tamper-evident blockchain anchor confirmation receipt.
/// </summary>
public sealed record BlockchainAnchorReceipt(
    Guid AuditRecordId,
    string RecordHash,
    AnchorStatus AnchorStatus,
    string? TransactionReference,
    long? BlockNumber,
    DateTime? AnchoredAtUtc
);

/// <summary>
/// Neutral application model representing an audit record integrity verification outcome.
/// </summary>
public sealed record AuditIntegrityVerificationResult(
    Guid AuditRecordId,
    string CurrentHash,
    string? AnchoredHash,
    AuditVerificationStatus VerificationStatus,
    string? TransactionReference,
    DateTime? AnchoredAtUtc,
    DateTime VerifiedAtUtc,
    string Explanation
);

/// <summary>
/// API-level DTO representing complete verification facts for presentation in the governance capability UI.
/// </summary>
public sealed record GovernanceAuditVerificationReceiptDto(
    Guid RecordId,
    string EngineType,
    string ActionName,
    string GovernanceStatus,
    DateTime DecisionRecordedAt,
    string RecordHash,
    string VerificationStatus,
    string AnchorStatus,
    string? LedgerTransactionReference,
    long? BlockNumber,
    DateTime? AnchoredAtUtc,
    DateTime? VerifiedAtUtc,
    string Explanation
);
