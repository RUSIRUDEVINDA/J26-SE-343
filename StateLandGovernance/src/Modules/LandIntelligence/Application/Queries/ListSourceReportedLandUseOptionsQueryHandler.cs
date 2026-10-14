using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.RealParcels;

namespace StateLandGovernance.LandIntelligence.Application.Queries;

public sealed record ListSourceReportedLandUseOptionsQuery;

public sealed class ListSourceReportedLandUseOptionsQueryHandler
{
    private readonly IRealParcelSourceReadRepository _realParcelSourceReadRepository;

    public ListSourceReportedLandUseOptionsQueryHandler(
        IRealParcelSourceReadRepository realParcelSourceReadRepository)
    {
        _realParcelSourceReadRepository = realParcelSourceReadRepository;
    }

    public async Task<IReadOnlyList<SourceReportedLandUseOptionDto>> HandleAsync(
        ListSourceReportedLandUseOptionsQuery query,
        CancellationToken cancellationToken = default)
    {
        var persisted = await _realParcelSourceReadRepository.ListSourceReportedLandUseOptionsAsync(cancellationToken);
        if (persisted.Count > 0)
        {
            return persisted;
        }

        return ColomboCadastreSourceDomains.ListLandUseFilterOptions()
            .Select(option => new SourceReportedLandUseOptionDto(option.Code, option.EnglishLabel, 0))
            .ToList();
    }
}
