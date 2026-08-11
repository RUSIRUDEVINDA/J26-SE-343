using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;

namespace StateLandGovernance.LandIntelligence.Application.Validators;

public sealed class LandRecommendationRequestValidator : IRequestValidator<LandRecommendationRequest>
{
    public ValidationResult Validate(LandRecommendationRequest request)
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
