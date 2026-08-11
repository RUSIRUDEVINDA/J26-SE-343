using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.Mappings;
using StateLandGovernance.LandIntelligence.Domain.Exceptions;

namespace StateLandGovernance.LandIntelligence.Application.Queries;

public sealed class GetLandParcelByIdQueryHandler
    : IQueryHandler<GetLandParcelByIdQuery, LandParcelDto>
{
    private readonly ILandParcelRepository _landParcelRepository;

    public GetLandParcelByIdQueryHandler(ILandParcelRepository landParcelRepository)
    {
        _landParcelRepository = landParcelRepository;
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

        return LandParcelMapper.ToDto(parcel);
    }
}
