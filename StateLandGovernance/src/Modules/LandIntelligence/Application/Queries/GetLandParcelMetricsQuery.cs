using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.Services;

namespace StateLandGovernance.LandIntelligence.Application.Queries;

public sealed record GetLandParcelMetricsQuery;

public sealed class GetLandParcelMetricsQueryHandler
    : IQueryHandler<GetLandParcelMetricsQuery, LandParcelMetricsDto>
{
    private readonly LandParcelAvailabilityComposer _availabilityComposer;

    public GetLandParcelMetricsQueryHandler(LandParcelAvailabilityComposer availabilityComposer) =>
        _availabilityComposer = availabilityComposer;

    public Task<LandParcelMetricsDto> HandleAsync(
        GetLandParcelMetricsQuery query,
        CancellationToken cancellationToken = default) =>
        _availabilityComposer.BuildMetricsAsync(cancellationToken);
}
