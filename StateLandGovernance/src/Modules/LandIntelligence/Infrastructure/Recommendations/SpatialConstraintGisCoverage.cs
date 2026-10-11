using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Recommendations;

internal static class SpatialConstraintGisCoverage
{
    /// <summary>
    /// True when unified GIS enrichment completed with conservation/spatial layers available,
    /// so an empty intersection result is a assessed outcome rather than missing coverage.
    /// </summary>
    public static bool IsMappedConservationLayerAssessedClear(LandParcel parcel) =>
        parcel.GisDerivedIntelligence?.EnrichmentStatus == GisEnrichmentOverallStatus.Complete;
}
