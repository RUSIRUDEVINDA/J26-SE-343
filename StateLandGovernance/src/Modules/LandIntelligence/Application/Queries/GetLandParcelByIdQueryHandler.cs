using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.Mappings;
using StateLandGovernance.LandIntelligence.Domain.Exceptions;

namespace StateLandGovernance.LandIntelligence.Application.Queries;

public sealed class GetLandParcelByIdQueryHandler
    : IQueryHandler<GetLandParcelByIdQuery, LandParcelDto>
{
    private readonly ILandParcelRepository _landParcelRepository;
    private readonly IRealParcelSourceReadRepository _realParcelSourceReadRepository;
    private readonly IResearchSimulatedLeaseReadRepository _researchSimulatedLeaseReadRepository;
    private readonly IResearchDemoMode _researchDemoMode;

    public GetLandParcelByIdQueryHandler(
        ILandParcelRepository landParcelRepository,
        IRealParcelSourceReadRepository realParcelSourceReadRepository,
        IResearchSimulatedLeaseReadRepository researchSimulatedLeaseReadRepository,
        IResearchDemoMode researchDemoMode)
    {
        _landParcelRepository = landParcelRepository;
        _realParcelSourceReadRepository = realParcelSourceReadRepository;
        _researchSimulatedLeaseReadRepository = researchSimulatedLeaseReadRepository;
        _researchDemoMode = researchDemoMode;
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

        ResearchSimulatedLeaseWorkflowDto? simulated = null;
        if (_researchDemoMode.IsEnabled)
        {
            simulated = await _researchSimulatedLeaseReadRepository.GetByParcelIdAsync(
                parcel.Id,
                cancellationToken);
        }

        var availability = LandParcelAvailabilityMapper.Build(
            cadastralSource,
            simulated,
            _researchDemoMode.IsEnabled);

        return LandParcelMapper.ToDto(parcel, cadastralSource, availability);
    }
}
