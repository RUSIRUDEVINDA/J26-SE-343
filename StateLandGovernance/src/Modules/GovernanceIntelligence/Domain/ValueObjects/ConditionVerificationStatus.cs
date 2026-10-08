using System;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;

namespace StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

/// <summary>
/// Value object representing the derived evaluation status for a specific governance condition.
/// </summary>
public sealed record ConditionVerificationStatus
{
    public string ConditionId { get; }
    public bool IsMandatory { get; }
    public DerivedConditionStatus Status { get; }
    public bool IsSatisfied => Status == DerivedConditionStatus.Satisfied;
    public string FailureReason { get; }
    public string Notes { get; }

    public ConditionVerificationStatus(
        string conditionId,
        bool isMandatory,
        DerivedConditionStatus status,
        string failureReason = "",
        string notes = "")
    {
        if (string.IsNullOrWhiteSpace(conditionId))
        {
            throw new ArgumentException("ConditionId cannot be null or empty.", nameof(conditionId));
        }

        ConditionId = conditionId.Trim();
        IsMandatory = isMandatory;
        Status = status;
        FailureReason = failureReason?.Trim() ?? string.Empty;
        Notes = notes?.Trim() ?? string.Empty;
    }
}
