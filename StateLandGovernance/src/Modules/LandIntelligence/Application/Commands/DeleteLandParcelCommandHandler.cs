using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.Validators;
using StateLandGovernance.LandIntelligence.Domain.Exceptions;

namespace StateLandGovernance.LandIntelligence.Application.Commands;

public sealed class DeleteLandParcelCommandHandler
{
    private readonly ILandParcelRepository _landParcelRepository;
    private readonly ILandParcelGraphSynchronizer _graphSynchronizer;
    private readonly DeleteLandParcelCommandValidator _validator;

    public DeleteLandParcelCommandHandler(
        ILandParcelRepository landParcelRepository,
        ILandParcelGraphSynchronizer graphSynchronizer,
        DeleteLandParcelCommandValidator validator)
    {
        _landParcelRepository = landParcelRepository;
        _graphSynchronizer = graphSynchronizer;
        _validator = validator;
    }

    public async Task HandleAsync(
        DeleteLandParcelCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = _validator.Validate(command);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.Errors);
        }

        var parcel = await _landParcelRepository.GetByIdAsync(command.LandParcelId, cancellationToken)
            ?? throw new LandParcelNotFoundException(command.LandParcelId);

        await _landParcelRepository.DeleteAsync(parcel.Id, cancellationToken);
        await _graphSynchronizer.RemoveAfterDeleteAsync(parcel.Id, cancellationToken);
    }
}
