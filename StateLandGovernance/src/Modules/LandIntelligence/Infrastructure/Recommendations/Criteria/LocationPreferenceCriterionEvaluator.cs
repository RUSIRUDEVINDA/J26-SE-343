using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Recommendations.Criteria;

internal sealed class LocationPreferenceCriterionEvaluator : IRecommendationCriterionEvaluator
{
    public const decimal DefaultWeight = 0.10m;
    public string Key => "location-preference";
    public int Order => 40;

    public bool IsApplicable(LandRecommendationSearchRequest request) =>
        request.PreferredLocation is not null;

    public CriterionEvaluationDto Evaluate(LandParcel parcel, LandRecommendationSearchRequest request)
    {
        var preferred = request.PreferredLocation!;
        var location = parcel.Location;
        var checks = new List<(string Label, bool Matched)>();

        if (!string.IsNullOrWhiteSpace(preferred.Province))
        {
            checks.Add(("province", Matches(preferred.Province, location.Province)));
        }

        if (!string.IsNullOrWhiteSpace(preferred.District))
        {
            checks.Add(("district", Matches(preferred.District, location.District)));
        }

        if (!string.IsNullOrWhiteSpace(preferred.DivisionalSecretariat))
        {
            checks.Add(("divisional secretariat", Matches(preferred.DivisionalSecretariat, location.DivisionalSecretariat)));
        }

        if (checks.Count == 0)
        {
            return CriterionEvaluationFactory.Create(
                Key,
                "Location Preference",
                CriterionCategory.LocationPreference,
                isMet: true,
                score: 100m,
                DefaultWeight,
                "No specific location filters were supplied.");
        }

        var matchedCount = checks.Count(c => c.Matched);
        var score = (decimal)matchedCount / checks.Count * 100m;
        var isMet = matchedCount == checks.Count;
        var failed = checks.Where(c => !c.Matched).Select(c => c.Label);

        return CriterionEvaluationFactory.Create(
            Key,
            "Location Preference",
            CriterionCategory.LocationPreference,
            isMet,
            score,
            DefaultWeight,
            isMet
                ? "Parcel location matches all preferred administrative boundaries."
                : $"Parcel location partially matches preferred location; unmatched: {string.Join(", ", failed)}.");
    }

    private static bool Matches(string preferred, string actual) =>
        string.Equals(preferred.Trim(), actual.Trim(), StringComparison.OrdinalIgnoreCase);
}
