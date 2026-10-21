using StateLandGovernance.LandIntelligence.Application.Interfaces;

namespace StateLandGovernance.LandIntelligence.Application.Queries;

public sealed record ListDerivedSoilGroupOptionsQuery(string? District = null);

public sealed class ListDerivedSoilGroupOptionsQueryHandler
{
    private readonly ILandParcelRepository _landParcelRepository;

    public ListDerivedSoilGroupOptionsQueryHandler(ILandParcelRepository landParcelRepository)
    {
        _landParcelRepository = landParcelRepository;
    }

    public Task<IReadOnlyList<string>> HandleAsync(
        ListDerivedSoilGroupOptionsQuery query,
        CancellationToken cancellationToken = default) =>
        _landParcelRepository.ListDerivedSoilGroupOptionsAsync(query.District, cancellationToken);
}
