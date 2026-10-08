using StateLandGovernance.GovernanceIntelligence.Application.DTOs;

namespace StateLandGovernance.GovernanceIntelligence.Application.Interfaces;

public enum EarlyGovernanceReferralMappingStatus
{
    NotRequired = 1,
    Unresolved = 2,
    ReferralRequired = 3
}

public sealed record EarlyGovernanceReferralPolicyDecision(
    EarlyGovernanceReferralMappingStatus Status,
    string DecisionCode,
    string Reason,
    IReadOnlyList<string> EvidenceReferences)
{
    public static EarlyGovernanceReferralPolicyDecision NotRequired(string decisionCode, string reason) =>
        new(EarlyGovernanceReferralMappingStatus.NotRequired, decisionCode, reason, Array.Empty<string>());

    public static EarlyGovernanceReferralPolicyDecision Unresolved(string decisionCode, string reason) =>
        new(EarlyGovernanceReferralMappingStatus.Unresolved, decisionCode, reason, Array.Empty<string>());

    public static EarlyGovernanceReferralPolicyDecision ReferralRequired(
        string decisionCode,
        string reason,
        IReadOnlyList<string> evidenceReferences) =>
        new(EarlyGovernanceReferralMappingStatus.ReferralRequired, decisionCode, reason, evidenceReferences);
}

public interface IEarlyGovernanceReferralPolicy
{
    EarlyGovernanceReferralPolicyDecision Evaluate(EarlyGovernanceScreeningResultDto screeningResult);
}

/// <summary>
/// Safe production default while the Commissioner-referral mapping remains unapproved.
/// Clear screening results are explicitly non-referral. Review and incomplete-evidence
/// outcomes remain unresolved and never create a referral intent.
/// </summary>
public sealed class UnconfiguredEarlyGovernanceReferralPolicy : IEarlyGovernanceReferralPolicy
{
    public EarlyGovernanceReferralPolicyDecision Evaluate(EarlyGovernanceScreeningResultDto screeningResult)
    {
        ArgumentNullException.ThrowIfNull(screeningResult);

        return screeningResult.OverallStatus switch
        {
            "Clear" => EarlyGovernanceReferralPolicyDecision.NotRequired(
                "EG_REFERRAL_NOT_REQUIRED_CLEAR",
                "The supplied screening evidence produced a Clear outcome."),
            "ReviewRequired" => EarlyGovernanceReferralPolicyDecision.Unresolved(
                "EG_REFERRAL_MAPPING_UNRESOLVED_REVIEW_REQUIRED",
                "Existing rules require officer review but do not authorize automatic Land Commissioner referral."),
            "InsufficientInformation" => EarlyGovernanceReferralPolicyDecision.Unresolved(
                "EG_REFERRAL_MAPPING_UNRESOLVED_INSUFFICIENT_INFORMATION",
                "Missing, unavailable, or unverified evidence does not authorize automatic Land Commissioner referral."),
            _ => EarlyGovernanceReferralPolicyDecision.Unresolved(
                "EG_REFERRAL_MAPPING_UNRESOLVED_UNKNOWN_STATUS",
                "The screening status has no approved Land Commissioner referral mapping.")
        };
    }
}
