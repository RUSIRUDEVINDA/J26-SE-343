using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.Mappings;
using StateLandGovernance.LandIntelligence.Application.Validators;

namespace StateLandGovernance.LandIntelligence.Application.Commands;

public sealed class CreateLandParcelCommandHandler
    : ICommandHandler<CreateLandParcelCommand, LandParcelDto>
{
    private readonly ILandParcelRepository _landParcelRepository;
    private readonly CreateLandParcelCommandValidator _validator;

    public CreateLandParcelCommandHandler(
        ILandParcelRepository landParcelRepository,
        CreateLandParcelCommandValidator validator)
    {
        _landParcelRepository = landParcelRepository;
        _validator = validator;
    }

    public async Task<LandParcelDto> HandleAsync(
        CreateLandParcelCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = _validator.Validate(command);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.Errors);
        }

        var existing = await _landParcelRepository.GetByCadastralNumberAsync(
            command.CadastralNumber,
            cancellationToken);

        if (existing is not null)
        {
            throw new ValidationException(["A land parcel with this cadastral number already exists."]);
        }

        var parcel = LandParcelMapper.ToEntity(command);
        await _landParcelRepository.AddAsync(parcel, cancellationToken);

        return LandParcelMapper.ToDto(parcel);
    }
}
