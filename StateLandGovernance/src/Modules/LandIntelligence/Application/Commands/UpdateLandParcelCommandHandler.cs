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
    private readonly ILandParcelGisEnrichmentPersistenceService _gisEnrichmentPersistence;
    private readonly UpdateLandParcelCommandValidator _validator;

    public UpdateLandParcelCommandHandler(
        ILandParcelRepository landParcelRepository,
        ILandParcelGraphSynchronizer graphSynchronizer,
        ILandParcelGisEnrichmentPersistenceService gisEnrichmentPersistence,
        UpdateLandParcelCommandValidator validator)
    {
        _landParcelRepository = landParcelRepository;
        _graphSynchronizer = graphSynchronizer;
        _gisEnrichmentPersistence = gisEnrichmentPersistence;
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

        var previousLatitude = parcel.Spatial.CentroidLatitude;
        var previousLongitude = parcel.Spatial.CentroidLongitude;
        var previousBoundary = parcel.Spatial.Boundary;

        LandParcelInputMapper.ApplyUpdate(parcel, command);

        var locationChanged =
            !NearlyEqual(previousLatitude, parcel.Spatial.CentroidLatitude)
            || !NearlyEqual(previousLongitude, parcel.Spatial.CentroidLongitude)
            || !ReferenceEquals(previousBoundary, parcel.Spatial.Boundary);

        await _landParcelRepository.UpdateAsync(parcel, cancellationToken);

        if (locationChanged)
        {
            await _gisEnrichmentPersistence.InvalidateLocationDependentEvidenceAsync(
                parcel.Id,
                cancellationToken);
        }

        await _graphSynchronizer.SynchronizeAfterPersistAsync(parcel, cancellationToken);

        return LandParcelMapper.ToDto(parcel);
    }

    private static bool NearlyEqual(double left, double right) =>
        Math.Abs(left - right) < 1e-9;
}
