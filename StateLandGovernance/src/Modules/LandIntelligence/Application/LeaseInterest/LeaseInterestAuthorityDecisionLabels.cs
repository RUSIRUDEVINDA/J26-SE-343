using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Application.LeaseInterest;

public static class LeaseInterestAuthorityDecisionLabels
{
    public static string DecisionLabel(LeaseInterestAuthorityDecisionKind decision) =>
        decision switch
        {
            LeaseInterestAuthorityDecisionKind.Approved => "Approved",
            LeaseInterestAuthorityDecisionKind.Rejected => "Rejected",
            _ => "Unknown decision",
        };

    public static string? NextActionHint(LeaseInterestAuthorityDecisionKind decision) =>
        decision switch
        {
            LeaseInterestAuthorityDecisionKind.Approved =>
                "Formal lease processing is still required. This preliminary approval does not create a lease or reserve land.",
            LeaseInterestAuthorityDecisionKind.Rejected =>
                "No further action is required on this expression of interest unless you are contacted through official channels.",
            _ => null,
        };
}
