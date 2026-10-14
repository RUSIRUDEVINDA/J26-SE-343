namespace StateLandGovernance.LandIntelligence.Domain.Enums;

public enum LeaseInterestRequestStatus
{
    Draft = 1,
    Submitted = 2,
    PendingVerification = 3,
    NeedsInformation = 4,
    Declined = 5,
    ReferredToAuthority = 6,
}
