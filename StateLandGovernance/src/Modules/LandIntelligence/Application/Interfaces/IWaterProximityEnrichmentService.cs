using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

/// <summary>
/// Derives nearest GIS canal/lake proximity evidence for a parcel.
/// Factual spatial intelligence only; does not persist or affect recommendation scoring.
/// </summary>
public interface IWaterProximityEnrichmentService
{
    Task<WaterProximityEnrichmentResult> EnrichAsync(
        Guid parcelId,
        CancellationToken cancellationToken = default);
}
