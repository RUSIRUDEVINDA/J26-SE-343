using StateLandGovernance.LandIntelligence.Application.Commands;
using StateLandGovernance.LandIntelligence.Application.Interfaces;

namespace StateLandGovernance.LandIntelligence.Application.Validators;

public sealed class UpdateLandParcelCommandValidator : IRequestValidator<UpdateLandParcelCommand>
{
    public ValidationResult Validate(UpdateLandParcelCommand request)
    {
        var errors = new List<string>();

        if (request.LandParcelId == Guid.Empty)
        {
            errors.Add("Land parcel identifier is required.");
        }

        if (request.CurrentUseType is null
            && request.CurrentUseDescription is null
            && request.SoilType is null
            && request.TerrainDescription is null
            && request.ElevationMeters is null)
        {
            errors.Add("At least one updatable field must be provided.");
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }
}
