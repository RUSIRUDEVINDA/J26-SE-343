using StateLandGovernance.LandIntelligence.Application.Commands;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Application.Validators;

public sealed class CreateLandParcelCommandValidator : IRequestValidator<CreateLandParcelCommand>
{
    public ValidationResult Validate(CreateLandParcelCommand request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.CadastralNumber))
        {
            errors.Add("Cadastral number is required.");
        }

        if (request.AreaValue <= 0)
        {
            errors.Add("Area value must be greater than zero.");
        }

        if (!Enum.IsDefined(request.AreaUnit))
        {
            errors.Add("Area unit must be SquareMeters, Hectares, or Acres.");
        }

        if (string.IsNullOrWhiteSpace(request.Province))
        {
            errors.Add("Province is required.");
        }

        if (string.IsNullOrWhiteSpace(request.District))
        {
            errors.Add("District is required.");
        }

        if (string.IsNullOrWhiteSpace(request.DivisionalSecretariat))
        {
            errors.Add("Divisional secretariat is required.");
        }

        if (request.CentroidLatitude is < -90 or > 90)
        {
            errors.Add("Centroid latitude must be between -90 and 90.");
        }

        if (request.CentroidLongitude is < -180 or > 180)
        {
            errors.Add("Centroid longitude must be between -180 and 180.");
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }
}
