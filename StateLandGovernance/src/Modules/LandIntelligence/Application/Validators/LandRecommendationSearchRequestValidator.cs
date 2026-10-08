using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;

namespace StateLandGovernance.LandIntelligence.Application.Validators;

public sealed class LandRecommendationSearchRequestValidator : IRequestValidator<LandRecommendationSearchRequest>
{
    public ValidationResult Validate(LandRecommendationSearchRequest request)
    {
        var errors = new List<string>();

        if (request.MaxResults is <= 0 or > 100)
        {
            errors.Add("Max results must be between 1 and 100.");
        }

        if (request.RequiredAreaHectares is < 0)
        {
            errors.Add("Required area cannot be negative.");
        }

        if (request.AreaTolerancePercent is < 0 or > 100)
        {
            errors.Add("Area tolerance percent must be between 0 and 100.");
        }

        if (request.Accessibility?.MaxRoadDistanceMeters is <= 0)
        {
            errors.Add("Maximum road distance must be greater than zero when specified.");
        }

        if (request.Regulatory?.MaxRegulatoryReferences is < 0)
        {
            errors.Add("Maximum regulatory references cannot be negative.");
        }

        if (request.AdditionalCriteria is not null)
        {
            foreach (var criterion in request.AdditionalCriteria)
            {
                if (string.IsNullOrWhiteSpace(criterion.Key))
                {
                    errors.Add("Custom criterion key is required.");
                }

                if (string.IsNullOrWhiteSpace(criterion.Name))
                {
                    errors.Add("Custom criterion name is required.");
                }

                if (criterion.Weight is < 0 or > 1)
                {
                    errors.Add($"Custom criterion '{criterion.Key}' weight must be between 0 and 1.");
                }
            }

            var duplicateKeys = request.AdditionalCriteria
                .GroupBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateKeys.Count > 0)
            {
                errors.Add($"Duplicate custom criterion keys: {string.Join(", ", duplicateKeys)}.");
            }
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }
}
