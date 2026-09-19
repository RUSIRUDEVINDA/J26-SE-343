using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Recommendations;

internal sealed record HardConstraintEvaluation(
    bool IsViolated,
    IReadOnlyList<string> Violations)
{
    public string Summary => Violations.Count == 0
        ? string.Empty
        : string.Join(" ", Violations);
}

/// <summary>
/// Evaluates non-negotiable legal and environmental restrictions before any
/// supplementary ML suitability assessment is considered.
/// </summary>
internal static class HardConstraintEvaluator
{
    private static readonly EnvironmentalRestrictionType[] GisConservationTypes =
    [
        EnvironmentalRestrictionType.ProtectedArea,
        EnvironmentalRestrictionType.ForestReserve,
        EnvironmentalRestrictionType.WildlifeCorridor,
    ];

    public static HardConstraintEvaluation Evaluate(
        LandParcel parcel,
        LandRecommendationSearchRequest request)
    {
        var violations = new List<string>();
        var rejectSevereEnvironmental = request.Environmental?.RejectProhibitiveEnvironmentalRestrictions ?? true;

        foreach (var constraint in parcel.SpatialConstraints.Where(c => c.Severity == RestrictionSeverity.Prohibitive))
        {
            violations.Add($"Prohibitive spatial constraint ({constraint.Type}).");
        }

        foreach (var restriction in parcel.EnvironmentalRestrictions)
        {
            if (rejectSevereEnvironmental && restriction.Severity == RestrictionSeverity.Prohibitive)
            {
                violations.Add($"Prohibitive environmental restriction ({restriction.Type}).");
                continue;
            }

            if (rejectSevereEnvironmental && restriction.Severity == RestrictionSeverity.High)
            {
                violations.Add($"High-severity environmental restriction ({restriction.Type}).");
                continue;
            }

            if (GisDerivedIntelligenceDetector.IsGisDerivedEnvironmentalRestriction(restriction)
                && GisConservationTypes.Contains(restriction.Type))
            {
                violations.Add(
                    $"Parcel intersects mapped conservation area ({restriction.Type}): {restriction.Description}");
            }
        }

        return new HardConstraintEvaluation(violations.Count > 0, violations);
    }
}
