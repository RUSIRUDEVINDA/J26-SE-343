namespace StateLandGovernance.GovernanceIntelligence.Domain.Enums;

/// <summary>
/// Identifies the specific recorded case concern evaluated during early governance screening.
/// These represent case-review indicators and do not constitute automated rejections, legal determinations,
/// or accusations against any person.
/// </summary>
public enum EarlyGovernanceIndicatorType
{
    /// <summary>
    /// Active or historical legal dispute recorded in relation to the subject land parcel or case.
    /// </summary>
    LegalDispute = 1,

    /// <summary>
    /// Unauthorized physical or informal occupation recorded on the parcel.
    /// </summary>
    UnauthorizedOccupation = 2,

    /// <summary>
    /// Unauthorized structures or construction activity observed or reported on the parcel.
    /// </summary>
    UnauthorizedConstruction = 3,

    /// <summary>
    /// Formal or informal family, succession, or inheritance claim associated with the parcel.
    /// </summary>
    FamilyOrInheritanceClaim = 4,

    /// <summary>
    /// Competing or concurrent multiple claimants for the parcel or leasehold right.
    /// </summary>
    MultipleClaimants = 5,

    /// <summary>
    /// Unresolved public, neighbor, or institutional objection lodged against the lease or use.
    /// </summary>
    UnresolvedObjection = 6,

    /// <summary>
    /// Recorded prior unlawful land transactions, encroachment, or unauthorized alienation.
    /// </summary>
    PreviousIllegalLandActivity = 7
}
