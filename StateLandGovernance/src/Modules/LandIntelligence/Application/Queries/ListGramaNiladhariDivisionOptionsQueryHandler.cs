using StateLandGovernance.LandIntelligence.Application.Interfaces;

namespace StateLandGovernance.LandIntelligence.Application.Queries;

public sealed record ListGramaNiladhariDivisionOptionsQuery(string? District = null);

public sealed class ListGramaNiladhariDivisionOptionsQueryHandler
{
    private readonly IParcelGnDivisionMappingReadRepository _gnDivisionMappingReadRepository;

    public ListGramaNiladhariDivisionOptionsQueryHandler(
        IParcelGnDivisionMappingReadRepository gnDivisionMappingReadRepository) =>
        _gnDivisionMappingReadRepository = gnDivisionMappingReadRepository;

    public Task<IReadOnlyList<string>> HandleAsync(
        ListGramaNiladhariDivisionOptionsQuery query,
        CancellationToken cancellationToken = default) =>
        _gnDivisionMappingReadRepository.ListDistinctGnDivisionNamesAsync(
            query.District,
            cancellationToken);
}
