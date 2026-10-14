using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;

namespace StateLandGovernance.LandIntelligence.Application.Queries;

public sealed record GetLandIntelligenceGisCoverageQuery;

public sealed class GetLandIntelligenceGisCoverageQueryHandler
    : IQueryHandler<GetLandIntelligenceGisCoverageQuery, LandIntelligenceGisCoverageDto>
{
    private readonly ILandIntelligenceGisCoverageService _coverageService;

    public GetLandIntelligenceGisCoverageQueryHandler(ILandIntelligenceGisCoverageService coverageService) =>
        _coverageService = coverageService;

    public Task<LandIntelligenceGisCoverageDto> HandleAsync(
        GetLandIntelligenceGisCoverageQuery query,
        CancellationToken cancellationToken = default) =>
        _coverageService.MeasureCoverageAsync(cancellationToken);
}
