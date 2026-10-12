using StateLandGovernance.GovernanceIntelligence.Application.DTOs;

namespace StateLandGovernance.GovernanceIntelligence.Application.Interfaces;

/// <summary>
/// Development-only PP1 policy. It authorizes durable synthetic referrals only for the two
/// approved review scenarios and never represents a production Commissioner-referral rule.
/// </summary>
public sealed class Pp1EarlyGovernanceReferralPolicy : IEarlyGovernanceReferralPolicy
{
    private static readonly HashSet<string> ApprovedReferralCases = new(StringComparer.Ordinal)
    {
        "PP1-FINAL-REFERRAL-001",
        "PP1-FINAL-COMBINED-001"
    };

    public EarlyGovernanceReferralPolicyDecision Evaluate(EarlyGovernanceScreeningResultDto screeningResult)
    {
        ArgumentNullException.ThrowIfNull(screeningResult);

        if (screeningResult.OverallStatus == "ReviewRequired" &&
            ApprovedReferralCases.Contains(screeningResult.CaseId) &&
            screeningResult.InputVersion == Projections.GovernanceInputProvenance.FinalPp1SyntheticSeedVersion)
        {
            var evidence = screeningResult.IndicatorResults
                .Where(item => item.RequiresReview && !string.IsNullOrWhiteSpace(item.EvidenceReference))
                .Select(item => item.EvidenceReference!)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            return EarlyGovernanceReferralPolicyDecision.ReferralRequired(
                "PP1_SYNTHETIC_REVIEW_REFERRAL",
                "Development-only PP1 synthetic scenario requested a Commissioner review handoff; no real Component 3 termination is asserted.",
                evidence);
        }

        return screeningResult.OverallStatus == "Clear"
            ? EarlyGovernanceReferralPolicyDecision.NotRequired(
                "PP1_REFERRAL_NOT_REQUIRED_CLEAR",
                "The approved PP1 synthetic scenario produced a Clear screening result.")
            : EarlyGovernanceReferralPolicyDecision.Unresolved(
                "PP1_REFERRAL_NOT_AUTHORIZED",
                "The outcome is not one of the approved PP1 synthetic referral scenarios.");
    }
}
