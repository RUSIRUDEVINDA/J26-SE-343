namespace StateLandGovernance.LandIntelligence.Domain.Enums;

/// <summary>
/// Overall status of persisted GIS enrichment for a parcel (H10 snapshot).
/// </summary>
public enum GisEnrichmentOverallStatus
{
    Complete = 1,
    Partial = 2,
    Unavailable = 3
}
