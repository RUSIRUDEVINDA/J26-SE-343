using System;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;

namespace StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

/// <summary>
/// Value object representing submitted proof status for a prerequisite governance condition.
/// </summary>
public sealed record VerificationEvidence
{
    public string ConditionId { get; }
    public string EvidenceId { get; }
    public SuppliedEvidenceStatus ProvidedStatus { get; }
    public DateTime EvidenceTimestamp { get; }
    public DateTime? ExpiryTimestamp { get; }
    public string IssuerOrAuthority { get; }
    public string Remarks { get; }

    public VerificationEvidence(
        string conditionId,
        string evidenceId = "",
        SuppliedEvidenceStatus providedStatus = SuppliedEvidenceStatus.Satisfied,
        DateTime? evidenceTimestamp = null,
        DateTime? expiryTimestamp = null,
        string issuerOrAuthority = "",
        string remarks = "")
    {
        if (string.IsNullOrWhiteSpace(conditionId))
        {
            throw new ArgumentException("ConditionId cannot be null or empty.", nameof(conditionId));
        }

        ConditionId = conditionId.Trim();
        EvidenceId = evidenceId?.Trim() ?? string.Empty;
        ProvidedStatus = providedStatus;
        EvidenceTimestamp = evidenceTimestamp ?? DateTime.UtcNow;
        ExpiryTimestamp = expiryTimestamp;
        IssuerOrAuthority = issuerOrAuthority?.Trim() ?? string.Empty;
        Remarks = remarks?.Trim() ?? string.Empty;
    }
}
