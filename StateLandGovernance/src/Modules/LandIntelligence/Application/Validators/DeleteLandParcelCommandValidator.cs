using StateLandGovernance.LandIntelligence.Application.Commands;
using StateLandGovernance.LandIntelligence.Application.Interfaces;

namespace StateLandGovernance.LandIntelligence.Application.Validators;

public sealed class DeleteLandParcelCommandValidator : IRequestValidator<DeleteLandParcelCommand>
{
    public ValidationResult Validate(DeleteLandParcelCommand request)
    {
        var errors = new List<string>();

        if (request.LandParcelId == Guid.Empty)
        {
            errors.Add("Land parcel identifier is required.");
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }
}
