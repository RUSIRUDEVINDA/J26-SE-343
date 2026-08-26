using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

/// <summary>
/// Persists selected H9 unified GIS enrichment results as parcel-derived intelligence
/// without overwriting official Land Commissioner attributes.
/// </summary>
public interface ILandParcelGisEnrichmentPersistenceService
{
    Task PersistAsync(
        LandParcelGisEnrichmentResult result,
        CancellationToken cancellationToken = default);
}
