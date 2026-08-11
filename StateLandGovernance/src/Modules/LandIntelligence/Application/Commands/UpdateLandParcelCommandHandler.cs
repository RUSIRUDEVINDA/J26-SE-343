using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.Mappings;
using StateLandGovernance.LandIntelligence.Application.Validators;
using StateLandGovernance.LandIntelligence.Domain.Exceptions;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.LandIntelligence.Application.Commands;

public sealed class UpdateLandParcelCommandHandler
    : ICommandHandler<UpdateLandParcelCommand, LandParcelDto>
{
    private readonly ILandParcelRepository _landParcelRepository;
    private readonly UpdateLandParcelCommandValidator _validator;

    public UpdateLandParcelCommandHandler(
        ILandParcelRepository landParcelRepository,
        UpdateLandParcelCommandValidator validator)
    {
        _landParcelRepository = landParcelRepository;
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

        if (command.CurrentUseType is not null)
        {
            parcel.UpdateCurrentUse(new LandUse(command.CurrentUseType.Value, command.CurrentUseDescription));
        }

        if (command.SoilType is not null
            || command.TerrainDescription is not null
            || command.ElevationMeters is not null)
        {
            var existing = parcel.Characteristics;
            parcel.UpdateCharacteristics(new LandCharacteristics(
                command.SoilType ?? existing?.SoilType,
                command.TerrainDescription ?? existing?.TerrainDescription,
                command.ElevationMeters ?? existing?.ElevationMeters));
        }

        await _landParcelRepository.UpdateAsync(parcel, cancellationToken);

        return LandParcelMapper.ToDto(parcel);
    }
}
