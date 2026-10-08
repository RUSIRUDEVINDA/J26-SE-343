using System;
using System.Collections.Generic;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;

namespace StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

/// <summary>
/// Value object representing the overall evaluation result of conditional governance verification.
/// </summary>
public sealed record ConditionalVerificationResult
{
    public string VerificationId { get; }
    public string SubjectId { get; }
    public ConditionalVerificationOutcome Outcome { get; }
    public int TotalConditionsCount { get; }
    public int MandatoryConditionsCount { get; }
    public int SatisfiedMandatoryCount { get; }
    public int UnsatisfiedMandatoryCount { get; }
    public int SatisfiedOptionalCount { get; }
    public int PendingConditionsCount { get; }
    public int MissingConditionsCount { get; }
    public int ExpiredConditionsCount { get; }
    public IReadOnlyList<ConditionVerificationStatus> ConditionStatuses { get; }
    public string SummaryExplanation { get; }
    public string RecommendedAction { get; }
    public DateTime EvaluationTimestamp { get; }

    public ConditionalVerificationResult(
        string verificationId,
        string subjectId,
        ConditionalVerificationOutcome outcome,
        int totalConditionsCount,
        int mandatoryConditionsCount,
        int satisfiedMandatoryCount,
        int unsatisfiedMandatoryCount,
        int satisfiedOptionalCount,
        int pendingConditionsCount,
        int missingConditionsCount,
        int expiredConditionsCount,
        IEnumerable<ConditionVerificationStatus> conditionStatuses,
        string summaryExplanation,
        string recommendedAction,
        DateTime evaluationTimestamp)
    {
        if (string.IsNullOrWhiteSpace(verificationId))
        {
            throw new ArgumentException("VerificationId cannot be null or empty.", nameof(verificationId));
        }

        if (string.IsNullOrWhiteSpace(subjectId))
        {
            throw new ArgumentException("SubjectId cannot be null or empty.", nameof(subjectId));
        }

        VerificationId = verificationId.Trim();
        SubjectId = subjectId.Trim();
        Outcome = outcome;
        TotalConditionsCount = totalConditionsCount;
        MandatoryConditionsCount = mandatoryConditionsCount;
        SatisfiedMandatoryCount = satisfiedMandatoryCount;
        UnsatisfiedMandatoryCount = unsatisfiedMandatoryCount;
        SatisfiedOptionalCount = satisfiedOptionalCount;
        PendingConditionsCount = pendingConditionsCount;
        MissingConditionsCount = missingConditionsCount;
        ExpiredConditionsCount = expiredConditionsCount;
        ConditionStatuses = new List<ConditionVerificationStatus>(conditionStatuses ?? Array.Empty<ConditionVerificationStatus>()).AsReadOnly();
        SummaryExplanation = summaryExplanation?.Trim() ?? string.Empty;
        RecommendedAction = recommendedAction?.Trim() ?? string.Empty;
        EvaluationTimestamp = evaluationTimestamp;
    }
}
