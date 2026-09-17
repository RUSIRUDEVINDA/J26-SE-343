using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

/// <summary>
/// Derives parcel-level environmental/spatial evidence from imported GIS soil conservation
/// and soil erosion reference layers. Evidence-only; does not persist parcel restrictions.
/// </summary>
public interface IEnvironmentalSpatialConstraintEnrichmentService
{
    Task<EnvironmentalSpatialConstraintEnrichmentResult> EnrichAsync(
        Guid parcelId,
        CancellationToken cancellationToken = default);
}
