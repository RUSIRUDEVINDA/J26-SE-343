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

/// Evaluates non-negotiable legal and environmental restrictions before ranking.

/// Always applied — not controlled by optional applicant preference fields.

/// </summary>

internal static class HardConstraintEvaluator

{

    private static readonly EnvironmentalRestrictionType[] GisConservationTypes =

    [

        EnvironmentalRestrictionType.ProtectedArea,

        EnvironmentalRestrictionType.ForestReserve,

        EnvironmentalRestrictionType.WildlifeCorridor,

    ];



    public static HardConstraintEvaluation Evaluate(LandParcel parcel)

    {

        var violations = new List<string>();



        foreach (var constraint in parcel.SpatialConstraints.Where(c => c.Severity == RestrictionSeverity.Prohibitive))

        {

            violations.Add($"Prohibitive spatial constraint ({constraint.Type}).");

        }



        foreach (var restriction in parcel.EnvironmentalRestrictions)

        {

            if (restriction.Severity == RestrictionSeverity.Prohibitive)

            {

                violations.Add($"Prohibitive environmental restriction ({restriction.Type}).");

                continue;

            }



            if (restriction.Severity == RestrictionSeverity.High)

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


