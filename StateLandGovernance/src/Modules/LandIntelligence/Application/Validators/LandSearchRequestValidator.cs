using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;

namespace StateLandGovernance.LandIntelligence.Application.Validators;

public sealed class LandSearchRequestValidator : IRequestValidator<LandSearchRequest>
{
    public ValidationResult Validate(LandSearchRequest request)
    {
        var errors = new List<string>();

        if (request.Page <= 0)
        {
            errors.Add("Page must be greater than zero.");
        }

        if (request.PageSize is <= 0 or > 100)
        {
            errors.Add("Page size must be between 1 and 100.");
        }

        if (request.MinArea is not null && request.MaxArea is not null && request.MinArea > request.MaxArea)
        {
            errors.Add("Minimum area cannot exceed maximum area.");
        }

        if (request.MinLatitude is not null && request.MaxLatitude is not null
            && request.MinLatitude > request.MaxLatitude)
        {
            errors.Add("Minimum latitude cannot exceed maximum latitude.");
        }

        if (request.MinLongitude is not null && request.MaxLongitude is not null
            && request.MinLongitude > request.MaxLongitude)
        {
            errors.Add("Minimum longitude cannot exceed maximum longitude.");
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }
}
