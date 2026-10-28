using StateLandGovernance.GovernanceIntelligence.Application.DTOs;

namespace StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;

public sealed record FindingClosureEligibility(string RuleCode, string RuleVersion, bool CanClose, bool EvidenceRequired, string Reason, bool ReleasesProgressionHold = false);

/// <summary>Officer-review authority, separate from the regulatory evaluation engine.</summary>
public static class FindingClosurePolicy
{
    public const string Operation = "FindingVerifiedClosed";
    public static FindingClosureEligibility Evaluate(ComplianceFindingDto finding, IReadOnlyList<RegulatoryFollowupEvent> history)
    {
        var closed = history.Any(x => x.RuleCode == finding.RuleCode && x.RuleVersion == finding.RuleVersion && x.Operation == Operation);
        var eligible = finding.Status is "InsufficientInformation" or "RequiresHumanReview";
        var release = eligible;
        return new(finding.RuleCode, finding.RuleVersion, eligible && !closed, false,
            closed ? "Verified & Closed for C4 screening. This is not legal, institutional or lease approval."
            : !eligible ? "Only saved Insufficient Information or Human Review Required findings accept this officer disposition."
            : "Resolve this finding for C4 screening only. No remarks or evidence are required. Later legal/institutional approvals are not recorded by this action.", release);
    }
    /// <summary>Whether DS closure resolves this internal C4 screening finding; never external approval.</summary>
    public static bool CanResolveC4Finding(ComplianceFindingDto finding) =>
        finding.Status is "InsufficientInformation" or "RequiresHumanReview";

    public static bool Meaningful(string value, int length, int words) => value.Length >= length
        && value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length >= words
        && value.Count(char.IsLetter) >= length / 2;

    public static void ValidatePayload(RecordRegulatoryFollowup input)
    {
        if (input.ReviewOutcome != null || input.MissingInformation != null)
            throw new ArgumentException("The individual officer disposition does not accept a separate review outcome or missing-information payload.");
    }
}
