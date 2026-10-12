using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

public interface ILandIntelligenceGisCoverageService
{
    Task<LandIntelligenceGisCoverageDto> MeasureCoverageAsync(CancellationToken cancellationToken = default);

    Task<ParcelGisEnrichmentSummaryDto> GetParcelEnrichmentSummaryAsync(
        Guid landParcelId,
        CancellationToken cancellationToken = default);
}
