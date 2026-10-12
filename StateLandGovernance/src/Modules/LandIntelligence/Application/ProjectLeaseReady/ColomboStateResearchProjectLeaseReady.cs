namespace StateLandGovernance.LandIntelligence.Application.ProjectLeaseReady;

/// <summary>
/// Project-scoped lease-ready designation for the Colombo state research 300-parcel import.
/// Not an official government availability determination.
/// </summary>
public static class ColomboStateResearchProjectLeaseReady
{
    public const string DatasetKey = "colombo-state-research-300";

    public const string DesignationLabel = "Lease-ready — project dataset assumption";

    public const string DesignationNotice =
        "This parcel is marked lease-ready for the J26-SE-343 project dataset only. " +
        "It is not official government verification of lease availability.";

    public const int ExpectedDesignationCount = 300;
}
