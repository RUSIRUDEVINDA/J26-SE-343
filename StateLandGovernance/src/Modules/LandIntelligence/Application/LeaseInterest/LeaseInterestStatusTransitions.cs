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
                or LeaseInterestRequestStatus.ReferredToAuthority,
            LeaseInterestRequestStatus.NeedsInformation => next is
                LeaseInterestRequestStatus.PendingVerification
                or LeaseInterestRequestStatus.Declined
                or LeaseInterestRequestStatus.ReferredToAuthority,
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

    public static string ApplicantOutcomeSummary(LeaseInterestRequestStatus status) =>
        status switch
        {
            LeaseInterestRequestStatus.Draft =>
                "Draft — not submitted. This is not a reservation or lease.",
            LeaseInterestRequestStatus.Submitted =>
                "Submitted — awaiting officer queue (preliminary interest only).",
            LeaseInterestRequestStatus.PendingVerification =>
                "Pending officer verification. Your expression of interest is not a reservation or lease approval.",
            LeaseInterestRequestStatus.NeedsInformation =>
                "Officer needs more information before verification can continue. Follow official channels if contacted.",
            LeaseInterestRequestStatus.Declined =>
                "Preliminary interest declined after officer review. This is not a court or authority decision.",
            LeaseInterestRequestStatus.ReferredToAuthority =>
                "Referred for authority follow-up outside this pilot. No lease has been created.",
            _ => "Status unknown.",
        };
}
