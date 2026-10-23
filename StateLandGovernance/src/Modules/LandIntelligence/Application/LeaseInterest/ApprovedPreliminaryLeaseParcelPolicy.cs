namespace StateLandGovernance.LandIntelligence.Application.LeaseInterest;

/// <summary>
/// Shared wording for parcels with authority-approved preliminary interest (status ApprovedInPrinciple).
/// </summary>
public static class ApprovedPreliminaryLeaseParcelPolicy
{
    public const string BlockNewLeaseInterestMessage =
        "This parcel already has an approved preliminary lease interest request. " +
        "You cannot start or submit another expression of interest for this parcel. " +
        "An approved preliminary request is not an executed lease, reserved land, or verified official lease availability.";

    public const string ResearchDemoNotice =
        "Approved preliminary request — research demo only. Not an executed lease, land reservation, or official government approval.";
}
