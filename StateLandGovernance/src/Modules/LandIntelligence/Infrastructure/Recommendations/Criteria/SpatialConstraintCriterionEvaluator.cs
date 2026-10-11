using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Recommendations.Criteria;

internal sealed class SpatialConstraintCriterionEvaluator : IRecommendationCriterionEvaluator
{
    private const string CriterionKey = "spatial-constraints";

    public const decimal DefaultWeight = 0.10m;
    public string Key => CriterionKey;
    public int Order => 80;

    public bool IsApplicable(LandRecommendationSearchRequest request) => true;

    public CriterionEvaluationDto Evaluate(LandParcel parcel, LandRecommendationSearchRequest request)
    {
        var recordedSpatial = parcel.SpatialConstraints.ToList();
        var gisMappedConservation = parcel.EnvironmentalRestrictions
            .Where(GisDerivedIntelligenceDetector.IsGisDerivedEnvironmentalRestriction)
            .ToList();

        if (recordedSpatial.Count > 0)
        {
            return EvaluateRecordedSpatialConstraints(recordedSpatial);
        }

        if (gisMappedConservation.Count > 0)
        {
            return EvaluateMappedGisRestrictions(gisMappedConservation);
        }

        if (!SpatialConstraintGisCoverage.IsMappedConservationLayerAssessedClear(parcel))
        {
            return CreateCoverageNotAssessed(parcel);
        }

        return CriterionEvaluationFactory.Create(
            CriterionKey,
            "Spatial Constraints",
            CriterionCategory.SpatialConstraintImpact,
            isMet: true,
            score: 100m,
            DefaultWeight,
            "Mapped conservation/spatial GIS layer assessed; no intersection found within imported coverage.",
            AttributeProvenance.Derived(GisDerivedIntelligenceOwnership.SourceName),
            "spatial.constraints");
    }

    private static CriterionEvaluationDto CreateCoverageNotAssessed(LandParcel parcel)
    {
        var enrichmentStatus = parcel.GisDerivedIntelligence?.EnrichmentStatus;
        var (summary, reason) = enrichmentStatus switch
        {
            null => (
                "No mapped conservation/spatial GIS enrichment is available for this parcel.",
                "Spatial constraint GIS coverage missing"),
            GisEnrichmentOverallStatus.Unavailable => (
                "Spatial constraint GIS coverage unavailable for this parcel.",
                "Spatial constraint coverage unavailable"),
            GisEnrichmentOverallStatus.Partial => (
                "Mapped conservation/spatial GIS layer was not fully assessed for this parcel.",
                "Spatial constraint GIS coverage incomplete"),
            _ => (
                "Mapped conservation/spatial GIS layer was not assessed for this parcel.",
                "Spatial constraint GIS coverage not assessed")
        };

        return CriterionEvaluationFactory.Create(
            CriterionKey,
            "Spatial Constraints",
            CriterionCategory.SpatialConstraintImpact,
            isMet: false,
            score: 0m,
            DefaultWeight,
            summary,
            AttributeProvenance.Unknown("Spatial constraints"),
            "spatial.constraints",
            wasAssessed: false,
            notAssessedReason: reason);
    }

    private static CriterionEvaluationDto EvaluateRecordedSpatialConstraints(
        IReadOnlyList<SpatialConstraint> constraints)
    {
        var maxSeverity = constraints.Max(c => c.Severity);
        var sample = constraints.OrderByDescending(c => c.Severity).First();

        if (maxSeverity == RestrictionSeverity.Prohibitive)
        {
            return CriterionEvaluationFactory.Create(
                CriterionKey,
                "Spatial Constraints",
                CriterionCategory.SpatialConstraintImpact,
                isMet: false,
                score: 10m,
                DefaultWeight,
                $"Parcel has prohibitive spatial constraint ({sample.Type}): {sample.Description}");
        }

        var score = maxSeverity switch
        {
            RestrictionSeverity.Low => 90m,
            RestrictionSeverity.Medium => 70m,
            RestrictionSeverity.High => 45m,
            _ => 30m
        };

        return CriterionEvaluationFactory.Create(
            CriterionKey,
            "Spatial Constraints",
            CriterionCategory.SpatialConstraintImpact,
            isMet: maxSeverity <= RestrictionSeverity.Medium,
            score,
            DefaultWeight,
            $"{constraints.Count} spatial constraint(s) recorded; highest severity is {maxSeverity}.");
    }

    private static CriterionEvaluationDto EvaluateMappedGisRestrictions(
        IReadOnlyList<EnvironmentalRestriction> gisRestrictions)
    {
        var maxSeverity = gisRestrictions.Max(r => r.Severity);
        var sample = gisRestrictions.OrderByDescending(r => r.Severity).First();

        if (maxSeverity == RestrictionSeverity.Prohibitive)
        {
            return CriterionEvaluationFactory.Create(
                CriterionKey,
                "Spatial Constraints",
                CriterionCategory.SpatialConstraintImpact,
                isMet: false,
                score: 10m,
                DefaultWeight,
                $"Mapped GIS conservation/spatial intersection recorded: {sample.Description}",
                sample.DataProvenance,
                "spatial.constraints");
        }

        var score = maxSeverity switch
        {
            RestrictionSeverity.Low => 90m,
            RestrictionSeverity.Medium => 70m,
            RestrictionSeverity.High => 45m,
            _ => 30m
        };

        return CriterionEvaluationFactory.Create(
            CriterionKey,
            "Spatial Constraints",
            CriterionCategory.SpatialConstraintImpact,
            isMet: maxSeverity <= RestrictionSeverity.Medium,
            score,
            DefaultWeight,
            $"{gisRestrictions.Count} mapped GIS conservation/spatial intersection(s); highest severity is {maxSeverity}.",
            sample.DataProvenance,
            "spatial.constraints");
    }
}
