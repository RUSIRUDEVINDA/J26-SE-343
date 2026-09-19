using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

/// <summary>
/// Builds GIS-derived parcel intelligence sync requests from PostGIS persisted state only.
/// </summary>
public interface IGisGraphSyncRequestBuilder
{
    Task<GisDerivedParcelIntelligenceGraphSyncRequest?> BuildSyncRequestAsync(
        Guid parcelId,
        CancellationToken cancellationToken = default);
}
