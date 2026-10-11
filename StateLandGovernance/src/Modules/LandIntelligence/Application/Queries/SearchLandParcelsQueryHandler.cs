using StateLandGovernance.LandIntelligence.Application.DTOs;

using StateLandGovernance.LandIntelligence.Application.Interfaces;

using StateLandGovernance.LandIntelligence.Application.Mappings;

using StateLandGovernance.LandIntelligence.Application.Services;

using StateLandGovernance.LandIntelligence.Application.Validators;



namespace StateLandGovernance.LandIntelligence.Application.Queries;



public sealed class SearchLandParcelsQueryHandler

    : IQueryHandler<SearchLandParcelsQuery, LandSearchResponse>

{

    private readonly ILandParcelRepository _landParcelRepository;

    private readonly IRealParcelSourceReadRepository _realParcelSourceReadRepository;

    private readonly LandParcelAvailabilityComposer _availabilityComposer;

    private readonly LandSearchRequestValidator _validator;



    public SearchLandParcelsQueryHandler(

        ILandParcelRepository landParcelRepository,

        IRealParcelSourceReadRepository realParcelSourceReadRepository,

        LandParcelAvailabilityComposer availabilityComposer,

        LandSearchRequestValidator validator)

    {

        _landParcelRepository = landParcelRepository;

        _realParcelSourceReadRepository = realParcelSourceReadRepository;

        _availabilityComposer = availabilityComposer;

        _validator = validator;

    }



    public async Task<LandSearchResponse> HandleAsync(

        SearchLandParcelsQuery query,

        CancellationToken cancellationToken = default)

    {

        var request = LandSearchRequestNormalizer.Normalize(query.Request);

        var validation = _validator.Validate(request);

        if (!validation.IsValid)

        {

            throw new ValidationException(validation.Errors);

        }



        var parcels = await _landParcelRepository.SearchAsync(request, cancellationToken);

        var totalCount = await _landParcelRepository.CountSearchAsync(request, cancellationToken);

        var parcelIds = parcels.Select(parcel => parcel.Id).ToList();

        var cadastralSources = await _realParcelSourceReadRepository.GetByParcelIdsAsync(

            parcelIds,

            cancellationToken);

        var availabilityByParcel = await _availabilityComposer.BuildManyAsync(

            parcelIds,

            cadastralSources,

            cancellationToken);



        var results = parcels

            .Select(parcel => LandParcelMapper.ToSearchResultDto(

                parcel,

                cadastralSources.GetValueOrDefault(parcel.Id),

                availabilityByParcel.GetValueOrDefault(parcel.Id)))

            .ToList();



        return new LandSearchResponse(

            results,

            request.Page,

            request.PageSize,

            totalCount);

    }

}


