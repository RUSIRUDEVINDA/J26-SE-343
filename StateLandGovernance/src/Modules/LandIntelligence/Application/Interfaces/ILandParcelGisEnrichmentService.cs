using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

/// <summary>
/// Orchestrates H4-H8 GIS enrichment services for a land parcel and returns one unified,
/// explainable intelligence result. Does not persist derived values or affect recommendations.
/// </summary>
public interface ILandParcelGisEnrichmentService
{
    Task<LandParcelGisEnrichmentResult> EnrichAsync(
        Guid parcelId,
        CancellationToken cancellationToken = default);
}
