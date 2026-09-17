using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

/// <summary>
/// Derives nearest GIS road accessibility evidence for a parcel from imported reference roads.
/// Does not persist results or connect to recommendation scoring.
/// </summary>
public interface IRoadAccessibilityEnrichmentService
{
    Task<RoadAccessibilityEnrichmentResult> EnrichAsync(
        Guid parcelId,
        CancellationToken cancellationToken = default);
}
