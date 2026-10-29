namespace StateLandGovernance.LandIntelligence.Domain.Enums;

public enum LeaseInterestRequestStatus
{
    Draft = 1,
    Submitted = 2,
    PendingVerification = 3,
    NeedsInformation = 4,
    Declined = 5,
    ReferredToAuthority = 6,
    /// <summary>
    /// Authority approved (preliminary project decision only).
    /// Not a lease, reservation, or official government lease approval — formal lease processing is still required.
    /// </summary>
    ApprovedInPrinciple = 7,
    /// <summary>
    /// Another request for the same parcel was approved in principle; this request was not rejected on its merits.
    /// </summary>
    ClosedAnotherPreliminaryApproved = 8,
    /// <summary>
    /// Preliminary verification complete; awaiting a different officer's one-time valuation.
    /// </summary>
    AwaitingValuation = 9,
}
