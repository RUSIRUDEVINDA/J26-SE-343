using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

/// <summary>
/// Contract for spatial overlay and constraint analysis (PostGIS implementation deferred).
/// </summary>
public interface ISpatialAnalysisService
{
    Task<IReadOnlyList<SpatialConstraintDto>> AnalyzeConstraintsAsync(
        Guid parcelId,
        CancellationToken cancellationToken = default);
}
