using StateLandGovernance.LandIntelligence.Application.Commands;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.Mappings;
using StateLandGovernance.LandIntelligence.Application.Validators;
using StateLandGovernance.LandIntelligence.Domain.Exceptions;

namespace StateLandGovernance.LandIntelligence.Application.Commands;

public sealed class UpdateLandParcelCommandHandler
    : ICommandHandler<UpdateLandParcelCommand, LandParcelDto>
{
    private readonly ILandParcelRepository _landParcelRepository;
    private readonly ILandParcelGraphSynchronizer _graphSynchronizer;
    private readonly UpdateLandParcelCommandValidator _validator;

    public UpdateLandParcelCommandHandler(
        ILandParcelRepository landParcelRepository,
        ILandParcelGraphSynchronizer graphSynchronizer,
        UpdateLandParcelCommandValidator validator)
    {
        _landParcelRepository = landParcelRepository;
        _graphSynchronizer = graphSynchronizer;
        _validator = validator;
    }

    public async Task<LandParcelDto> HandleAsync(
        UpdateLandParcelCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = _validator.Validate(command);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.Errors);
        }

        var parcel = await _landParcelRepository.GetByIdAsync(command.LandParcelId, cancellationToken)
            ?? throw new LandParcelNotFoundException(command.LandParcelId);

        LandParcelInputMapper.ApplyUpdate(parcel, command);

        await _landParcelRepository.UpdateAsync(parcel, cancellationToken);
        await _graphSynchronizer.SynchronizeAfterPersistAsync(parcel, cancellationToken);

        return LandParcelMapper.ToDto(parcel);
    }
}
