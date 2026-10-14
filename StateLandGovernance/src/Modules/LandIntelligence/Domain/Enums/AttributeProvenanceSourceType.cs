namespace StateLandGovernance.LandIntelligence.Domain.Enums;

/// <summary>
/// Origin category for enrichable parcel attributes. Unknown is the safe default when provenance is not recorded.
/// </summary>
public enum AttributeProvenanceSourceType
{
    Official = 1,
    ExternalAuthoritative = 2,
    Derived = 3,
    Imputed = 4,
    Synthetic = 5,
    Unknown = 99
}
