using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using ValidationException = StateLandGovernance.LandIntelligence.Application.Interfaces.ValidationException;
using StateLandGovernance.LandIntelligence.Application.Mappings;
using StateLandGovernance.LandIntelligence.Application.Services;
using StateLandGovernance.LandIntelligence.Domain.Exceptions;

namespace StateLandGovernance.LandIntelligence.Application.Queries;

public sealed class GetLandParcelByIdQueryHandler
    : IQueryHandler<GetLandParcelByIdQuery, LandParcelDto>
{
    private readonly ILandParcelRepository _landParcelRepository;
    private readonly IRealParcelSourceReadRepository _realParcelSourceReadRepository;
    private readonly LandParcelAvailabilityComposer _availabilityComposer;
    private readonly ILandIntelligenceGisCoverageService _gisCoverageService;

    public GetLandParcelByIdQueryHandler(
        ILandParcelRepository landParcelRepository,
        IRealParcelSourceReadRepository realParcelSourceReadRepository,
        LandParcelAvailabilityComposer availabilityComposer,
        ILandIntelligenceGisCoverageService gisCoverageService)
    {
        _landParcelRepository = landParcelRepository;
        _realParcelSourceReadRepository = realParcelSourceReadRepository;
        _availabilityComposer = availabilityComposer;
        _gisCoverageService = gisCoverageService;
    }

    public async Task<LandParcelDto> HandleAsync(
        GetLandParcelByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.LandParcelId == Guid.Empty)
        {
            throw new ValidationException(["Land parcel identifier is required."]);
        }

        var parcel = await _landParcelRepository.GetByIdAsync(query.LandParcelId, cancellationToken)
            ?? throw new LandParcelNotFoundException(query.LandParcelId);

        var cadastralSource = await _realParcelSourceReadRepository.GetByParcelIdAsync(
            parcel.Id,
            cancellationToken);

        var availability = await _availabilityComposer.BuildAsync(
            parcel.Id,
            cadastralSource,
            cancellationToken);

        var gisEnrichment = await _gisCoverageService.GetParcelEnrichmentSummaryAsync(
            parcel.Id,
            cancellationToken);

        return LandParcelMapper.ToDto(parcel, cadastralSource, availability, gisEnrichment);
    }
}
