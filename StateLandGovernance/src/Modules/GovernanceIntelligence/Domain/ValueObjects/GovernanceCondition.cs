using System;

namespace StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

/// <summary>
/// Value object representing a prerequisite governance condition definition.
/// </summary>
public sealed record GovernanceCondition
{
    public string ConditionId { get; }
    public string Title { get; }
    public string Category { get; }
    public bool IsMandatory { get; }
    public int? MaxEvidenceAgeDays { get; }
    public string Description { get; }

    public GovernanceCondition(
        string conditionId,
        string title = "",
        string category = "",
        bool isMandatory = true,
        int? maxEvidenceAgeDays = null,
        string description = "")
    {
        if (string.IsNullOrWhiteSpace(conditionId))
        {
            throw new ArgumentException("ConditionId cannot be null or empty.", nameof(conditionId));
        }

        if (maxEvidenceAgeDays.HasValue && maxEvidenceAgeDays.Value <= 0)
        {
            throw new ArgumentException("MaxEvidenceAgeDays must be a positive integer if specified.", nameof(maxEvidenceAgeDays));
        }

        ConditionId = conditionId.Trim();
        Title = title?.Trim() ?? string.Empty;
        Category = category?.Trim() ?? string.Empty;
        IsMandatory = isMandatory;
        MaxEvidenceAgeDays = maxEvidenceAgeDays;
        Description = description?.Trim() ?? string.Empty;
    }
}
