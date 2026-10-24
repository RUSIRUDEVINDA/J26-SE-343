using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Application.LeaseInterest;

/// <summary>
/// One parcel may have at most one request in <see cref="LeaseInterestRequestStatus.ApprovedInPrinciple"/>.
/// Other open requests are closed when that approval is recorded.
/// </summary>
public static class LeaseInterestParcelExclusiveApprovalPolicy
{
    public const string PreliminaryApprovalDisclaimer =
        "Approved in principle is a preliminary project decision only — not an executed lease, " +
        "land reservation, or official government lease approval.";

    public const string BlockSecondApprovalMessage =
        "This parcel already has another lease-interest request approved in principle. " +
        "Only one preliminary approval per parcel is allowed. " +
        PreliminaryApprovalDisclaimer;

    public const string ApplicantClosedBecauseAnotherApprovedNote =
        "This request was closed because another preliminary expression of interest for the same parcel " +
        "was approved in principle. This is not a rejection of your proposal on its merits. " +
        PreliminaryApprovalDisclaimer;

    public static bool IsOpenForParcelExclusiveClosure(LeaseInterestRequestStatus status) =>
        status is LeaseInterestRequestStatus.Draft
            or LeaseInterestRequestStatus.Submitted
            or LeaseInterestRequestStatus.PendingVerification
            or LeaseInterestRequestStatus.NeedsInformation
            or LeaseInterestRequestStatus.AwaitingValuation
            or LeaseInterestRequestStatus.ReferredToAuthority;

    public static bool IsTerminalForWorkflow(LeaseInterestRequestStatus status) =>
        status is LeaseInterestRequestStatus.Declined
            or LeaseInterestRequestStatus.ApprovedInPrinciple
            or LeaseInterestRequestStatus.ClosedAnotherPreliminaryApproved;
}
