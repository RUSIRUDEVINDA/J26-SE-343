using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

/// <summary>
/// Derives primary GIS soil group for a parcel from imported soil-group polygons.
/// Does not overwrite official SoilType without precedence checks.
/// </summary>
public interface ISoilGroupEnrichmentService
{
    Task<SoilGroupEnrichmentResult> EnrichAsync(
        Guid parcelId,
        CancellationToken cancellationToken = default);
}
