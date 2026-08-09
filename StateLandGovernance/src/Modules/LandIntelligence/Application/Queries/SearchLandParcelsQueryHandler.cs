using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.Mappings;
using StateLandGovernance.LandIntelligence.Application.Validators;

namespace StateLandGovernance.LandIntelligence.Application.Queries;

public sealed class SearchLandParcelsQueryHandler
    : IQueryHandler<SearchLandParcelsQuery, LandSearchResponse>
{
    private readonly ILandParcelRepository _landParcelRepository;
    private readonly LandSearchRequestValidator _validator;

    public SearchLandParcelsQueryHandler(
        ILandParcelRepository landParcelRepository,
        LandSearchRequestValidator validator)
    {
        _landParcelRepository = landParcelRepository;
        _validator = validator;
    }

    public async Task<LandSearchResponse> HandleAsync(
        SearchLandParcelsQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = _validator.Validate(query.Request);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.Errors);
        }

        var parcels = await _landParcelRepository.SearchAsync(query.Request, cancellationToken);
        var results = parcels.Select(LandParcelMapper.ToSearchResultDto).ToList();

        return new LandSearchResponse(
            results,
            query.Request.Page,
            query.Request.PageSize,
            results.Count);
    }
}
