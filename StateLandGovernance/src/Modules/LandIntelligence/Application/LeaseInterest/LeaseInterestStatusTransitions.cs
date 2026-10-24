using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Application.LeaseInterest;

public static class LeaseInterestStatusTransitions
{
    /// <summary>
    /// Officer-driven outcomes after preliminary submission. Not lease approval.
    /// </summary>
    public static bool TryGetTransition(
        LeaseInterestRequestStatus current,
        LeaseInterestRequestStatus next,
        out string? rejectionReason)
    {
        rejectionReason = null;
        if (current == next)
        {
            rejectionReason = "Status is already set to the requested value.";
            return false;
        }

        var allowed = current switch
        {
            LeaseInterestRequestStatus.PendingVerification => next is
                LeaseInterestRequestStatus.NeedsInformation
                or LeaseInterestRequestStatus.Declined
                or LeaseInterestRequestStatus.AwaitingValuation,
            LeaseInterestRequestStatus.NeedsInformation => next is
                LeaseInterestRequestStatus.PendingVerification
                or LeaseInterestRequestStatus.Declined
                or LeaseInterestRequestStatus.AwaitingValuation,
            _ => false,
        };

        if (!allowed)
        {
            rejectionReason =
                $"Cannot change status from {current} to {next}. Only preliminary review transitions are permitted.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Authority decisions after referral and officer valuation. Valuation saves never use this path.
    /// Only Approved → ApprovedInPrinciple and Rejected → Declined are allowed.
    /// </summary>
    public static bool TryGetAuthorityTransition(
        LeaseInterestRequestStatus current,
        LeaseInterestAuthorityDecisionKind decision,
        out LeaseInterestRequestStatus nextStatus,
        out string? rejectionReason)
    {
        nextStatus = current;
        rejectionReason = null;

        if (current != LeaseInterestRequestStatus.ReferredToAuthority)
        {
            rejectionReason =
                "Authority decisions can only be recorded while the request is referred to the authority.";
            return false;
        }

        nextStatus = decision switch
        {
            LeaseInterestAuthorityDecisionKind.Approved =>
                LeaseInterestRequestStatus.ApprovedInPrinciple,
            LeaseInterestAuthorityDecisionKind.Rejected =>
                LeaseInterestRequestStatus.Declined,
            _ => current,
        };

        if (nextStatus == current)
        {
            rejectionReason = "Select Approved or Rejected.";
            return false;
        }

        return true;
    }

    public static string ApplicantOutcomeSummary(LeaseInterestRequestStatus status) =>
        status switch
        {
            LeaseInterestRequestStatus.Draft =>
                "Draft — not submitted.",
            LeaseInterestRequestStatus.Submitted =>
                "Submitted — awaiting officer queue (preliminary interest only).",
            LeaseInterestRequestStatus.PendingVerification => string.Empty,
            LeaseInterestRequestStatus.NeedsInformation =>
                "More information is required before officer verification can continue.",
            LeaseInterestRequestStatus.Declined =>
                "Rejected after review.",
            LeaseInterestRequestStatus.AwaitingValuation =>
                "Preliminary verification is complete. A valuation officer will record the land worksheet next.",
            LeaseInterestRequestStatus.ReferredToAuthority =>
                "Referred for authority follow-up after officer valuation (preliminary only).",
            LeaseInterestRequestStatus.ApprovedInPrinciple => string.Empty,
            LeaseInterestRequestStatus.ClosedAnotherPreliminaryApproved =>
                "Closed — another preliminary request for this parcel was approved in principle. " +
                "This is not a rejection of your proposal on its merits.",
            _ => "Status unknown.",
        };
}
